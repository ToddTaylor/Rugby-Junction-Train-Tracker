using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;

namespace ConsoleApp.Subscribers.APILogger
{
    public class ApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public ApiClient()
        {
            _httpClient = new HttpClient();
            _configuration = ConfigurationHelper.LoadConfiguration();
        }

        public async Task PostAsync<TRequest>(TRequest data)
        {
            try
            {
                var url = $"{GetApUrl()}/Telemetries";

                var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = JsonContent.Create(data)
                };

                // Add the API key to the headers
                var _apiKey = GetApiKey();
                request.Headers.Add("X-Api-Key", _apiKey);

                HttpResponseMessage response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode == false)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"API error: {response.StatusCode} - {errorContent}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception during API call: {ex.Message}");
            }
        }

        private string? GetApiKey()
        {
            try
            {
                return _configuration.GetValue<string>("ApiSettings:ApiKey");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception during API key retrieval: {ex.Message}");
                throw;
            }
        }

        private string? GetApUrl()
        {
            try
            {
                return _configuration.GetValue<string>("ApiSettings:Url");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception during API URL retrieval: {ex.Message}");
                throw;
            }
        }
    }
}
