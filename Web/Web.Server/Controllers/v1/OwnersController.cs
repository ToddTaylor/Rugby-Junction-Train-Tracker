using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Web.Server.DTOs;
using Web.Server.Entities;
using Web.Server.Services;

namespace Web.Server.Controllers.v1
{
    [Route("api/[controller]")]
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

        // GET: api/Owners
        [HttpGet]
        public async Task<ActionResult<IEnumerable<UpdateOwnerDTO>>> GetOwners()
        {
            var owners = await _ownerService.GetOwnersAsync();
            var ownerDTOs = _mapper.Map<IEnumerable<UpdateOwnerDTO>>(owners);

            return Ok(ownerDTOs);
        }

        // GET: api/Owners/5
        [HttpGet("{id}")]
        public async Task<ActionResult<UpdateOwnerDTO>> GetOwner(int id)
        {
            var owner = await _ownerService.GetOwnerByIdAsync(id);

            if (owner == null)
            {
                return NotFound();
            }

            var ownerDTO = _mapper.Map<UpdateOwnerDTO>(owner);

            return Ok(ownerDTO);
        }

        // PUT: api/Owners/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutOwner(int id, UpdateOwnerDTO updateOwnerDTO)
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

        // POST: api/Owners
        [HttpPost]
        public async Task<ActionResult<UpdateOwnerDTO>> PostOwner(CreateOwnerDTO createOwnerDTO)
        {
            var owner = _mapper.Map<Owner>(createOwnerDTO);
            var createdOwner = await _ownerService.CreateOwnerAsync(owner);

            var ownerDTO = _mapper.Map<UpdateOwnerDTO>(createdOwner);

            return CreatedAtAction("GetOwner", new { id = createdOwner.ID }, ownerDTO);
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

