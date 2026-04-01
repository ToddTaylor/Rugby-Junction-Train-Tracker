using MapsterMapper;
using Microsoft.AspNetCore.Mvc;
using Web.Server.DTOs;
using Web.Server.Services;

namespace Web.Server.Controllers.v1
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class TelemetriesController : ControllerBase
    {
        // In-memory queue for telemetry messages
        internal static readonly System.Collections.Concurrent.ConcurrentQueue<string> _telemetryQueue = new();

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
            var response = new MessageEnvelope<IEnumerable<TelemetryDTO>>(default!, new List<string>());
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
            var response = new MessageEnvelope<TelemetryDTO>(default!, new List<string>());
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
            var response = new MessageEnvelope<TelemetryDTO>(default!, new List<string>());
            try
            {
                // Serialize telemetryDTO to JSON (or other format)
                var telemetryJson = System.Text.Json.JsonSerializer.Serialize(telemetryDTO);

                // Enqueue telemetry message to in-memory queue
                _telemetryQueue.Enqueue(telemetryJson);

                _logger.LogDebug($"[InMemoryQueue] Enqueued telemetry: {telemetryJson}");

                // Respond immediately
                return Accepted();
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
