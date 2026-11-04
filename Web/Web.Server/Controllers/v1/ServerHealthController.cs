using Microsoft.AspNetCore.Mvc;
using Web.Server.DTOs;
using Web.Server.Entities;
using Web.Server.Filters;
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
        public const int MaxMuteNoteLength = 500;

        private readonly IServerHealthService _serverHealthService;
        private readonly IServerCommandService _serverCommandService;
        private readonly IUserService _userService;
        private readonly ILogger<ServerHealthController> _logger;

        public ServerHealthController(
            IServerHealthService serverHealthService,
            IServerCommandService serverCommandService,
            IUserService userService,
            ILogger<ServerHealthController> logger)
        {
            _serverHealthService = serverHealthService;
            _serverCommandService = serverCommandService;
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

        // PUT: api/v1/ServerHealth/5/mute
        [HttpPut("{beaconId:int}/mute")]
        public async Task<IActionResult> MuteAlerts(int beaconId, MuteServerAlertsDTO dto)
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

                if (dto.MutedUntil.HasValue && dto.MutedUntil.Value.ToUniversalTime() <= DateTime.UtcNow)
                {
                    response.Errors.Add("The mute end time must be in the future.");
                    return BadRequest(response);
                }

                if (dto.Note?.Length > MaxMuteNoteLength)
                {
                    response.Errors.Add($"The note can be at most {MaxMuteNoteLength} characters.");
                    return BadRequest(response);
                }

                if (!await _serverHealthService.MuteAlertsAsync(beaconId, dto, currentUser))
                {
                    return NotFound();
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while muting alerts for beacon {BeaconId}.", beaconId);
                response.Errors.Add(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, response);
            }
        }

        // DELETE: api/v1/ServerHealth/5/mute
        [HttpDelete("{beaconId:int}/mute")]
        public async Task<IActionResult> UnmuteAlerts(int beaconId)
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

                if (!await _serverHealthService.UnmuteAlertsAsync(beaconId))
                {
                    return NotFound();
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while turning alerts back on for beacon {BeaconId}.", beaconId);
                response.Errors.Add(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, response);
            }
        }

        // POST: api/v1/ServerHealth/5/device-key
        /// <summary>
        /// Creates or replaces the beacon's device key and returns it once.
        /// </summary>
        [HttpPost("{beaconId:int}/device-key")]
        [RequireHeaderAuthToken]
        public async Task<IActionResult> CreateDeviceKey(int beaconId)
        {
            var response = new MessageEnvelope<DeviceKeyCreatedDTO?>(null, []);
            try
            {
                var (currentUser, denied) = await AuthorizeManageAsync(beaconId);
                if (denied != null)
                {
                    return denied;
                }

                var key = await _serverCommandService.CreateDeviceKeyAsync(beaconId, currentUser!);
                if (key == null)
                {
                    return NotFound();
                }

                response.Data = new DeviceKeyCreatedDTO { DeviceKey = key };
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while creating a device key for beacon {BeaconId}.", beaconId);
                response.Errors.Add(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, response);
            }
        }

        // DELETE: api/v1/ServerHealth/5/device-key
        [HttpDelete("{beaconId:int}/device-key")]
        [RequireHeaderAuthToken]
        public async Task<IActionResult> RemoveDeviceKey(int beaconId)
        {
            var response = new MessageEnvelope<object?>(null, []);
            try
            {
                var (_, denied) = await AuthorizeManageAsync(beaconId);
                if (denied != null)
                {
                    return denied;
                }

                return await _serverCommandService.RemoveDeviceKeyAsync(beaconId) ? NoContent() : NotFound();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while removing the device key for beacon {BeaconId}.", beaconId);
                response.Errors.Add(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, response);
            }
        }

        // POST: api/v1/ServerHealth/5/commands
        /// <summary>
        /// Asks the beacon PC to reboot or restart its apps. The PC picks the command up with its next health check.
        /// </summary>
        [HttpPost("{beaconId:int}/commands")]
        [RequireHeaderAuthToken]
        public async Task<IActionResult> RequestCommand(int beaconId, RequestBeaconCommandDTO dto)
        {
            var response = new MessageEnvelope<BeaconCommandDTO?>(null, []);
            try
            {
                var (currentUser, denied) = await AuthorizeManageAsync(beaconId);
                if (denied != null)
                {
                    return denied;
                }

                if (!Enum.IsDefined(dto.Type))
                {
                    response.Errors.Add("Unknown command type.");
                    return BadRequest(response);
                }

                var result = await _serverCommandService.RequestCommandAsync(beaconId, dto.Type, currentUser!);
                if (result.Error == CommandRequestError.None)
                {
                    response.Data = result.Command;
                    return StatusCode(StatusCodes.Status201Created, response);
                }

                response.Errors.Add(result.Message ?? "The command could not be sent.");
                return result.Error switch
                {
                    CommandRequestError.BeaconNotFound => NotFound(response),
                    CommandRequestError.DailyLimitReached => StatusCode(StatusCodes.Status429TooManyRequests, response),
                    _ => Conflict(response),
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while requesting a command for beacon {BeaconId}.", beaconId);
                response.Errors.Add(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, response);
            }
        }

        // DELETE: api/v1/ServerHealth/5/commands/12
        /// <summary>
        /// Cancels a command the PC has not picked up and started yet.
        /// </summary>
        [HttpDelete("{beaconId:int}/commands/{commandId:int}")]
        [RequireHeaderAuthToken]
        public async Task<IActionResult> CancelCommand(int beaconId, int commandId)
        {
            var response = new MessageEnvelope<object?>(null, []);
            try
            {
                var (currentUser, denied) = await AuthorizeManageAsync(beaconId);
                if (denied != null)
                {
                    return denied;
                }

                switch (await _serverCommandService.CancelCommandAsync(beaconId, commandId, currentUser!))
                {
                    case CommandChangeResult.Updated:
                        return NoContent();
                    case CommandChangeResult.NotFound:
                        return NotFound();
                    default:
                        response.Errors.Add("The PC has already started this command, or it has ended.");
                        return Conflict(response);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while cancelling command {CommandId} for beacon {BeaconId}.", commandId, beaconId);
                response.Errors.Add(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, response);
            }
        }

        /// <summary>
        /// The signed-in user, or a 401/403 result when nobody is signed in or the user may not manage the beacon.
        /// </summary>
        private async Task<(User? User, IActionResult? Denied)> AuthorizeManageAsync(int beaconId)
        {
            var currentUser = await GetCurrentUserAsync();
            if (currentUser == null)
            {
                return (null, Unauthorized());
            }

            if (!await _serverHealthService.CanManageBeaconAsync(currentUser, beaconId))
            {
                return (currentUser, StatusCode(StatusCodes.Status403Forbidden, new MessageEnvelope<object?>(null, ["Forbidden."])));
            }

            return (currentUser, null);
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
