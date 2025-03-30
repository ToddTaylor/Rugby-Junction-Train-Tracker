using Microsoft.AspNetCore.Mvc;
using Web.Server.Models;
using Web.Server.Services;

namespace Web.Server.Controllers.v1
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class TelemetryController : ControllerBase
    {
        private readonly ITelemetryService _telemetryService;
        private readonly ILogger<TelemetryController> _logger;

        public TelemetryController(ILogger<TelemetryController> logger, ITelemetryService telemetryService)
        {
            _logger = logger;
            _telemetryService = telemetryService;
        }

        [HttpPost("Alert")]
        public IActionResult Post([FromBody] Alert alert)
        {
            try
            {
                _telemetryService.ProcessTelemetry(alert);

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while processing telemetry data.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An recoverable error has occurred.  Please try again later.");
            }
        }
    }
}