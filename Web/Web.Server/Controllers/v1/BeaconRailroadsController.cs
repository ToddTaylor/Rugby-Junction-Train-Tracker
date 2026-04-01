using MapsterMapper;
using Microsoft.AspNetCore.Mvc;
using Web.Server.DTOs;
using Web.Server.Services;

namespace Web.Server.Controllers.v1
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class BeaconRailroadsController : ControllerBase
    {
        private readonly IBeaconRailroadService _service;
        private readonly ILogger<BeaconRailroadsController> _logger;
        private readonly IMapper _mapper;

        public BeaconRailroadsController(IBeaconRailroadService service, ILogger<BeaconRailroadsController> logger, IMapper mapper)
        {
            _service = service;
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
                var beaconRailroadDTO = _mapper.Map<IEnumerable<BeaconRailroadDTO>>(beaconRailroads);
                response.Data = beaconRailroadDTO;
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

                var beaconRailroad = _mapper.Map<Entities.BeaconRailroad>(dto);

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
    }
}
