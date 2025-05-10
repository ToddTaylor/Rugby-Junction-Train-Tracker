using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Web.Server.DTOs;
using Web.Server.Entities;
using Web.Server.Services;

namespace Web.Server.Controllers.v1
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class OwnersController : ControllerBase
    {
        private readonly IOwnerService _ownerService;
        private readonly ILogger<OwnersController> _logger;
        private readonly IMapper _mapper;

        public OwnersController(
            ILogger<OwnersController> logger,
            IMapper mapper,
            IOwnerService ownerService)
        {
            _logger = logger;
            _mapper = mapper;
            _ownerService = ownerService;
        }

        // ...existing code...
        [HttpGet]
        public async Task<ActionResult> GetOwners()
        {
            var response = new MessageEnvelope<IEnumerable<OwnerDTO>>(null, new List<string>());
            try
            {
                var owners = await _ownerService.GetOwnersAsync();
                response.Data = _mapper.Map<IEnumerable<OwnerDTO>>(owners);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching owners.");
                response.Errors.Add(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, response);
            }
        }

        // GET: api/Owners/5
        [HttpGet("{id}")]
        public async Task<ActionResult> GetOwner(int id)
        {
            var response = new MessageEnvelope<OwnerDTO>(null, []);
            try
            {
                var owner = await _ownerService.GetOwnerByIdAsync(id);

                if (owner == null)
                {
                    return NotFound(new { data = new List<object>(), errors = new List<string> { "Owner not found." } });
                }

                var ownerDTO = _mapper.Map<OwnerDTO>(owner);

                response.Data = ownerDTO;

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching the owner.");
                response.Errors.Add(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, response);
            }
        }

        // PUT: api/Owners/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutOwner(int id, UpdateOwnerDTO updateOwnerDTO)
        {
            var response = new MessageEnvelope<OwnerDTO>(null, []);

            try
            {
                if (id != updateOwnerDTO.ID)
                {
                    return BadRequest();
                }

                var owner = _mapper.Map<Owner>(updateOwnerDTO);

                try
                {
                    await _ownerService.UpdateOwnerAsync(id, owner);
                }
                catch (KeyNotFoundException)
                {
                    return NotFound();
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while updating the owner.");
                response.Errors.Add(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, response);
            }
        }

        // POST: api/Owners
        [HttpPost]
        public async Task<ActionResult> PostOwner(CreateOwnerDTO createOwnerDTO)
        {
            var response = new MessageEnvelope<OwnerDTO>(null, []);
            try
            {
                var owner = _mapper.Map<Owner>(createOwnerDTO);
                var createdOwner = await _ownerService.CreateOwnerAsync(owner);
                response.Data = _mapper.Map<OwnerDTO>(createdOwner);
                return CreatedAtAction("GetOwner", new { id = response.Data.ID }, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while creating the owner.");
                response.Errors.Add(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, response);
            }
        }

        // DELETE: api/Owners/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteOwner(int id)
        {
            var success = await _ownerService.DeleteOwnerAsync(id);
            if (!success)
            {
                return NotFound();
            }

            return NoContent();
        }
    }
}

