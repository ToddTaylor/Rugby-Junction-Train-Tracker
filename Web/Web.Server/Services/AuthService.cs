using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Web.Server.DTOs;
using Web.Server.Providers;
using Web.Server.Repositories;

namespace Web.Server.Services;

public class AuthService : IAuthService
{
    private class CodeEntry
    {
        public string CodeHash { get; set; } = string.Empty;
        public DateTime ExpiresUtc { get; set; }
        public int FailedAttempts { get; set; }
    }

    private class TokenEntry
    {
        public int UserId { get; set; }
        public DateTime ExpiresUtc { get; set; }
        public DateTime LastRefreshed { get; set; }
    }

    private readonly ITimeProvider _timeProvider;
    private readonly ILogger<AuthService> _logger;
    private readonly IUserRepository _userRepository;
    private readonly Data.TelemetryDbContext _db;
    private readonly IEmailService _emailService;

    // email -> CodeEntry
    private static readonly ConcurrentDictionary<string, CodeEntry> _codes = new(StringComparer.OrdinalIgnoreCase);
    // email -> list of send timestamps (UTC)
    private static readonly ConcurrentDictionary<string, List<DateTime>> _sendHistory = new(StringComparer.OrdinalIgnoreCase);
    // Refresh LastLogin at most once per hour per user
    private static readonly TimeSpan LastLoginRefreshInterval = TimeSpan.FromHours(1);

    public const int CodeLength = 6;
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan ShortSessionLifetime = TimeSpan.FromHours(12);
    public static readonly TimeSpan LongSessionLifetime = TimeSpan.FromDays(365);
    public const int MaxSendsPerHour = 5;
    /// <summary>
    /// Wrong codes allowed before the code is discarded and a new one must be requested.
    /// </summary>
    public const int MaxVerifyAttempts = 5;
    public const string InvalidCodeError = "That code is not valid or has expired. Check it, or request a new code.";

    private static readonly TimeSpan SendHistoryWindow = TimeSpan.FromHours(1);
    private const int PruneThreshold = 1000;

    public AuthService(ITimeProvider timeProvider, ILogger<AuthService> logger, IUserRepository userRepository, Data.TelemetryDbContext db, IEmailService emailService)
    {
        _timeProvider = timeProvider;
        _logger = logger;
        _userRepository = userRepository;
        _db = db;
        _emailService = emailService;
    }

    public async Task<(bool Success, List<string> Errors)> SendCodeAsync(string email)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(email))
        {
            errors.Add("Email is required.");
            return (false, errors);
        }

        email = email.Trim();
        var now = _timeProvider.UtcNow;
        PruneExpiredEntries(now);

        // The limit applies to every address, registered or not, so hitting it says nothing about the address.
        var history = _sendHistory.GetOrAdd(email, _ => new List<DateTime>());
        lock (history)
        {
            history.RemoveAll(d => (now - d) > SendHistoryWindow);
            if (history.Count >= MaxSendsPerHour)
            {
                errors.Add("Rate limit exceeded. Try again later.");
                return (false, errors);
            }
            history.Add(now);
        }

        // An address that is not registered, or is inactive, gets the same answer as one that is, and no email.
        // Otherwise this endpoint would tell anyone which addresses have accounts.
        var user = await _userRepository.GetByEmailAsync(email);
        if (user == null || !user.IsActive)
        {
            return (true, errors);
        }

        var code = GenerateNumericCode(CodeLength);
        var codeHash = Hash(code);
        _codes[email] = new CodeEntry { CodeHash = codeHash, ExpiresUtc = now.Add(CodeLifetime) };

        var (emailSuccess, emailErrors) = await _emailService.SendVerificationCodeAsync(email, code);
        if (!emailSuccess)
        {
            errors.AddRange(emailErrors);

            // Roll back the code entry if it still matches the one we just created
            if (_codes.TryGetValue(email, out var existingEntry) && existingEntry.CodeHash == codeHash)
            {
                _codes.TryRemove(email, out _);
            }

            // Roll back the send-history entry we just added
            lock (history)
            {
                if (history.Count > 0 && history[history.Count - 1] == now)
                {
                    history.RemoveAt(history.Count - 1);
                }
            }
            return (false, errors);
        }

        return (true, errors);
    }

    public async Task<(bool Success, AuthVerifySuccessDTO? Result, List<string> Errors)> VerifyCodeAsync(string email, string code, bool remember)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code))
        {
            errors.Add("Email and code are required.");
            return (false, null, errors);
        }

        email = email.Trim();

        // Every failure below gives the same message. Different messages for "no code was sent", "wrong code"
        // and "too many attempts" would tell a caller whether the address has an account.
        if (!_codes.TryGetValue(email, out var entry))
        {
            errors.Add(InvalidCodeError);
            return (false, null, errors);
        }

        var now = _timeProvider.UtcNow;
        lock (entry)
        {
            if (now > entry.ExpiresUtc || entry.FailedAttempts >= MaxVerifyAttempts)
            {
                RemoveCode(email, entry);
                errors.Add(InvalidCodeError);
                return (false, null, errors);
            }

            if (!HashesEqual(entry.CodeHash, Hash(code.Trim())))
            {
                // A six digit code can be guessed in a million tries, so it is thrown away after a few wrong ones.
                entry.FailedAttempts++;
                if (entry.FailedAttempts >= MaxVerifyAttempts)
                {
                    RemoveCode(email, entry);
                    _logger.LogWarning("Sign-in code for {Email} was discarded after {Attempts} wrong attempts.", email, entry.FailedAttempts);
                }

                errors.Add(InvalidCodeError);
                return (false, null, errors);
            }

            // The code is right: use it up now, so it cannot be used twice.
            RemoveCode(email, entry);
        }

        // Look up user and get roles
        var user = await _userRepository.GetByEmailAsync(email);
        if (user == null || !user.IsActive)
        {
            errors.Add(InvalidCodeError);
            return (false, null, errors);
        }

        // Update last login timestamp
        user.LastLogin = now;
        await _userRepository.UpdateAsync(user);

        // Get user roles
        var roles = user.UserRoles?.Select(ur => ur.Role?.RoleName ?? string.Empty)
            .Where(r => !string.IsNullOrEmpty(r))
            .ToList() ?? new List<string>();

        // success: generate token
        var token = Guid.NewGuid().ToString("N");
        var expires = now.Add(remember ? LongSessionLifetime : ShortSessionLifetime);
        // Store token in database
        var authToken = new Entities.AuthToken
        {
            Token = token,
            UserId = user.ID,
            ExpiresUtc = expires,
            LastRefreshed = now
        };
        _db.AuthTokens.Add(authToken);
        await _db.SaveChangesAsync();
        var result = new AuthVerifySuccessDTO
        {
            Token = token,
            ExpiresUtc = expires,
            Roles = roles,
            UserId = user.ID
        };
        return (true, result, errors);
    }

    private static void RemoveCode(string email, CodeEntry entry) =>
        _codes.TryRemove(new KeyValuePair<string, CodeEntry>(email, entry));

    /// <summary>
    /// Drops expired codes and send histories once the dictionaries have grown, so addresses that are never
    /// used again do not stay in memory.
    /// </summary>
    private static void PruneExpiredEntries(DateTime now)
    {
        if (_sendHistory.Count > PruneThreshold)
        {
            foreach (var pair in _sendHistory)
            {
                lock (pair.Value)
                {
                    if (pair.Value.All(d => (now - d) > SendHistoryWindow))
                    {
                        _sendHistory.TryRemove(pair);
                    }
                }
            }
        }

        if (_codes.Count > PruneThreshold)
        {
            foreach (var pair in _codes.Where(pair => now > pair.Value.ExpiresUtc))
            {
                _codes.TryRemove(pair);
            }
        }
    }

    public async Task<bool> InvalidateTokenAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;
        var authToken = await _db.AuthTokens.FirstOrDefaultAsync(t => t.Token == token);
        if (authToken == null)
            return false;
        _db.AuthTokens.Remove(authToken);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<(bool IsValid, int? UserId)> ValidateAndRefreshTokenAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return (false, null);
        var authToken = await _db.AuthTokens.FirstOrDefaultAsync(t => t.Token == token);
        if (authToken == null)
            return (false, null);
        var now = _timeProvider.UtcNow;
        if (now > authToken.ExpiresUtc)
        {
            _db.AuthTokens.Remove(authToken);
            await _db.SaveChangesAsync();
            return (false, null);
        }

        // Always check if user is active
        var user = await _userRepository.GetByIdAsync(authToken.UserId);
        if (user == null || !user.IsActive)
        {
            // Invalidate token if user is inactive or missing
            _db.AuthTokens.Remove(authToken);
            await _db.SaveChangesAsync();
            return (false, null);
        }

        // Always update LastActive on any token validation (app open/access)
        user.LastActive = now;
        // Only update LastLogin if it's been more than the refresh interval since last update
        if (now - authToken.LastRefreshed > LastLoginRefreshInterval)
        {
            user.LastLogin = now;
            authToken.LastRefreshed = now;
        }
        await _userRepository.UpdateAsync(user);
        await _db.SaveChangesAsync();
        return (true, authToken.UserId);
    }

    private static string GenerateNumericCode(int length)
    {
        // GetInt32 gives every digit the same chance; a byte modulo 10 favours 0 to 5.
        var sb = new StringBuilder(length);
        for (var i = 0; i < length; i++)
        {
            sb.Append(RandomNumberGenerator.GetInt32(10));
        }
        return sb.ToString();
    }

    private static string Hash(string value)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }

    private static bool HashesEqual(string a, string b) => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
