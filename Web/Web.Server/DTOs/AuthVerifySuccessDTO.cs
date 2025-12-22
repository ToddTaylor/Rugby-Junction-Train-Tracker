namespace Web.Server.DTOs;

public class AuthVerifySuccessDTO
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresUtc { get; set; }
    public List<string> Roles { get; set; } = new();
    public int UserId { get; set; }
}
