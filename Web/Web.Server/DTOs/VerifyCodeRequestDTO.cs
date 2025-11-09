namespace Web.Server.DTOs;

public class VerifyCodeRequestDTO
{
    public string Email { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool Remember { get; set; }
}
