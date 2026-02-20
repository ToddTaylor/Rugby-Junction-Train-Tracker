using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web.Server.Data;
using Web.Server.DTOs;

namespace Web.Server.Controllers.v1
{
    [Route("api/v1/[controller]/roles")]
    [ApiController]
    public class RolesController : ControllerBase
    {
        private readonly TelemetryDbContext _context;

        public RolesController(TelemetryDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult> GetRoles()
        {
            var response = new MessageEnvelope<IEnumerable<string>>(default!, new List<string>());
            try
            {
                var roles = await _context.Roles.Select(r => r.RoleName).ToListAsync();
                response.Data = roles;
                return Ok(response);
            }
            catch (Exception ex)
            {
                response.Errors.Add(ex.Message);
                return StatusCode(500, response);
            }
        }
    }
}
