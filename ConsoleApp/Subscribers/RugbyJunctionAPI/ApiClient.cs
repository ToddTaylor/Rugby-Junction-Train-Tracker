using Microsoft.Extensions.Configuration;

namespace ConsoleApp.Subscribers.RugbyJunctionAPI
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
                return _configuration.GetValue<string>("Subscribers:1:ApiSettings:ApiKey");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception during API key retrieval from configuration: {ex.Message}");
                throw;
            }
        }

        protected string? GetApUrl()
        {
            try
            {
                return _configuration.GetValue<string>("Subscribers:1:ApiSettings:Url");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception during API URL retrieval from configuration: {ex.Message}");
                throw;
            }
        }
    }
}
