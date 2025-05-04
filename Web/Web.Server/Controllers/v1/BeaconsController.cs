using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Web.Server.DTOs;
using Web.Server.Entities;
using Web.Server.Services;

namespace Web.Server.Controllers.v1
{
    [Route("api/[controller]")]
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
        public async Task<ActionResult<IEnumerable<BeaconDTO>>> GetBeacons()
        {
            var beacons = await _beaconService.GetBeaconsAsync();
            var beaconDTOs = _mapper.Map<IEnumerable<BeaconDTO>>(beacons);

            return Ok(beaconDTOs);
        }

        // GET: api/v1/Beacons/5
        [HttpGet("{id}")]
        public async Task<ActionResult<BeaconDTO>> GetBeacon(int id)
        {
            var beacon = await _beaconService.GetBeaconByIdAsync(id);

            if (beacon == null)
            {
                return NotFound();
            }

            var beaconDTO = _mapper.Map<BeaconDTO>(beacon);

            return Ok(beaconDTO);
        }

        // PUT: api/v1/Beacons/5
        [HttpPut]
        public async Task<IActionResult> PutBeacon(UpdateBeaconDTO updateBeaconDTO)
        {
            var beacon = _mapper.Map<Beacon>(updateBeaconDTO);

            try
            {
                await _beaconService.UpdateBeaconAsync(updateBeaconDTO.ID, beacon);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }

            return NoContent();
        }

        // POST: api/v1/Beacons
        [HttpPost]
        public async Task<ActionResult<BeaconDTO>> PostBeacon(CreateBeaconDTO createBeaconDTO)
        {
            var beacon = _mapper.Map<Beacon>(createBeaconDTO);
            var createdBeacon = await _beaconService.CreateBeaconAsync(beacon);

            var beaconDTO = _mapper.Map<BeaconDTO>(createdBeacon);

            return CreatedAtAction("GetBeacon", new { id = createdBeacon.ID }, beaconDTO);
        }

        // POST: api/v1/Beacons/Health
        [HttpPost("Health/{id}")]
        public async Task<IActionResult> CheckHealth(int beaconId)
        {
            if (beaconId == 0)
            {
                return BadRequest("A beacon ID is required.");
            }

            // Simulate health check logic
            var beacon = await _beaconService.GetBeaconByIdAsync(beaconId);
            if (beacon == null)
            {
                return NotFound(new { Status = "Unhealthy", ID = beaconId });
            }

            return Ok(new { Status = "Healthy", ID = beaconId });
        }

        // DELETE: api/v1/Beacons/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBeacon(int id)
        {
            var success = await _beaconService.DeleteBeaconAsync(id);
            if (!success)
            {
                return NotFound();
            }

            return NoContent();
        }
    }
}

