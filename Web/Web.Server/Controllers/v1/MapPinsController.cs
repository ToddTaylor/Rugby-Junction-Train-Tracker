using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Web.Server.DTOs;
using Web.Server.Services;

namespace Web.Server.Controllers.v1
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class MapPinsController : ControllerBase
    {
        private readonly IMapPinService _mapPinsService;
        private readonly IBeaconRailroadService _beaconRailroadService;
        private readonly ILogger<MapPinsController> _logger;
        private readonly IMapper _mapper;

        public MapPinsController(
            IBeaconRailroadService beaconRailroadService,
            IMapPinService mapPinsService,
            ILogger<MapPinsController> logger,
            IMapper mapper)
        {
            _beaconRailroadService = beaconRailroadService;
            _mapPinsService = mapPinsService;
            _logger = logger;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult> Get([FromQuery] int? minutes)
        {
            var response = new MessageEnvelope<IEnumerable<MapPinDTO>>(null, []);
            try
            {
                var mapPins = await _mapPinsService.GetMapPinsAsync(minutes);
                var mapPinDTOs = _mapper.Map<IEnumerable<MapPinDTO>>(mapPins);

                foreach (var mapPinDTO in mapPinDTOs)
                {
                    var beaconRailroad = await _beaconRailroadService.GetByIdAsync(mapPinDTO.BeaconID, mapPinDTO.SubdivisionID.Value);
                    mapPinDTO.Milepost = beaconRailroad.Milepost;
                    mapPinDTO.Latitude = beaconRailroad.Latitude;
                    mapPinDTO.Longitude = beaconRailroad.Longitude;
                    mapPinDTO.Railroad = beaconRailroad.Subdivision.Railroad.Name;
                    mapPinDTO.Subdivision = beaconRailroad.Subdivision.Name;
                }

                response.Data = mapPinDTOs;
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching map pins.");
                response.Errors.Add(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, response);
            }
        }

        [HttpGet("Latest")]
        public async Task<ActionResult> GetLatest()
        {
            var response = new MessageEnvelope<IEnumerable<MapPinLatestDTO>>(null, []);
            try
            {
                var mapPins = await _mapPinsService.GetMapPinsLatestAsync();
                var mapPinDTOs = _mapper.Map<IEnumerable<MapPinLatestDTO>>(mapPins);

                response.Data = mapPinDTOs;
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching map pins.");
                response.Errors.Add(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, response);
            }
        }

        // DELETE: api/v1/MapPin/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBeacon(int id)
        {
            var success = await _mapPinsService.DeleteMapPinAsync(id);
            if (!success)
            {
                return NotFound();
            }

            return NoContent();
        }
    }
}
