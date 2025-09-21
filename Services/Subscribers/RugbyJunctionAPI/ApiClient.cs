using Services.Models;

namespace Services.Subscribers.RugbyJunctionAPI
{
    public class ApiClient
    {
        protected readonly HttpClient _httpClient;
        private readonly ApiSettings _apiSettings;

        public ApiClient(AppSettings appSettings)
        {
            _httpClient = new HttpClient();
            _apiSettings = appSettings.Subscribers
                .First(s => s.ID == Constants.SUBSCRIBER_ID)
                .ApiSettings;
        }

        protected string? GetApiKey()
        {
            try
            {
                return _apiSettings.ApiKey;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception during API key retrieval from appSettings: {ex.Message}");
                throw;
            }
        }

        protected string? GetApUrl()
        {
            try
            {
                return _apiSettings.Url;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception during API URL retrieval from appSettings: {ex.Message}");
                throw;
            }
        }
    }
}
