using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Web.Server.DTOs;
using Web.Server.Entities;
using Web.Server.Services;

namespace Web.Server.Controllers.v1
{
    [Route("api/[controller]")]
    [ApiController]
    public class RailroadsController : ControllerBase
    {
        private readonly IRailroadService _service;
        private readonly ILogger<TelemetriesController> _logger;
        private readonly IMapper _mapper;

        public RailroadsController(IRailroadService service, ILogger<TelemetriesController> logger, IMapper mapper)
        {
            _service = service;
            _logger = logger;
            _mapper = mapper;
        }

        // GET: api/Railroads
        [HttpGet]
        public async Task<ActionResult<IEnumerable<RailroadDTO>>> GetRailroads()
        {
            var railroads = await _service.GetRailroads();
            var railroadDTOs = _mapper.Map<IEnumerable<RailroadDTO>>(railroads);
            return Ok(railroadDTOs);
        }

        // GET: api/Railroads/5
        [HttpGet("{id}")]
        public async Task<ActionResult<RailroadDTO>> GetRailroad(int id)
        {
            var railroad = await _service.GetRailroad(id);

            if (railroad == null)
            {
                return NotFound();
            }

            var railroadDTO = _mapper.Map<RailroadDTO>(railroad);

            return Ok(railroadDTO);
        }

        // PUT: api/Railroads/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut()]
        public async Task<IActionResult> PutRailroad(UpdateRailroadDTO updateRailroadDTO)
        {
            var railroad = _mapper.Map<Railroad>(updateRailroadDTO);

            _service.UpdateRailroad(railroad);

            return NoContent();
        }

        // POST: api/Railroads
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<RailroadDTO>> PostRailroad(UpdateRailroadDTO updateRailroadDTO)
        {
            var railroad = _mapper.Map<Railroad>(updateRailroadDTO);

            railroad = await _service.CreateRailroad(railroad);

            var railroadDTO = _mapper.Map<RailroadDTO>(railroad);

            return Ok(railroadDTO);
        }

        // DELETE: api/Railroads/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRailroad(int id)
        {
            _service.DeleteRailroad(id);

            return NoContent();
        }
    }
}
