using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Web.Server.DTOs;
using Web.Server.Entities;
using Web.Server.Services;

namespace Web.Server.Controllers.v1
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class TelemetriesController : ControllerBase
    {
        private readonly ITelemetryService _telemetryService;
        private readonly ILogger<TelemetriesController> _logger;
        private readonly IMapper _mapper;

        public TelemetriesController(
            ILogger<TelemetriesController> logger,
            IMapper mapper,
            ITelemetryService telemetryService)
        {
            _logger = logger;
            _mapper = mapper;
            _telemetryService = telemetryService;
        }

        // GET: api/v1/Telemetries
        [HttpGet]
        public async Task<ActionResult<IEnumerable<TelemetryDTO>>> GetTelemetries()
        {
            var telemetries = await _telemetryService.GetTelemetries();

            var telemetryDTOs = _mapper.Map<IEnumerable<TelemetryDTO>>(telemetries);

            return Ok(telemetryDTOs);
        }

        // POST: api/v1/Telemetries
        [HttpPost("Telemetry")]
        public IActionResult Post([FromBody] CreateTelemetryDTO telemetryDTO)
        {
            var telemetry = this._mapper.Map<Telemetry>(telemetryDTO);

            try
            {
                _telemetryService.CreateTelemetry(telemetry);

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