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

        // ...existing code...
        [HttpGet]
        public async Task<ActionResult> GetRailroads()
        {
            var response = new MessageEnvelope<IEnumerable<RailroadDTO>>(null, []);
            try
            {
                var railroads = await _railroadService.GetRailroads();
                response.Data = _mapper.Map<IEnumerable<RailroadDTO>>(railroads);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching railroads.");
                response.Errors.Add(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, response);
            }
        }

        // GET: api/Railroads/5
        [HttpGet("{id}")]
        public async Task<ActionResult> GetRailroad(int id)
        {
            var response = new MessageEnvelope<RailroadDTO>(null, []);
            try
            {
                var railroad = await _railroadService.GetRailroad(id);

                if (railroad == null)
                {
                    response.Errors.Add("Railroad not found.");
                    return NotFound(response);
                }

                var railroadDTO = _mapper.Map<RailroadDTO>(railroad);

                response.Data = railroadDTO;

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching the railroad.");
                response.Errors.Add(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, response);
            }
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
                return NoContent();
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        // POST: api/Railroads
        [HttpPost]
        public async Task<ActionResult> PostRailroad(CreateRailroadDTO createRailroadDTO)
        {
            var response = new MessageEnvelope<RailroadDTO>(null, []);
            try
            {
                var railroad = _mapper.Map<Railroad>(createRailroadDTO);
                var createdRailroad = await _railroadService.CreateRailroad(railroad);
                response.Data = _mapper.Map<RailroadDTO>(createdRailroad);
                return CreatedAtAction("GetRailroad", new { id = response.Data.ID }, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while creating the railroad.");
                response.Errors.Add(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, response);
            }
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
                // Do nothing as item already doesn't exist.
            }

            return NoContent();
        }
    }
}