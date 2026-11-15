using Microsoft.AspNetCore.Mvc;
using Web.Server.DTOs;
using Web.Server.Services;

namespace Web.Server.Controllers.v1;

[Route("api/v1/auth")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Tells the web client whether its sign-in token is still good. AuthTokenMiddleware answers 401 before this
    /// runs when the token is invalid, expired or belongs to an inactive user, which signs the client out.
    /// </summary>
    [HttpGet("session")]
    public IActionResult GetSession()
    {
        return Ok(new { authenticated = HttpContext.Items.ContainsKey("UserId") });
    }

    /// <summary>
    /// Logs out the user by invalidating the token.
    /// </summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] string token)
    {
        try
        {
            var success = await _authService.InvalidateTokenAsync(token);
            if (success)
            {
                return Ok(new { success = true });
            }
            return BadRequest(new { success = false, errors = new List<string> { "Invalid token or already logged out." } });
        }
        catch (Exception ex)
        {
            // The token is a credential, so it is kept out of the log.
            _logger.LogError(ex, "An error occurred while logging out.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, errors = new List<string> { "An error occurred while logging out." } });
        }
    }

    /// <summary>
    /// Sends a verification code to the provided email address. The answer is the same whether or not the
    /// address has an account, so it cannot be used to find out who is registered.
    /// </summary>
    [HttpPost("send-code")]
    public async Task<IActionResult> SendCode([FromBody] SendCodeRequestDTO request)
    {
        try
        {
            var (success, errors) = await _authService.SendCodeAsync(request.Email);
            if (success)
            {
                return Ok(new { success });
            }
            return BadRequest(new { success, errors });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending verification code to {Email}", request.Email);
            return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, errors = new List<string> { "An error occurred while sending the verification code." } });
        }
    }

    /// <summary>
    /// Verifies a previously sent code and returns an auth token.
    /// </summary>
    [HttpPost("verify-code")]
    public async Task<IActionResult> VerifyCode([FromBody] VerifyCodeRequestDTO request)
    {
        try
        {
            var (success, result, errors) = await _authService.VerifyCodeAsync(request.Email, request.Code, request.Remember);
            if (success && result != null)
            {
                return Ok(new { success, token = result.Token, expiresUtc = result.ExpiresUtc, roles = result.Roles, userId = result.UserId });
            }
            return BadRequest(new { success, errors });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while verifying code for {Email}", request.Email);
            return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, errors = new List<string> { "An error occurred while verifying the code." } });
        }
    }
}
