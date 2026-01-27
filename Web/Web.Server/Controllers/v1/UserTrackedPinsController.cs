using Microsoft.AspNetCore.Mvc;
using Web.Server.DTOs;
using Web.Server.Services;

namespace Web.Server.Controllers.v1
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class UserTrackedPinsController : ControllerBase
    {
        private readonly IUserTrackedPinService _userTrackedPinService;
        private readonly ILogger<UserTrackedPinsController> _logger;

        public UserTrackedPinsController(
            IUserTrackedPinService userTrackedPinService,
            ILogger<UserTrackedPinsController> logger)
        {
            _userTrackedPinService = userTrackedPinService;
            _logger = logger;
        }

        /// <summary>
        /// Updates the symbol for a tracked pin by unique tracked pin ID.
        /// </summary>
        [HttpPatch("{trackedPinId}/symbol")]
        public async Task<ActionResult<MessageEnvelope<object>>> UpdateTrackedPinSymbol(int trackedPinId, [FromBody] UpdateTrackedPinSymbolRequestDTO request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (!userId.HasValue)
                {
                    return BadRequest(new MessageEnvelope<object>(null!, new List<string> { "User not authenticated." }));
                }
                await _userTrackedPinService.UpdateSymbolAsync(userId.Value, trackedPinId, request.Symbol);
                return Ok(new MessageEnvelope<object>(new { success = true }, new List<string>()));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating tracked pin symbol");
                return StatusCode(500, new MessageEnvelope<object>(null!, new List<string> { "An error occurred while updating the symbol." }));
            }
        }

        /// <summary>
        /// Updates the last known beacon for a tracked pin by unique tracked pin ID.
        /// </summary>
        [HttpPatch("{trackedPinId}/location")]
        public async Task<ActionResult<MessageEnvelope<object>>> UpdateTrackedPinLocation(int trackedPinId, [FromBody] UpdateTrackedPinLocationRequestDTO request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (!userId.HasValue)
                {
                    return BadRequest(new MessageEnvelope<object>(null!, new List<string> { "User not authenticated." }));
                }
                await _userTrackedPinService.UpdateLocationAsync(userId.Value, trackedPinId, request.BeaconID, request.SubdivisionID, request.BeaconName);
                return Ok(new MessageEnvelope<object>(new { success = true }, new List<string>()));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating tracked pin location");
                return StatusCode(500, new MessageEnvelope<object>(null!, new List<string> { "An error occurred while updating the location." }));
            }
        }

        /// <summary>
        /// Removes a tracked pin by unique tracked pin ID.
        /// </summary>
        [HttpDelete("{trackedPinId}")]
        public async Task<ActionResult<MessageEnvelope<object>>> RemoveTrackedPin(int trackedPinId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (!userId.HasValue)
                {
                    return BadRequest(new MessageEnvelope<object>(null!, new List<string> { "User not authenticated." }));
                }
                await _userTrackedPinService.DeleteAsync(userId.Value, trackedPinId);
                return Ok(new MessageEnvelope<object>(new { success = true }, new List<string>()));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting tracked pin");
                return StatusCode(500, new MessageEnvelope<object>(null!, new List<string> { "An error occurred while deleting the tracked pin." }));
            }
        }


        /// <summary>
        /// Gets all tracked pins for the current user.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<MessageEnvelope<IEnumerable<UserTrackedPinDTO>>>> GetUserTrackedPins()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (!userId.HasValue)
                {
                    return BadRequest(new MessageEnvelope<IEnumerable<UserTrackedPinDTO>>(null!, new List<string> { "User not authenticated." }));
                }

                var trackedPins = await _userTrackedPinService.GetByUserIdAsync(userId.Value);
                return Ok(new MessageEnvelope<IEnumerable<UserTrackedPinDTO>>(trackedPins, new List<string>()));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching tracked pins for user");
                return StatusCode(500, new MessageEnvelope<IEnumerable<UserTrackedPinDTO>>(null!, new List<string> { "An error occurred while fetching tracked pins." }));
            }
        }

        /// <summary>
        /// Adds a tracked pin for the current user.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<MessageEnvelope<UserTrackedPinDTO>>> AddTrackedPin([FromBody] AddTrackedPinRequestDTO request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (!userId.HasValue)
                {
                    return BadRequest(new MessageEnvelope<UserTrackedPinDTO>(null!, new List<string> { "User not authenticated." }));
                }

                if (request.MapPinId <= 0 || string.IsNullOrWhiteSpace(request.Color))
                {
                    return BadRequest(new MessageEnvelope<UserTrackedPinDTO>(null!, new List<string> { "MapPinId and Color are required." }));
                }

                var trackedPin = await _userTrackedPinService.AddAsync(
                    userId.Value,
                    request.MapPinId,
                    request.BeaconID,
                    request.SubdivisionID,
                    request.BeaconName,
                    request.Symbol,
                    request.Color);

                return CreatedAtAction(nameof(GetUserTrackedPins), new { userId = userId.Value },
                    new MessageEnvelope<UserTrackedPinDTO>(trackedPin, new List<string>()));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding tracked pin");
                return StatusCode(500, new MessageEnvelope<UserTrackedPinDTO>(null!, new List<string> { "An error occurred while adding the tracked pin." }));
            }
        }

        /// <summary>
        /// Updates the symbol for a tracked pin.
        /// </summary>

        private int? GetCurrentUserId()
        {
            if (HttpContext.Items.TryGetValue("UserId", out var userIdObj) && userIdObj is int userId)
            {
                return userId;
            }
            return null;
        }
    }
}
