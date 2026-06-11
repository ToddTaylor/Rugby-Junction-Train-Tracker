using Microsoft.AspNetCore.Mvc;
using Web.Server.DTOs;
using Web.Server.Services;

namespace Web.Server.Controllers.v1
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class PassengerMapPinsController : ControllerBase
    {
        private readonly IPassengerMapPinService _passengerMapPinService;
        private readonly ILogger<PassengerMapPinsController> _logger;

        public PassengerMapPinsController(IPassengerMapPinService passengerMapPinService, ILogger<PassengerMapPinsController> logger)
        {
            _passengerMapPinService = passengerMapPinService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<MessageEnvelope<IEnumerable<PassengerMapPinDTO>>>> GetPassengerMapPins()
        {
            var response = new MessageEnvelope<IEnumerable<PassengerMapPinDTO>>(null!, []);
            try
            {
                response.Data = await _passengerMapPinService.GetPassengerMapPinsAsync();
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching passenger map pins.");
                response.Errors.Add(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, response);
            }
        }
    }
}