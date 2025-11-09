namespace Web.Server.DTOs;

public class AuthVerifySuccessDTO
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresUtc { get; set; }
}
