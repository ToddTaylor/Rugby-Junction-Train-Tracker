using Microsoft.AspNetCore.Mvc;
using Web.Server.DTOs;
using Web.Server.Services;

namespace Web.Server.Controllers.v1
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class AmtrakController : ControllerBase
    {
        private readonly IAmtrakConfigurationService _configurationService;
        private readonly IUserService _userService;
        private readonly ILogger<AmtrakController> _logger;

        public AmtrakController(
            IAmtrakConfigurationService configurationService,
            IUserService userService,
            ILogger<AmtrakController> logger)
        {
            _configurationService = configurationService;
            _userService = userService;
            _logger = logger;
        }

        [HttpGet("trains")]
        public async Task<ActionResult<MessageEnvelope<IEnumerable<AmtrakTrackedTrainDTO>>>> GetTrackedTrains()
        {
            if (!await IsAdminAsync())
            {
                return Forbid();
            }

            var response = new MessageEnvelope<IEnumerable<AmtrakTrackedTrainDTO>>(null!, []);
            try
            {
                response.Data = await _configurationService.GetTrackedTrainsAsync();
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching Amtrak tracked trains.");
                response.Errors.Add(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, response);
            }
        }

        [HttpPost("trains")]
        public async Task<ActionResult<MessageEnvelope<AmtrakTrackedTrainDTO>>> AddTrackedTrain(CreateAmtrakTrackedTrainDTO request)
        {
            if (!await IsAdminAsync())
            {
                return Forbid();
            }

            var response = new MessageEnvelope<AmtrakTrackedTrainDTO>(null!, []);
            try
            {
                response.Data = await _configurationService.AddTrackedTrainAsync(request.TrainNumber);
                return CreatedAtAction(nameof(GetTrackedTrains), response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while adding Amtrak tracked train {TrainNumber}.", request.TrainNumber);
                response.Errors.Add(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, response);
            }
        }

        [HttpDelete("trains/{id}")]
        public async Task<IActionResult> DeleteTrackedTrain(int id)
        {
            if (!await IsAdminAsync())
            {
                return Forbid();
            }

            try
            {
                var deleted = await _configurationService.DeleteTrackedTrainAsync(id);
                if (!deleted)
                {
                    return NotFound();
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while deleting Amtrak tracked train {TrainId}.", id);
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
            }
        }

        [HttpGet("polling")]
        public async Task<ActionResult<MessageEnvelope<AmtrakPollingConfigurationDTO>>> GetPollingConfiguration()
        {
            if (!await IsAdminAsync())
            {
                return Forbid();
            }

            var response = new MessageEnvelope<AmtrakPollingConfigurationDTO>(null!, []);
            try
            {
                response.Data = await _configurationService.GetPollingConfigurationAsync();
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching Amtrak polling configuration.");
                response.Errors.Add(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, response);
            }
        }

        [HttpPut("polling")]
        public async Task<ActionResult<MessageEnvelope<AmtrakPollingConfigurationDTO>>> UpdatePollingConfiguration(UpdateAmtrakPollingConfigurationDTO request)
        {
            if (!await IsAdminAsync())
            {
                return Forbid();
            }

            var response = new MessageEnvelope<AmtrakPollingConfigurationDTO>(null!, []);
            try
            {
                response.Data = await _configurationService.UpdatePollingConfigurationAsync(request.PollIntervalMinutes);
                return Ok(response);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                response.Errors.Add(ex.Message);
                return BadRequest(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while updating Amtrak polling configuration.");
                response.Errors.Add(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, response);
            }
        }

        private async Task<bool> IsAdminAsync()
        {
            if (!HttpContext.Items.TryGetValue("UserId", out var userIdObj) || userIdObj is not int userId)
            {
                return false;
            }

            var user = await _userService.GetUserByIdAsync(userId);
            return user?.UserRoles?.Any(ur =>
                string.Equals(ur.Role?.RoleName, "Admin", StringComparison.OrdinalIgnoreCase)) == true;
        }
    }
}