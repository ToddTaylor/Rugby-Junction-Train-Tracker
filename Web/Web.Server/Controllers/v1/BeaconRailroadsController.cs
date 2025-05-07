using Microsoft.AspNetCore.Mvc;
using Web.Server.DTOs;
using Web.Server.Entities;
using Web.Server.Services;

namespace Web.Server.Controllers.v1
{
    [ApiController]
    [Route("api/[controller]")]
    public class BeaconRailroadsController : ControllerBase
    {
        private readonly IBeaconRailroadService _service;

        public BeaconRailroadsController(IBeaconRailroadService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<BeaconRailroadDTO>>> GetAll()
        {
            var beaconRailroads = await _service.GetAllAsync();
            return Ok(beaconRailroads.Select(br => new BeaconRailroadDTO
            {
                BeaconID = br.BeaconID,
                RailroadID = br.RailroadID,
                Latitude = br.Latitude,
                Longitude = br.Longitude,
                Direction = br.Direction
            }));
        }

        [HttpGet("{beaconId:int}/{railroadId:int}")]
        public async Task<ActionResult<BeaconRailroadDTO>> GetById(int beaconId, int railroadId)
        {
            var beaconRailroad = await _service.GetByIdAsync(beaconId, railroadId);
            if (beaconRailroad == null)
            {
                return NotFound();
            }

            return Ok(new BeaconRailroadDTO
            {
                BeaconID = beaconRailroad.BeaconID,
                RailroadID = beaconRailroad.RailroadID,
                Latitude = beaconRailroad.Latitude,
                Longitude = beaconRailroad.Longitude,
                Direction = beaconRailroad.Direction
            });
        }

        [HttpPost]
        public async Task<ActionResult<BeaconRailroadDTO>> Create(CreateBeaconRailroadDTO dto)
        {
            var beaconRailroad = new BeaconRailroad
            {
                BeaconID = dto.BeaconID,
                RailroadID = dto.RailroadID,
                Latitude = dto.Latitude,
                Longitude = dto.Longitude,
                Direction = dto.Direction
            };

            var created = await _service.AddAsync(beaconRailroad);

            return CreatedAtAction(nameof(GetById), new { beaconId = created.BeaconID, railroadId = created.RailroadID }, new BeaconRailroadDTO
            {
                BeaconID = created.BeaconID,
                RailroadID = created.RailroadID,
                Latitude = created.Latitude,
                Longitude = created.Longitude,
                Direction = created.Direction
            });
        }

        [HttpPut("{beaconId:int}/{railroadId:int}")]
        public async Task<IActionResult> Update(int beaconId, int railroadId, UpdateBeaconRailroadDTO dto)
        {
            if (beaconId != dto.BeaconID || railroadId != dto.RailroadID)
            {
                return BadRequest("BeaconID and RailroadID in the URL must match the DTO.");
            }

            var beaconRailroad = new BeaconRailroad
            {
                BeaconID = dto.BeaconID,
                RailroadID = dto.RailroadID,
                Latitude = dto.Latitude,
                Longitude = dto.Longitude,
                Direction = dto.Direction
            };

            await _service.UpdateAsync(beaconRailroad);

            return NoContent();
        }

        [HttpDelete("{beaconId:int}/{railroadId:int}")]
        public async Task<IActionResult> Delete(int beaconId, int railroadId)
        {
            var deleted = await _service.DeleteAsync(beaconId, railroadId);
            if (!deleted)
            {
                return NotFound();
            }

            return NoContent();
        }
    }
}
