using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Web.Server.DTOs;
using Web.Server.Entities;
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
        public async Task<ActionResult> GetTelemetries()
        {
            var response = new MessageEnvelope<IEnumerable<TelemetryDTO>>(null, []);
            try
            {
                var telemetries = await _telemetryService.GetTelemetries();
                response.Data = _mapper.Map<IEnumerable<TelemetryDTO>>(telemetries);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching telemetries.");
                response.Errors.Add(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, response);
            }
        }

        // POST: api/v1/Telemetries
        [HttpPost]
        public IActionResult Post([FromBody] CreateTelemetryDTO telemetryDTO)
        {
            var response = new MessageEnvelope<object>(null, []);
            try
            {
                var telemetry = _mapper.Map<Telemetry>(telemetryDTO);
                _telemetryService.CreateTelemetry(telemetry);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while processing telemetry data.");
                response.Errors.Add(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, response);
            }
        }
    }
}