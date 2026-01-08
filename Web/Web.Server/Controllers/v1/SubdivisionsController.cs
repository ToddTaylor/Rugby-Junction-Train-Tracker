using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Web.Server.DTOs;
using Web.Server.Entities;
using Web.Server.Services;

namespace Web.Server.Controllers.v1
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class SubdivisionsController : ControllerBase
    {
        private readonly ISubdivisionService _subdivisionService;
        private readonly ILogger<SubdivisionsController> _logger;
        private readonly IMapper _mapper;

        public SubdivisionsController(ISubdivisionService subdivisionService, ILogger<SubdivisionsController> logger, IMapper mapper)
        {
            _subdivisionService = subdivisionService;
            _logger = logger;
            _mapper = mapper;
        }

        // ...existing code...
        [HttpGet]
        public async Task<ActionResult> GetSubdivisions()
        {
            var response = new MessageEnvelope<IEnumerable<SubdivisionDTO>>(null, []);
            try
            {
                var subdivisions = await _subdivisionService.GetSubdivisionsAsync();
                response.Data = _mapper.Map<IEnumerable<SubdivisionDTO>>(subdivisions);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching subdivisions.");
                response.Errors.Add(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, response);
            }
        }

        // GET: api/Subdivisions/5
        [HttpGet("{id}")]
        public async Task<ActionResult> GetSubdivision(int id)
        {
            var response = new MessageEnvelope<SubdivisionDTO>(null, []);
            try
            {
                var subdivision = await _subdivisionService.GetSubdivisionAsync(id);

                if (subdivision == null)
                {
                    response.Errors.Add("Subdivision not found.");
                    return NotFound(response);
                }

                var railroadDTO = _mapper.Map<SubdivisionDTO>(subdivision);

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

        // PUT: api/Subdivisions/5
        [HttpPut("{id}")]
        public async Task<ActionResult> PutSubdivision(int id, UpdateSubdivisionDTO updateSubdivisionDTO)
        {
            var response = new MessageEnvelope<SubdivisionDTO>(null, []);
            try
            {
                if (id != updateSubdivisionDTO.ID)
                {
                    response.Errors.Add("ID mismatch.");
                    return BadRequest(response);
                }

                var subdivision = _mapper.Map<Subdivision>(updateSubdivisionDTO);

                try
                {
                    var updatedSubdivision = await _subdivisionService.UpdateSubdivisionAsync(subdivision);
                    response.Data = _mapper.Map<SubdivisionDTO>(updatedSubdivision);
                    return Ok(response);
                }
                catch (KeyNotFoundException)
                {
                    response.Errors.Add("Subdivision not found.");
                    return NotFound(response);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while updating subdivision {SubdivisionId}.", id);
                response.Errors.Add(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, response);
            }
        }

        // POST: api/Subdivisions
        [HttpPost]
        public async Task<ActionResult> PostSubdivision(CreateSubdivisionDTO createSubdivisionDTO)
        {
            var response = new MessageEnvelope<SubdivisionDTO>(null, []);
            try
            {
                var subdivision = _mapper.Map<Subdivision>(createSubdivisionDTO);
                var createdSubdivision = await _subdivisionService.CreateSubdivisionAsync(subdivision);
                response.Data = _mapper.Map<SubdivisionDTO>(createdSubdivision);
                return CreatedAtAction("GetSubdivision", new { id = response.Data.ID }, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while creating the railroad.");
                response.Errors.Add(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, response);
            }
        }

        // DELETE: api/Subdivisions/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSubdivision(int id)
        {
            try
            {
                try
                {
                    await _subdivisionService.DeleteSubdivisionAsync(id);
                }
                catch (KeyNotFoundException)
                {
                    // Do nothing as item already doesn't exist.
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while deleting subdivision {SubdivisionId}.", id);
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
            }
        }
    }
}