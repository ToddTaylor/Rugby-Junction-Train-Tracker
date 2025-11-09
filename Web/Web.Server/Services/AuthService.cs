using Mailtrap;
using Mailtrap.Core.Validation;
using Mailtrap.Emails.Requests;
using Mailtrap.Emails.Responses;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Web.Server.DTOs;
using Web.Server.Providers;

namespace Web.Server.Services;

public class AuthService : IAuthService
{
    private class CodeEntry
    {
        public string CodeHash { get; set; } = string.Empty;
        public DateTime ExpiresUtc { get; set; }
    }

    private readonly ITimeProvider _timeProvider;
    private readonly ILogger<AuthService> _logger;

    // email -> CodeEntry
    private static readonly ConcurrentDictionary<string, CodeEntry> _codes = new(StringComparer.OrdinalIgnoreCase);
    // email -> list of send timestamps (UTC)
    private static readonly ConcurrentDictionary<string, List<DateTime>> _sendHistory = new(StringComparer.OrdinalIgnoreCase);

    public const int CodeLength = 6;
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan ShortSessionLifetime = TimeSpan.FromHours(12);
    public static readonly TimeSpan LongSessionLifetime = TimeSpan.FromDays(365);
    public const int MaxSendsPerHour = 5;

    public AuthService(ITimeProvider timeProvider, ILogger<AuthService> logger)
    {
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<(bool Success, List<string> Errors)> SendCodeAsync(string email)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(email))
        {
            errors.Add("Email is required.");
            return (false, errors);
        }

        var now = _timeProvider.UtcNow;
        var history = _sendHistory.GetOrAdd(email, _ => new List<DateTime>());
        lock (history)
        {
            // remove entries older than 1 hour
            history.RemoveAll(d => (now - d) > TimeSpan.FromHours(1));
            if (history.Count >= MaxSendsPerHour)
            {
                errors.Add("Rate limit exceeded. Try again later.");
                return (false, errors);
            }
            history.Add(now);
        }

        var code = GenerateNumericCode(CodeLength);
        var codeHash = Hash(code);
        _codes[email] = new CodeEntry { CodeHash = codeHash, ExpiresUtc = now.Add(CodeLifetime) };

        try
        {
            var apiToken = "9e7b1356251970a4b0e621401ddea303";
            using var mailtrapClientFactory = new MailtrapClientFactory(apiToken);
            IMailtrapClient mailtrapClient = mailtrapClientFactory.CreateClient();

            var request = SendEmailRequest
                .Create()
                .From("admin@rugbyjunction.us", "Rugby Junction")
                .To(email)
                .Subject("Your Rugby Junction Train Tracker Verification Code")
                .Text($"Your verification code is: {code}")
                .Html($@"<html>
                            <body>
                                <p>Your verification code is: <strong>{code}</strong></p>
                            </body>
                        </html>");

            ValidationResult validationResult = request.Validate();
            if (!validationResult.IsValid)
            {
                foreach (var err in validationResult.Errors)
                    errors.Add(err);
                return (false, errors);
            }

            SendEmailResponse? response = await mailtrapClient.Email().Send(request);

            if (response is null || !response.Success)
            {
                _logger.LogError("Mailtrap send failed. Response: {@Response}", response);
                errors.Add("Failed to send verification email. Please try again later.");
                return (false, errors);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send verification code email to {Email}", email);
            errors.Add("Failed to send verification email. Please try again later.");
            return (false, errors);
        }

        return (true, errors);
    }

    public Task<(bool Success, AuthVerifySuccessDTO? Result, List<string> Errors)> VerifyCodeAsync(string email, string code, bool remember)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code))
        {
            errors.Add("Email and code are required.");
            return Task.FromResult<(bool, AuthVerifySuccessDTO?, List<string>)>((false, null, errors));
        }

        if (!_codes.TryGetValue(email, out var entry))
        {
            errors.Add("Code not found. Please request a new code.");
            return Task.FromResult<(bool, AuthVerifySuccessDTO?, List<string>)>((false, null, errors));
        }

        var now = _timeProvider.UtcNow;
        if (now > entry.ExpiresUtc)
        {
            errors.Add("Code expired. Please request a new code.");
            _codes.TryRemove(email, out _); // cleanup
            return Task.FromResult<(bool, AuthVerifySuccessDTO?, List<string>)>((false, null, errors));
        }

        if (!HashesEqual(entry.CodeHash, Hash(code)))
        {
            errors.Add("Invalid code.");
            return Task.FromResult<(bool, AuthVerifySuccessDTO?, List<string>)>((false, null, errors));
        }

        // success: generate token
        var token = Guid.NewGuid().ToString("N");
        var expires = now.Add(remember ? LongSessionLifetime : ShortSessionLifetime);
        var result = new AuthVerifySuccessDTO { Token = token, ExpiresUtc = expires };

        // Optionally remove the code to prevent reuse
        _codes.TryRemove(email, out _);

        return Task.FromResult<(bool, AuthVerifySuccessDTO?, List<string>)>((true, result, errors));
    }

    private static string GenerateNumericCode(int length)
    {
        var bytes = RandomNumberGenerator.GetBytes(length);
        var sb = new StringBuilder(length);
        foreach (var b in bytes)
        {
            sb.Append((b % 10).ToString());
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
