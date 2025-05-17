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
        private readonly IMapPinsService _mapPinsService;
        private readonly ILogger<MapPinsController> _logger;
        private readonly IMapper _mapper;

        public MapPinsController(IMapPinsService mapPinsService, ILogger<MapPinsController> logger, IMapper mapper)
        {
            _mapPinsService = mapPinsService;
            _logger = logger;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult> Get()
        {
            var response = new MessageEnvelope<IEnumerable<MapPinDTO>>(null, []);
            try
            {
                var mapPins = await _mapPinsService.GetMapPinsAsync();
                response.Data = _mapper.Map<IEnumerable<MapPinDTO>>(mapPins);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching map pins.");
                response.Errors.Add(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, response);
            }
        }
    }
}
