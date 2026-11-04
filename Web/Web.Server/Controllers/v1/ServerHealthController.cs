using Microsoft.AspNetCore.Mvc;
using Web.Server.DTOs;
using Web.Server.Entities;
using Web.Server.Services;

namespace Web.Server.Controllers.v1
{
    /// <summary>
    /// Server Health dashboard: status and diagnostics of each beacon PC. Admins see every beacon;
    /// Custodians see beacons on the subdivisions they look after.
    /// </summary>
    [Route("api/v1/[controller]")]
    [ApiController]
    public class ServerHealthController : ControllerBase
    {
        public const int MaxExpectedProgramCount = 20;

        private readonly IServerHealthService _serverHealthService;
        private readonly IUserService _userService;
        private readonly ILogger<ServerHealthController> _logger;

        public ServerHealthController(IServerHealthService serverHealthService, IUserService userService, ILogger<ServerHealthController> logger)
        {
            _serverHealthService = serverHealthService;
            _userService = userService;
            _logger = logger;
        }

        // GET: api/v1/ServerHealth
        [HttpGet]
        public async Task<ActionResult> GetServerHealth()
        {
            var response = new MessageEnvelope<IReadOnlyList<ServerHealthDTO>>([], []);
            try
            {
                var currentUser = await GetCurrentUserAsync();
                if (currentUser == null)
                {
                    return Unauthorized();
                }

                if (!HasRole(currentUser, "Admin") && !HasRole(currentUser, "Custodian"))
                {
                    response.Errors.Add("Forbidden.");
                    return StatusCode(StatusCodes.Status403Forbidden, response);
                }

                response.Data = await _serverHealthService.GetServerHealthAsync(currentUser);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching server health.");
                response.Errors.Add(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, response);
            }
        }

        // PUT: api/v1/ServerHealth/5/expectations
        [HttpPut("{beaconId:int}/expectations")]
        public async Task<IActionResult> UpdateExpectations(int beaconId, UpdateServerExpectationsDTO dto)
        {
            var response = new MessageEnvelope<object?>(null, []);
            try
            {
                var currentUser = await GetCurrentUserAsync();
                if (currentUser == null)
                {
                    return Unauthorized();
                }

                if (!await _serverHealthService.CanManageBeaconAsync(currentUser, beaconId))
                {
                    response.Errors.Add("Forbidden.");
                    return StatusCode(StatusCodes.Status403Forbidden, response);
                }

                var counts = new[] { dto.ExpectedSdrSharpCount, dto.ExpectedSoftEotCount, dto.ExpectedSoftDpuCount };
                if (counts.Any(c => c is < 0 or > MaxExpectedProgramCount))
                {
                    response.Errors.Add($"Expected program counts must be between 0 and {MaxExpectedProgramCount}, or empty to skip the check.");
                    return BadRequest(response);
                }

                if (!await _serverHealthService.UpdateExpectedProgramsAsync(beaconId, dto))
                {
                    return NotFound();
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while updating expected programs for beacon {BeaconId}.", beaconId);
                response.Errors.Add(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, response);
            }
        }

        private async Task<User?> GetCurrentUserAsync()
        {
            if (!HttpContext.Items.TryGetValue("UserId", out var userIdObj) || userIdObj is not int userId)
            {
                return null;
            }

            return await _userService.GetUserByIdAsync(userId);
        }

        private static bool HasRole(User user, string roleName)
        {
            return user.UserRoles?.Any(ur =>
                string.Equals(ur.Role?.RoleName, roleName, StringComparison.OrdinalIgnoreCase)) == true;
        }
    }
}
