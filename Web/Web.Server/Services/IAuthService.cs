using Web.Server.DTOs;

namespace Web.Server.Services;

public interface IAuthService
{
    Task<(bool Success, List<string> Errors)> SendCodeAsync(string email);
    Task<(bool Success, AuthVerifySuccessDTO? Result, List<string> Errors)> VerifyCodeAsync(string email, string code, bool remember);
    Task<(bool IsValid, int? UserId)> ValidateAndRefreshTokenAsync(string token);
    Task<bool> InvalidateTokenAsync(string token);
}
