namespace Web.Server.Services;

public interface IEmailService
{
    Task<(bool Success, List<string> Errors)> SendVerificationCodeAsync(string toEmail, string code);
}
