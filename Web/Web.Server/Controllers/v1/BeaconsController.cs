using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Web.Server.DTOs;
using Web.Server.Entities;
using Web.Server.Services;

namespace Web.Server.Controllers.v1
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class BeaconsController : ControllerBase
    {
        private readonly IBeaconService _beaconService;
        private readonly ILogger<BeaconsController> _logger;
        private readonly IMapper _mapper;

        public BeaconsController(IBeaconService beaconService, ILogger<BeaconsController> logger, IMapper mapper)
        {
            _beaconService = beaconService;
            _logger = logger;
            _mapper = mapper;
        }

        // GET: api/v1/Beacons
        [HttpGet]
        public async Task<ActionResult> GetBeacons()
        {
            var response = new MessageEnvelope<IEnumerable<BeaconDTO>>(null, []);
            try
            {
                var beacons = await _beaconService.GetBeaconsAsync();
                response.Data = _mapper.Map<IEnumerable<BeaconDTO>>(beacons);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching beacons.");
                response.Errors.Add(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, response);
            }
        }

        // GET: api/v1/Beacons/5
        [HttpGet("{id}")]
        public async Task<ActionResult<BeaconDTO>> GetBeacon(int id)
        {
            var response = new MessageEnvelope<RailroadDTO>(null, []);
            try
            {
                var beacon = await _beaconService.GetBeaconByIdAsync(id);

                if (beacon == null)
                {
                    return NotFound();
                }

                var beaconDTO = _mapper.Map<BeaconDTO>(beacon);

                return Ok(beaconDTO);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching the beacon.");
                response.Errors.Add(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, response);
            }
        }

        // PUT: api/v1/Beacons/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutBeacon(int id, UpdateBeaconDTO updateBeaconDTO)
        {
            var response = new MessageEnvelope<BeaconDTO>(null, []);
            try
            {
                if (id != updateBeaconDTO.ID)
                {
                    return BadRequest();
                }

                var beacon = _mapper.Map<Beacon>(updateBeaconDTO);

                Beacon updatedBeacon;
                try
                {
                    updatedBeacon = await _beaconService.UpdateBeaconAsync(id, beacon);
                }
                catch (KeyNotFoundException)
                {
                    return NotFound();
                }

                response.Data = _mapper.Map<BeaconDTO>(updatedBeacon);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while updating the beacon.");
                response.Errors.Add(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, response);
            }
        }

        // POST: api/v1/Beacons
        [HttpPost]
        public async Task<ActionResult> PostBeacon(CreateBeaconDTO createBeaconDTO)
        {
            var response = new MessageEnvelope<BeaconDTO>(null, []);
            try
            {
                var beacon = _mapper.Map<Beacon>(createBeaconDTO);
                var createdBeacon = await _beaconService.CreateBeaconAsync(beacon);
                response.Data = _mapper.Map<BeaconDTO>(createdBeacon);
                return CreatedAtAction("GetBeacon", new { id = response.Data.ID }, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while creating the beacon.");
                response.Errors.Add(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, response);
            }
        }

        // POST: api/v1/Beacons/Health/{id}
        [HttpPost("Health/{id}")]
        public async Task<IActionResult> CheckHealth(int id)
        {
            try
            {
                if (id == 0)
                {
                    return BadRequest("A beacon ID is required.");
                }

                var beacon = await _beaconService.GetBeaconByIdAsync(id);

                if (beacon == null)
                {
                    return NotFound();
                }

                await _beaconService.UpdateBeaconHealthAsync(id, beacon);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while checking health for beacon {BeaconId}.", id);
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
            }
        }

        // DELETE: api/v1/Beacons/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBeacon(int id)
        {
            try
            {
                var success = await _beaconService.DeleteBeaconAsync(id);
                if (!success)
                {
                    return NotFound();
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while deleting beacon {BeaconId}.", id);
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
            }
        }
    }
}

