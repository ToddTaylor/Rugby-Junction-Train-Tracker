using Polly.Retry;
using Services.Models;

namespace Services.Subscribers.RugbyJunctionAPI
{
    public class TelemetryApiClient : ApiClient
    {
        private readonly AsyncRetryPolicy _retryPolicy;

        public TelemetryApiClient(AppSettings appSettings, string subscriberId) : base(appSettings)
        {
            _retryPolicy = PollyPolicies.GetExponentialBackoffPolicy(subscriberId);
        }

        public async Task SendTelemetryAsync(Telemetry telemetry)
        {
            try
            {
                await _retryPolicy.ExecuteAsync(async () =>
                {
                    var url = $"{base.GetApUrl()}/Telemetries";

                    var request = new HttpRequestMessage(HttpMethod.Post, url)
                    {
                        Content = System.Net.Http.Json.JsonContent.Create(telemetry)
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
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending telemetry to API: {ex.Message}");
            }
        }
    }
}
