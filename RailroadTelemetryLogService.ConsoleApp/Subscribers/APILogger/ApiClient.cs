using System.Net.Http.Json;

namespace ConsoleApp.Subscribers.APILogger
{
    public class ApiClient
    {
        private readonly HttpClient _httpClient;

        public ApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task PostAsync<TRequest>(string url, TRequest data)
        {
            try
            {
                HttpResponseMessage response = await _httpClient.PostAsJsonAsync(url, data);

                if (response.IsSuccessStatusCode == false)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"API error: {response.StatusCode} - {errorContent}");
                }
            }
            catch (Exception ex)
            {
                // Handle/log exception as needed
                Console.WriteLine($"Exception during API call: {ex.Message}");
                throw;
            }
        }
    }
}
