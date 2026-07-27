using MapsterMapper;
using Microsoft.AspNetCore.Mvc;
using Web.Server.DTOs;
using Web.Server.Entities;
using Web.Server.Providers;
using Web.Server.Services;

namespace Web.Server.Controllers.v1
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class BeaconRailroadsController : ControllerBase
    {
        private readonly IBeaconRailroadService _service;
        private readonly IUserService _userService;
        private readonly ITimeProvider _timeProvider;
        private readonly ILogger<BeaconRailroadsController> _logger;
        private readonly IMapper _mapper;

        public BeaconRailroadsController(
            IBeaconRailroadService service,
            IUserService userService,
            ITimeProvider timeProvider,
            ILogger<BeaconRailroadsController> logger,
            IMapper mapper)
        {
            _service = service;
            _userService = userService;
            _timeProvider = timeProvider;
            _logger = logger;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult> GetAll()
        {
            var response = new MessageEnvelope<IEnumerable<BeaconRailroadDTO>>(null, []);
            try
            {
                var beaconRailroads = await _service.GetAllAsync();
                var beaconRailroadDTOs = _mapper.Map<IEnumerable<BeaconRailroadDTO>>(beaconRailroads).ToList();
                var utcNow = _timeProvider.UtcNow;
                foreach (var dto in beaconRailroadDTOs)
                {
                    dto.Online = !BeaconRailroadHealthService.IsOffline(dto.LastUpdate, utcNow);
                }
                response.Data = beaconRailroadDTOs;
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching all beacon railroads.");
                response.Errors.Add(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, response);
            }
        }

        [HttpGet("{beaconId:int}/{subdivisionId:int}")]
        public async Task<ActionResult<BeaconRailroadDTO>> GetById(int beaconId, int subdivisionId)
        {
            var response = new MessageEnvelope<BeaconRailroadDTO>(null, []);
            try
            {
                var beaconRailroad = await _service.GetByIdAsync(beaconId, subdivisionId);
                if (beaconRailroad == null)
                {
                    return NotFound();
                }

                var beaconRailroadDTO = _mapper.Map<BeaconRailroadDTO>(beaconRailroad);
                beaconRailroadDTO.Online = !BeaconRailroadHealthService.IsOffline(beaconRailroadDTO.LastUpdate, _timeProvider.UtcNow);
                response.Data = beaconRailroadDTO;

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching the beacon railroad.");
                response.Errors.Add(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, response);
            }
        }

        [HttpPost]
        public async Task<ActionResult> Create(CreateBeaconRailroadDTO dto)
        {
            var response = new MessageEnvelope<BeaconRailroadDTO>(null, []);
            try
            {
                if (!await IsAdminAsync())
                {
                    response.Errors.Add("Forbidden.");
                    return StatusCode(StatusCodes.Status403Forbidden, response);
                }

                if (dto.TelemetryStaleHoursOverride.HasValue && dto.TelemetryStaleHoursOverride.Value <= 0)
                {
                    response.Errors.Add("TelemetryStaleHoursOverride must be a whole integer greater than zero when provided.");
                    return BadRequest(response);
                }

                var beaconRailroad = _mapper.Map<Entities.BeaconRailroad>(dto);

                var created = await _service.AddAsync(beaconRailroad);
                response.Data = _mapper.Map<BeaconRailroadDTO>(created);
                return CreatedAtAction(nameof(GetById), new { beaconId = response.Data.BeaconID, subdivisionId = response.Data.SubdivisionID }, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while creating a beacon railroad.");
                response.Errors.Add(ex.Message);
                if (ex.InnerException != null)
                { 
                    response.Errors.Add(ex.InnerException.Message); 
                }

                return StatusCode(StatusCodes.Status500InternalServerError, response);
            }
        }

        [HttpPut("{beaconId:int}/{subdivisionId:int}")]
        public async Task<IActionResult> Update(int beaconId, int subdivisionId, UpdateBeaconRailroadDTO dto)
        {
            var response = new MessageEnvelope<BeaconRailroadDTO>(null, []);

            try
            {
                if (beaconId != dto.BeaconID || subdivisionId != dto.SubdivisionID)
                {
                    response.Errors.Add("BeaconID and SubdivisionID in the URL must match the DTO.");
                    return BadRequest(response);
                }

                if (dto.TelemetryStaleHoursOverride.HasValue && dto.TelemetryStaleHoursOverride.Value <= 0)
                {
                    response.Errors.Add("TelemetryStaleHoursOverride must be a whole integer greater than zero when provided.");
                    return BadRequest(response);
                }

                var currentUser = await GetCurrentUserAsync();
                if (currentUser == null)
                {
                    response.Errors.Add("Forbidden.");
                    return StatusCode(StatusCodes.Status403Forbidden, response);
                }

                var existingBeaconRailroad = await _service.GetByIdAsync(beaconId, subdivisionId);
                if (existingBeaconRailroad == null)
                {
                    response.Errors.Add("BeaconRailroad not found.");
                    return NotFound(response);
                }

                if (!string.IsNullOrEmpty(dto.OfflineNote))
                {
                    var latestTelemetry = await _service.GetLatestTelemetryTimestampAsync(beaconId, subdivisionId);
                    var isTrulyOffline = BeaconRailroadHealthService.IsTrulyOffline(
                        existingBeaconRailroad.LastUpdate, latestTelemetry, _timeProvider.UtcNow);

                    if (!isTrulyOffline)
                    {
                        response.Errors.Add("Cannot set an offline note on a beacon railroad that is currently online.");
                        return BadRequest(response);
                    }
                }

                var isAdmin = HasRole(currentUser, "Admin");
                Entities.BeaconRailroad beaconRailroad;

                if (isAdmin)
                {
                    beaconRailroad = _mapper.Map<Entities.BeaconRailroad>(dto);
                    beaconRailroad.BeaconID = beaconId;  // Ensure composite key is set
                    beaconRailroad.SubdivisionID = subdivisionId;
                }
                else
                {
                    var isCustodian = HasRole(currentUser, "Custodian");
                    if (!isCustodian || existingBeaconRailroad.Subdivision?.CustodianId != currentUser.ID)
                    {
                        response.Errors.Add("Forbidden.");
                        return StatusCode(StatusCodes.Status403Forbidden, response);
                    }

                    var changedReadOnlyFields =
                        dto.Latitude != existingBeaconRailroad.Latitude ||
                        dto.Longitude != existingBeaconRailroad.Longitude ||
                        dto.Milepost != existingBeaconRailroad.Milepost ||
                        dto.MultipleTracks != existingBeaconRailroad.MultipleTracks ||
                        dto.Direction != existingBeaconRailroad.Direction ||
                        dto.TelemetryStaleHoursOverride != existingBeaconRailroad.TelemetryStaleHoursOverride;

                    if (changedReadOnlyFields)
                    {
                        response.Errors.Add("Custodians can only update OfflineNote for their assigned subdivision's beacon railroads.");
                        return StatusCode(StatusCodes.Status403Forbidden, response);
                    }

                    beaconRailroad = new Entities.BeaconRailroad
                    {
                        BeaconID = beaconId,
                        SubdivisionID = subdivisionId,
                        Direction = existingBeaconRailroad.Direction,
                        Latitude = existingBeaconRailroad.Latitude,
                        Longitude = existingBeaconRailroad.Longitude,
                        Milepost = existingBeaconRailroad.Milepost,
                        MultipleTracks = existingBeaconRailroad.MultipleTracks,
                        TelemetryStaleHoursOverride = existingBeaconRailroad.TelemetryStaleHoursOverride,
                        OfflineNote = dto.OfflineNote
                    };
                }

                await _service.UpdateAsync(beaconRailroad);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while updating the beacon railroad.");
                response.Errors.Add(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, response);
            }
        }

        [HttpDelete("{beaconId:int}/{subdivisionId:int}")]
        public async Task<IActionResult> Delete(int beaconId, int subdivisionId)
        {
            try
            {
                if (!await IsAdminAsync())
                {
                    return StatusCode(StatusCodes.Status403Forbidden);
                }

                var deleted = await _service.DeleteAsync(beaconId, subdivisionId);
                if (!deleted)
                {
                    return NotFound();
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while deleting beacon railroad {BeaconId}/{SubdivisionId}.", beaconId, subdivisionId);
                return StatusCode(StatusCodes.Status500InternalServerError, new MessageEnvelope<object>(null, new List<string> { ex.Message }));
            }
        }

        private async Task<bool> IsAdminAsync()
        {
            var user = await GetCurrentUserAsync();
            return user != null && HasRole(user, "Admin");
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
