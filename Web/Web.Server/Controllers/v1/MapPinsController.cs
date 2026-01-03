using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
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
        private readonly IMapPinHistoryService _mapPinHistoryService;
        private readonly ILogger<MapPinsController> _logger;
        private readonly IMapper _mapper;

        public MapPinsController(
            IBeaconRailroadService beaconRailroadService,
            IMapPinService mapPinsService,
            IMapPinHistoryService mapPinHistoryService,
            ILogger<MapPinsController> logger,
            IMapper mapper)
        {
            _beaconRailroadService = beaconRailroadService;
            _mapPinsService = mapPinsService;
            _mapPinHistoryService = mapPinHistoryService;
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

        [HttpGet("History/{beaconId}")]
        public async Task<ActionResult> GetHistory(int beaconId, [FromQuery] int? subdivisionId = null, [FromQuery] int? limit = 100)
        {
            var response = new MessageEnvelope<IEnumerable<MapPinHistoryDTO>>(null, []);
            try
            {
                var histories = await _mapPinHistoryService.GetHistoryByBeaconIdAsync(beaconId, subdivisionId, limit);
                var historyDTOs = new List<MapPinHistoryDTO>();

                foreach (var history in histories)
                {
                    var dto = new MapPinHistoryDTO
                    {
                        ID = history.ID,
                        BeaconID = history.BeaconID,
                        SubdivisionID = history.SubdivisionId,
                        Direction = history.Direction,
                        Moving = history.Moving,
                        IsLocal = history.IsLocal,
                        CreatedAt = history.CreatedAt.ToString("O"),
                        LastUpdate = history.LastUpdate.ToString("O")
                    };

                    // Populate beacon railroad data
                    if (history.BeaconRailroad != null)
                    {
                        dto.BeaconName = history.BeaconRailroad.Beacon?.Name;
                        dto.Latitude = history.BeaconRailroad.Latitude;
                        dto.Longitude = history.BeaconRailroad.Longitude;
                        dto.Milepost = history.BeaconRailroad.Milepost;
                        dto.Subdivision = history.BeaconRailroad.Subdivision?.Name;
                        dto.Railroad = history.BeaconRailroad.Subdivision?.Railroad?.Name;
                    }

                    // Deserialize addresses from JSON
                    if (!string.IsNullOrEmpty(history.AddressesJson))
                    {
                        try
                        {
                            dto.Addresses = JsonSerializer.Deserialize<List<AddressSnapshotDTO>>(history.AddressesJson);
                        }
                        catch
                        {
                            dto.Addresses = new List<AddressSnapshotDTO>();
                        }
                    }

                    historyDTOs.Add(dto);
                }

                response.Data = historyDTOs;
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching map pin history for beacon {BeaconId}.", beaconId);
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
