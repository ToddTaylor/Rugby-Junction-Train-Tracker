using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web.Server.Data;
using Web.Server.DTOs;
using Web.Server.Entities;

namespace Web.Server.Controllers.v1
{
    [Route("api/[controller]")]
    [ApiController]
    public class BeaconsController : ControllerBase
    {
        private readonly TelemetryDbContext _context;
        private readonly ILogger<TelemetriesController> _logger;
        private readonly IMapper _mapper;

        public BeaconsController(TelemetryDbContext context, ILogger<TelemetriesController> logger, IMapper mapper)
        {
            _context = context;
            _logger = logger;
            _mapper = mapper;
        }

        // GET: api/v1/Beacons
        [HttpGet]
        public async Task<ActionResult<IEnumerable<BeaconDTO>>> GetBeacons()
        {
            var beaconDTOs = _mapper.Map<IEnumerable<BeaconDTO>>(await GetBeaconsFromDb());

            return Ok(beaconDTOs);
        }

        // GET: api/v1/Beacons/5
        [HttpGet("{id}")]
        public async Task<ActionResult<BeaconDTO>> GetBeacon(int id)
        {
            var beacon = await _context.Beacons.FindAsync(id);

            if (beacon == null)
            {
                return NotFound();
            }

            var beaconDTO = _mapper.Map<BeaconDTO>(beacon);

            return Ok(beacon);
        }

        // PUT: api/v1/Beacons/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut()]
        public async Task<IActionResult> PutBeacon(UpdateBeaconDTO updateBeaconDTO)
        {
            var beacon = _mapper.Map<Beacon>(updateBeaconDTO);

            _context.Entry(beacon).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!BeaconExists(updateBeaconDTO.ID))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/v1/Beacons
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<BeaconDTO>> PostBeacon(CreateBeaconDTO createBeaconDTO)
        {
            var beacon = _mapper.Map<Beacon>(createBeaconDTO);
            _context.Beacons.Add(beacon);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetBeacon", new { id = beacon.ID }, beacon);
        }

        // POST: api/v1/Beacons/Health
        [HttpPost("Health/{id}")]
        public async Task<IActionResult> CheckHealth(int beaconId)
        {
            if (beaconId == 0)
            {
                return BadRequest("A beacon ID is required.");
            }

            // TODO: Replace with actual logic to check how long it's been since this beacon last reported telemetry.
            // If it's been too long, return not healthy.

            // Simulate health check logic
            bool isHealthy = beaconId == 12345;

            if (isHealthy)
            {
                return Ok(new { Status = "Healthy", ID = beaconId });
            }
            else
            {
                return NotFound(new { Status = "Unhealthy", ID = beaconId });
            }
        }

        // DELETE: api/v1/Beacons/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBeacon(int id)
        {
            var beacon = await _context.Beacons.FindAsync(id);
            if (beacon == null)
            {
                return NotFound();
            }

            _context.Beacons.Remove(beacon);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool BeaconExists(int id)
        {
            return _context.Beacons.Any(e => e.ID == id);
        }
    }
}
