using Microsoft.AspNetCore.Mvc;
using Web.Server.Models;
using Web.Server.Services;

namespace Web.Server.Controllers.v1
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class TelemetriesController : ControllerBase
    {
        private readonly ITelemetryService _telemetryService;
        private readonly ILogger<TelemetriesController> _logger;

        public TelemetriesController(ILogger<TelemetriesController> logger, ITelemetryService telemetryService)
        {
            _logger = logger;
            _telemetryService = telemetryService;
        }

        [HttpPost("Telemetry")]
        public IActionResult Post([FromBody] Telemetry telemetry)
        {
            try
            {
                _telemetryService.ProcessTelemetry(telemetry);

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