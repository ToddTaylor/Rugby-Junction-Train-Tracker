using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Web.Server.DTOs;
using Web.Server.Entities;
using Web.Server.Services;

namespace Web.Server.Controllers.v1
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class RailroadsController : ControllerBase
    {
        private readonly IRailroadService _railroadService;
        private readonly ILogger<RailroadsController> _logger;
        private readonly IMapper _mapper;

        public RailroadsController(IRailroadService railroadService, ILogger<RailroadsController> logger, IMapper mapper)
        {
            _railroadService = railroadService;
            _logger = logger;
            _mapper = mapper;
        }

        // GET: api/Railroads
        [HttpGet]
        public async Task<ActionResult<IEnumerable<RailroadDTO>>> GetRailroads()
        {
            var railroads = await _railroadService.GetRailroads();
            var railroadDTOs = _mapper.Map<IEnumerable<RailroadDTO>>(railroads);
            return Ok(railroadDTOs);
        }

        // GET: api/Railroads/5
        [HttpGet("{id}")]
        public async Task<ActionResult<RailroadDTO>> GetRailroad(int id)
        {
            var railroad = await _railroadService.GetRailroad(id);

            if (railroad == null)
            {
                return NotFound();
            }

            var railroadDTO = _mapper.Map<RailroadDTO>(railroad);

            return Ok(railroadDTO);
        }

        // PUT: api/Railroads/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutRailroad(int id, UpdateRailroadDTO updateRailroadDTO)
        {
            if (id != updateRailroadDTO.ID)
            {
                return BadRequest();
            }

            var railroad = _mapper.Map<Railroad>(updateRailroadDTO);

            try
            {
                await _railroadService.UpdateRailroad(railroad);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }

            return NoContent();
        }

        // POST: api/Railroads
        [HttpPost]
        public async Task<ActionResult<RailroadDTO>> PostRailroad(CreateRailroadDTO createRailroadDTO)
        {
            var railroad = _mapper.Map<Railroad>(createRailroadDTO);
            var createdRailroad = await _railroadService.CreateRailroad(railroad);

            var railroadDTO = _mapper.Map<RailroadDTO>(createdRailroad);

            return CreatedAtAction("GetRailroad", new { id = createdRailroad.ID }, railroadDTO);
        }

        // DELETE: api/Railroads/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRailroad(int id)
        {
            try
            {
                await _railroadService.DeleteRailroad(id);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }

            return NoContent();
        }
    }
}