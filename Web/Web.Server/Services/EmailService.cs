using Mailtrap;
using Mailtrap.Core.Validation;
using Mailtrap.Emails.Requests;
using Mailtrap.Emails.Responses;

namespace Web.Server.Services;

public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly string _apiToken;

    public EmailService(ILogger<EmailService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _apiToken = configuration["Mailtrap:ApiToken"]
            ?? throw new InvalidOperationException("Mailtrap:ApiToken configuration is required but was not found.");
    }

    public async Task<(bool Success, List<string> Errors)> SendVerificationCodeAsync(string toEmail, string code)
    {
        var errors = new List<string>();

        try
        {
            using var mailtrapClientFactory = new MailtrapClientFactory(_apiToken);
            IMailtrapClient mailtrapClient = mailtrapClientFactory.CreateClient();

            var request = SendEmailRequest
                .Create()
                .From("admin@rugbyjunction.us", "Rugby Junction")
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
                _logger.LogError("Mailtrap send failed. Response: {@Response}", response);
                errors.Add("Failed to send verification email. Please try again later.");
                return (false, errors);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send verification code email to {Email}", toEmail);
            errors.Add("Failed to send verification email. Please try again later.");
            return (false, errors);
        }

        return (true, errors);
    }
}
