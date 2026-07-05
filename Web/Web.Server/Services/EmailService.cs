using Mailtrap;
using Mailtrap.Core.Validation;
using Mailtrap.Emails.Requests;
using Mailtrap.Emails.Responses;

namespace Web.Server.Services;

public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly string _apiToken;
    private readonly string _fromAddress;
    private readonly bool _isDevelopment;
    private readonly bool _canUseMailtrap;

    public EmailService(ILogger<EmailService> logger, IConfiguration configuration, IWebHostEnvironment environment)
    {
        _logger = logger;
        _apiToken = configuration["Mailtrap:ApiToken"] ?? string.Empty;
        _fromAddress = configuration["Mailtrap:FromAddress"] ?? string.Empty;
        _isDevelopment = environment.IsDevelopment();
        _canUseMailtrap =
            !string.IsNullOrWhiteSpace(_apiToken) &&
            !string.IsNullOrWhiteSpace(_fromAddress) &&
            !_apiToken.StartsWith("PLACEHOLDER_", StringComparison.OrdinalIgnoreCase) &&
            !_fromAddress.StartsWith("PLACEHOLDER_", StringComparison.OrdinalIgnoreCase);

        if (!_canUseMailtrap && !_isDevelopment)
        {
            throw new InvalidOperationException("Mailtrap configuration is required in non-development environments.");
        }

        if (!_canUseMailtrap && _isDevelopment)
        {
            _logger.LogWarning("Mailtrap is not configured in Development. Verification codes will be logged locally.");
        }
    }

    public async Task<(bool Success, List<string> Errors)> SendVerificationCodeAsync(string toEmail, string code)
    {
        var errors = new List<string>();

        if (!_canUseMailtrap && _isDevelopment)
        {
            _logger.LogWarning("DEV LOGIN CODE for {Email}: {Code}", toEmail, code);
            return (true, errors);
        }

        try
        {
            using var mailtrapClientFactory = new MailtrapClientFactory(_apiToken);
            IMailtrapClient mailtrapClient = mailtrapClientFactory.CreateClient();

            var request = SendEmailRequest
                .Create()
                .From(_fromAddress, "Rugby Junction")
                .To(toEmail)
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
                _logger.LogError("Mailtrap send failed from {FromAddress}. Response: {@Response}", _fromAddress, response);
                if (_isDevelopment)
                {
                    _logger.LogWarning("DEV LOGIN CODE for {Email}: {Code}", toEmail, code);
                    return (true, errors);
                }
                errors.Add("Failed to send verification email. Please try again later.");
                return (false, errors);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send verification code email from {FromAddress} to {Email}", _fromAddress, toEmail);
            if (_isDevelopment)
            {
                _logger.LogWarning("DEV LOGIN CODE for {Email}: {Code}", toEmail, code);
                return (true, errors);
            }
            errors.Add("Failed to send verification email. Please try again later.");
            return (false, errors);
        }

        return (true, errors);
    }
}
