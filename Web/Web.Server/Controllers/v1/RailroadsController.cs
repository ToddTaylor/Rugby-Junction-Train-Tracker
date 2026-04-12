using MapsterMapper;
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
                var railroads = await _railroadService.GetRailroadsAsync();
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
                var railroad = await _railroadService.GetRailroadAsync(id);

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
        public async Task<ActionResult> PutRailroad(int id, UpdateRailroadDTO updateRailroadDTO)
        {
            var response = new MessageEnvelope<RailroadDTO>(null, []);
            try
            {
                if (id != updateRailroadDTO.ID)
                {
                    response.Errors.Add("ID mismatch.");
                    return BadRequest(response);
                }

                var railroad = _mapper.Map<Railroad>(updateRailroadDTO);
                railroad.ID = id;  // Ensure ID is set for the database update

                try
                {
                    var updatedRailroad = await _railroadService.UpdateRailroadAsync(railroad);
                    response.Data = _mapper.Map<RailroadDTO>(updatedRailroad);
                    return Ok(response);
                }
                catch (KeyNotFoundException)
                {
                    response.Errors.Add("Railroad not found.");
                    return NotFound(response);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while updating railroad {RailroadId}.", id);
                response.Errors.Add(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, response);
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
                var createdRailroad = await _railroadService.CreateRailroadAsync(railroad);
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
                try
                {
                    await _railroadService.DeleteRailroadAsync(id);
                }
                catch (KeyNotFoundException)
                {
                    // Do nothing as item already doesn't exist.
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while deleting railroad {RailroadId}.", id);
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
            }
        }
    }
}
