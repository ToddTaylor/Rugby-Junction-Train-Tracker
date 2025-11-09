using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Web.Server.DTOs;
using Web.Server.Entities;
using Web.Server.Services;

namespace Web.Server.Controllers.v1
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ILogger<UsersController> _logger;
        private readonly IMapper _mapper;

        public UsersController(
            ILogger<UsersController> logger,
            IMapper mapper,
            IUserService userService)
        {
            _logger = logger;
            _mapper = mapper;
            _userService = userService;
        }

        [HttpGet]
        public async Task<ActionResult> GetUsers()
        {
            var response = new MessageEnvelope<IEnumerable<UserDTO>>(null, new List<string>());
            try
            {
                var users = await _userService.GetUsersAsync();
                response.Data = _mapper.Map<IEnumerable<UserDTO>>(users);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching users.");
                response.Errors.Add(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, response);
            }
        }

        // GET: api/Users/5
        [HttpGet("{id}")]
        public async Task<ActionResult> GetUser(int id)
        {
            var response = new MessageEnvelope<UserDTO>(null, []);
            try
            {
                var user = await _userService.GetUserByIdAsync(id);

                if (user == null)
                {
                    return NotFound(new { data = new List<object>(), errors = new List<string> { "User not found." } });
                }

                var userDTO = _mapper.Map<UserDTO>(user);

                response.Data = userDTO;

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching the user.");
                response.Errors.Add(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, response);
            }
        }

        // GET: api/users?email={emailAddress}
        [HttpGet("by-email")]
        public async Task<ActionResult> GetUserByEmail([FromQuery] string email)
        {
            var response = new MessageEnvelope<UserDTO>(null, []);
            try
            {
                var user = await _userService.GetUserByEmailAsync(email);

                if (user == null)
                {
                    return NotFound(new { data = new List<object>(), errors = new List<string> { "User not found." } });
                }

                var userDTO = _mapper.Map<UserDTO>(user);

                response.Data = userDTO;

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching the user.");
                response.Errors.Add(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, response);
            }
        }

        // PUT: api/Users/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutUser(int id, UpdateUserDTO updateUser)
        {
            var response = new MessageEnvelope<UserDTO>(null, []);

            try
            {
                if (id != updateUser.ID)
                {
                    return BadRequest();
                }

                var user = _mapper.Map<User>(updateUser);

                try
                {
                    await _userService.UpdateUserAsync(id, user);
                }
                catch (KeyNotFoundException)
                {
                    return NotFound();
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while updating the user.");
                response.Errors.Add(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, response);
            }
        }

        // POST: api/Users
        [HttpPost]
        public async Task<ActionResult> PostUser(CreateUserDTO createUser)
        {
            var response = new MessageEnvelope<UserDTO>(null, []);
            try
            {
                var user = _mapper.Map<User>(createUser);
                var createdUser = await _userService.CreateUserAsync(user);
                response.Data = _mapper.Map<UserDTO>(createdUser);
                return CreatedAtAction("GetUser", new { id = response.Data.ID }, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while creating the user.");
                response.Errors.Add(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, response);
            }
        }

        // DELETE: api/Users/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var success = await _userService.DeleteUserAsync(id);
            if (!success)
            {
                return NotFound();
            }

            return NoContent();
        }
    }
}

