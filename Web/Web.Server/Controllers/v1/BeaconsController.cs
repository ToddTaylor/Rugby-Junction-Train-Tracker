using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;

namespace Web.Server.Controllers.v1
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class BeaconsController : ControllerBase
    {
        [HttpPost("Health")]
        public IActionResult CheckHealth([FromBody] HealthRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.BeaconID))
            {
                return BadRequest("ID cannot be null or empty.");
            }

            // Simulate health check logic
            bool isHealthy = request.BeaconID == "12345"; // Replace with actual logic

            if (isHealthy)
            {
                return Ok(new { Status = "Healthy", ID = request.BeaconID });
            }
            else
            {
                return NotFound(new { Status = "Unhealthy", ID = request.BeaconID });
            }
        }
    }

    public class HealthRequest
    {
        [JsonPropertyName("BeaconID")]
        public string BeaconID { get; set; }
    }
}
