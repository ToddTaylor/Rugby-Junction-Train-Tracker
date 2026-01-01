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
        public async Task<ActionResult> GetTelemetries()
        {
            var response = new MessageEnvelope<IEnumerable<TelemetryDTO>>(null, []);
            try
            {
                var telemetries = await _telemetryService.GetTelemetriesAsync();
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

        // GET: api/v1/Telemetries/5
        [HttpGet("{id}")]
        public async Task<ActionResult<TelemetryDTO>> GetTelemetry(int id)
        {
            var response = new MessageEnvelope<RailroadDTO>(null, []);
            try
            {
                var telemetry = await _telemetryService.GetTelemetryByIdAsync(id);

                if (telemetry == null)
                {
                    return NotFound();
                }

                var telemetryDTO = _mapper.Map<TelemetryDTO>(telemetry);

                return Ok(telemetryDTO);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching the telemetry.");
                response.Errors.Add(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, response);
            }
        }


        // POST: api/v1/Telemetries
        [HttpPost]
        public async Task<ActionResult> PostTelemetry(CreateTelemetryDTO telemetryDTO)
        {
            var response = new MessageEnvelope<TelemetryDTO>(null, []);
            try
            {
                var telemetry = _mapper.Map<Telemetry>(telemetryDTO);
                var createdTelemetry = await _telemetryService.CreateMapPinAsync(telemetry);
                response.Data = _mapper.Map<TelemetryDTO>(createdTelemetry);
                return CreatedAtAction("GetTelemetry", new { id = response.Data.ID }, response);
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