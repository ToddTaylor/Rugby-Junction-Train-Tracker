using Microsoft.AspNetCore.Mvc;
using Web.Server.DTOs;
using Web.Server.Services;

namespace Web.Server.Controllers.v1;

[Route("api/v1/auth")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUserService _userService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, IUserService userService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _userService = userService;
        _logger = logger;
    }

    /// <summary>
    /// Sends a verification code to the provided email address.
    /// </summary>
    [HttpPost("send-code")]
    public async Task<IActionResult> SendCode([FromBody] SendCodeRequestDTO request)
    {
        try
        {
            var user = await _userService.GetUserByEmailAsync(request.Email);
            if (user == null || !user.IsActive)
            {
                return BadRequest(new { success = false, errors = new List<string> { "User does not exist or is not active." } });
            }

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
