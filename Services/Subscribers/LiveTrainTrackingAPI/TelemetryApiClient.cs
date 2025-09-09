using System.Net.Http.Json;

namespace Services.Subscribers.LiveTrainTrackingAPI
{
    public class TelemetryApiClient : ApiClient
    {
        public TelemetryApiClient() : base()
        {
        }

        public async Task SendTelemetryAsync(Telemetry telemetry)
        {
            try
            {
                var url = base.GetTelemetryUrl();

                var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = JsonContent.Create(telemetry)
                };

                // Add the API key to the headers
                var _apiKey = base.GetApiKey();
                request.Headers.Add("X-Api-Key", _apiKey);

                var response = await base._httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode == false)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"API error: {response.StatusCode} - {errorContent}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending telemetry to API: {ex.Message}");
            }
        }
    }
}
