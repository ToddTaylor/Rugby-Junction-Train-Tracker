using Microsoft.AspNetCore.Mvc;
using Web.Server.DTOs;
using Web.Server.Enums;
using Web.Server.Filters;
using Web.Server.Services;

namespace Web.Server.Controllers.v1
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class AmtrakController : ControllerBase
    {
        private readonly IAmtrakConfigurationService _configurationService;
        private readonly ILogger<AmtrakController> _logger;

        public AmtrakController(
            IAmtrakConfigurationService configurationService,
            ILogger<AmtrakController> logger)
        {
            _configurationService = configurationService;
            _logger = logger;
        }

        [RequireRole(RoleNames.Admin)]
        [HttpGet("trains")]
        public async Task<ActionResult<MessageEnvelope<IEnumerable<AmtrakTrackedTrainDTO>>>> GetTrackedTrains()
        {
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

        [RequireRole(RoleNames.Admin)]
        [HttpPost("trains")]
        public async Task<ActionResult<MessageEnvelope<AmtrakTrackedTrainDTO>>> AddTrackedTrain(CreateAmtrakTrackedTrainDTO request)
        {
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

        [RequireRole(RoleNames.Admin)]
        [HttpDelete("trains/{id}")]
        public async Task<IActionResult> DeleteTrackedTrain(int id)
        {
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

        [RequireRole(RoleNames.Admin)]
        [HttpGet("polling")]
        public async Task<ActionResult<MessageEnvelope<AmtrakPollingConfigurationDTO>>> GetPollingConfiguration()
        {
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

        [RequireRole(RoleNames.Admin)]
        [HttpPut("polling")]
        public async Task<ActionResult<MessageEnvelope<AmtrakPollingConfigurationDTO>>> UpdatePollingConfiguration(UpdateAmtrakPollingConfigurationDTO request)
        {
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

    }
}