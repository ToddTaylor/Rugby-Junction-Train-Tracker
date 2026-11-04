using MapsterMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Web.Server.DTOs;
using Web.Server.Entities;
using Web.Server.Services;

namespace Web.Server.Controllers.v1
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class BeaconsController : ControllerBase
    {
        private readonly IBeaconService _beaconService;
        /// <summary>
        /// Header a beacon PC sends its device key in.
        /// </summary>
        public const string DeviceKeyHeaderName = "X-Beacon-Key";

        private readonly IServerHealthService _serverHealthService;
        private readonly IServerCommandService _serverCommandService;
        private readonly IUserService _userService;
        private readonly ILogger<BeaconsController> _logger;
        private readonly IMapper _mapper;

        public BeaconsController(
            IBeaconService beaconService,
            IServerHealthService serverHealthService,
            IServerCommandService serverCommandService,
            IUserService userService,
            ILogger<BeaconsController> logger,
            IMapper mapper)
        {
            _beaconService = beaconService;
            _serverHealthService = serverHealthService;
            _serverCommandService = serverCommandService;
            _userService = userService;
            _logger = logger;
            _mapper = mapper;
        }

        // GET: api/v1/Beacons
        [HttpGet]
        public async Task<ActionResult> GetBeacons()
        {
            var response = new MessageEnvelope<IEnumerable<BeaconDTO>>(null, []);
            try
            {
                var beacons = await _beaconService.GetBeaconsAsync();
                response.Data = _mapper.Map<IEnumerable<BeaconDTO>>(beacons);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching beacons.");
                response.Errors.Add(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, response);
            }
        }

        // GET: api/v1/Beacons/5
        [HttpGet("{id}")]
        public async Task<ActionResult<BeaconDTO>> GetBeacon(int id)
        {
            var response = new MessageEnvelope<RailroadDTO>(null, []);
            try
            {
                var beacon = await _beaconService.GetBeaconByIdAsync(id);

                if (beacon == null)
                {
                    return NotFound();
                }

                var beaconDTO = _mapper.Map<BeaconDTO>(beacon);

                return Ok(beaconDTO);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching the beacon.");
                response.Errors.Add(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, response);
            }
        }

        // PUT: api/v1/Beacons/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutBeacon(int id, UpdateBeaconDTO updateBeaconDTO)
        {
            var response = new MessageEnvelope<BeaconDTO>(null, []);
            try
            {
                if (!await IsAdminAsync())
                {
                    return StatusCode(StatusCodes.Status403Forbidden);
                }

                if (id != updateBeaconDTO.ID)
                {
                    return BadRequest();
                }

                var beacon = _mapper.Map<Beacon>(updateBeaconDTO);

                Beacon updatedBeacon;
                try
                {
                    updatedBeacon = await _beaconService.UpdateBeaconAsync(id, beacon);
                }
                catch (KeyNotFoundException)
                {
                    return NotFound();
                }

                response.Data = _mapper.Map<BeaconDTO>(updatedBeacon);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while updating the beacon.");
                response.Errors.Add(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, response);
            }
        }

        // POST: api/v1/Beacons
        [HttpPost]
        public async Task<ActionResult> PostBeacon(CreateBeaconDTO createBeaconDTO)
        {
            var response = new MessageEnvelope<BeaconDTO>(null, []);
            try
            {
                if (!await IsAdminAsync())
                {
                    return StatusCode(StatusCodes.Status403Forbidden);
                }

                var beacon = _mapper.Map<Beacon>(createBeaconDTO);
                var createdBeacon = await _beaconService.CreateBeaconAsync(beacon);
                response.Data = _mapper.Map<BeaconDTO>(createdBeacon);
                return CreatedAtAction("GetBeacon", new { id = response.Data.ID }, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while creating the beacon.");
                response.Errors.Add(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, response);
            }
        }

        // POST: api/v1/Beacons/Health/{id}
        /// <summary>
        /// Beacon PC health check. Current ConsoleApp versions send a diagnostics report in the body;
        /// older versions send an empty body, which only records the check-in.
        /// Once the beacon has a device key, the PC must send it in X-Beacon-Key or the check-in is refused (401).
        /// With a valid key, the response is 200 { command } when a reboot or restart-apps command is waiting, else 204.
        /// </summary>
        [HttpPost("Health/{id}")]
        public async Task<IActionResult> CheckHealth(int id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] BeaconDiagnosticsReportDTO? report = null)
        {
            try
            {
                if (id == 0)
                {
                    return BadRequest("A beacon ID is required.");
                }

                var beacon = await _beaconService.GetBeaconByIdAsync(id);

                if (beacon == null)
                {
                    return NotFound();
                }

                var keyCheck = await _serverCommandService.CheckDeviceKeyAsync(id, Request.Headers[DeviceKeyHeaderName].FirstOrDefault());
                if (keyCheck == DeviceKeyCheck.Invalid)
                {
                    _logger.LogWarning("Health check for beacon {BeaconId} refused: missing or wrong device key.", id);
                    return Unauthorized(new { error = "Missing or wrong device key." });
                }

                await _beaconService.UpdateBeaconHealthAsync(id, beacon);

                BeaconDiagnostics? diagnostics = null;
                if (report != null)
                {
                    diagnostics = _mapper.Map<BeaconDiagnostics>(report);
                    await _serverHealthService.RecordDiagnosticsAsync(id, diagnostics);
                }

                if (keyCheck == DeviceKeyCheck.Valid)
                {
                    var command = await _serverCommandService.HandleCheckInAsync(id, diagnostics);
                    if (command != null)
                    {
                        return Ok(new BeaconHealthResponseDTO { Command = command });
                    }
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while checking health for beacon {BeaconId}.", id);
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
            }
        }

        // POST: api/v1/Beacons/Health/{id}/commands/{commandId}
        /// <summary>
        /// The beacon PC reports on a command it received: Started before carrying it out, then Succeeded or Failed,
        /// or Rejected when it refuses. Requires the beacon's device key. A 409 on Started tells the PC not to run it.
        /// </summary>
        [HttpPost("Health/{id}/commands/{commandId:int}")]
        public async Task<IActionResult> ReportCommand(int id, int commandId, BeaconCommandReportDTO report)
        {
            try
            {
                var keyCheck = await _serverCommandService.CheckDeviceKeyAsync(id, Request.Headers[DeviceKeyHeaderName].FirstOrDefault());
                if (keyCheck != DeviceKeyCheck.Valid)
                {
                    _logger.LogWarning("Command report for beacon {BeaconId} refused: missing or wrong device key.", id);
                    return Unauthorized(new { error = "Missing or wrong device key." });
                }

                return await _serverCommandService.ReportCommandAsync(id, commandId, report) switch
                {
                    CommandChangeResult.Updated => NoContent(),
                    CommandChangeResult.NotFound => NotFound(),
                    _ => Conflict(new { error = "The command was cancelled, expired or already handled." }),
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while recording a command report for beacon {BeaconId}.", id);
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
            }
        }

        // DELETE: api/v1/Beacons/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBeacon(int id)
        {
            try
            {
                if (!await IsAdminAsync())
                {
                    return StatusCode(StatusCodes.Status403Forbidden);
                }

                var success = await _beaconService.DeleteBeaconAsync(id);
                if (!success)
                {
                    return NotFound();
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while deleting beacon {BeaconId}.", id);
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
            }
        }

        private async Task<bool> IsAdminAsync()
        {
            if (!HttpContext.Items.TryGetValue("UserId", out var userIdObj) || userIdObj is not int userId)
            {
                return false;
            }

            var user = await _userService.GetUserByIdAsync(userId);
            return user?.UserRoles?.Any(ur =>
                string.Equals(ur.Role?.RoleName, "Admin", StringComparison.OrdinalIgnoreCase)) == true;
        }
    }
}

