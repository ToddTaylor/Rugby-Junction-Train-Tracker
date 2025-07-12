using Microsoft.Extensions.Configuration;

namespace ConsoleApp.Subscribers.LiveTrainTrackerAPI
{
    public class ApiClient
    {
        protected readonly HttpClient _httpClient;

        protected readonly IConfiguration _configuration;

        public ApiClient()
        {
            _httpClient = new HttpClient();
            _configuration = ConfigurationHelper.LoadConfiguration();
        }

        protected string? GetApiKey()
        {
            try
            {
                return _configuration.GetValue<string>("Subscribers:0:ApiSettings:ApiKey");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception during API key retrieval from configuration: {ex.Message}");
                throw;
            }
        }

        protected string? GetHealthUrl()
        {
            try
            {
                return _configuration.GetValue<string>("Subscribers:0:ApiSettings:HealthUrl");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception during health URL retrieval from configuration: {ex.Message}");
                throw;
            }
        }

        protected string? GetTelemetryUrl()
        {
            try
            {
                return _configuration.GetValue<string>("Subscribers:0:ApiSettings:TelemetryUrl");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception during telemetry URL retrieval from configuration: {ex.Message}");
                throw;
            }
        }
    }
}
