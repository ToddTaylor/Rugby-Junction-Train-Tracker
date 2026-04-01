using MapsterMapper;
using Microsoft.AspNetCore.Mvc;
using Web.Server.DTOs;
using Web.Server.Services;

namespace Web.Server.Controllers.v1
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class SubdivisionTrackageRightsController : ControllerBase
    {
        private readonly ISubdivisionTrackageRightService _service;
        private readonly ILogger<SubdivisionTrackageRightsController> _logger;

        public SubdivisionTrackageRightsController(
            ISubdivisionTrackageRightService service,
            ILogger<SubdivisionTrackageRightsController> logger)
        {
            _service = service;
            _logger = logger;
        }

        /// <summary>
        /// Gets all trackage rights for a subdivision.
        /// </summary>
        [HttpGet("from/{fromSubdivisionID}")]
        public async Task<ActionResult<MessageEnvelope<IEnumerable<SubdivisionTrackageRightDTO>>>> GetTrackageRights(int fromSubdivisionID)
        {
            try
            {
                var rights = await _service.GetByFromSubdivisionAsync(fromSubdivisionID);
                return Ok(new MessageEnvelope<IEnumerable<SubdivisionTrackageRightDTO>>(rights, new List<string>()));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching trackage rights");
                return StatusCode(500, new MessageEnvelope<IEnumerable<SubdivisionTrackageRightDTO>>(null!, new List<string> { "An error occurred while fetching trackage rights." }));
            }
        }

        /// <summary>
        /// Replaces all trackage rights for a subdivision.
        /// </summary>
        [HttpPost("from/{fromSubdivisionID}")]
        public async Task<ActionResult<MessageEnvelope<object>>> ReplaceTrackageRights(int fromSubdivisionID, [FromBody] int[] toSubdivisionIDs)
        {
            try
            {
                await _service.ReplaceTrackageRightsAsync(fromSubdivisionID, toSubdivisionIDs);
                return Ok(new MessageEnvelope<object>(new { success = true }, new List<string>()));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error replacing trackage rights");
                return StatusCode(500, new MessageEnvelope<object>(null!, new List<string> { "An error occurred while updating trackage rights." }));
            }
        }

        /// <summary>
        /// Deletes a single trackage right.
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult<MessageEnvelope<object>>> DeleteTrackageRight(int id)
        {
            try
            {
                await _service.DeleteAsync(id);
                return Ok(new MessageEnvelope<object>(new { success = true }, new List<string>()));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting trackage right");
                return StatusCode(500, new MessageEnvelope<object>(null!, new List<string> { "An error occurred while deleting the trackage right." }));
            }
        }
    }
}
