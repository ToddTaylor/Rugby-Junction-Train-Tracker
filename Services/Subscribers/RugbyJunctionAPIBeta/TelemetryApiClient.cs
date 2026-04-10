using Polly;
using Polly.Retry;
using Services.Models;

namespace Services.Subscribers.RugbyJunctionAPIBeta
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
                var baseUrl = base.GetApUrl()?.TrimEnd('/');
                if (string.IsNullOrWhiteSpace(baseUrl))
                {
                    throw new InvalidOperationException("API base URL is not configured.");
                }

                var url = $"{baseUrl}/Telemetries";

                await _retryPolicy.ExecuteAsync(async _ =>
                {

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
                }, new Context { ["RequestUrl"] = url });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending telemetry to API: {ex.Message}");
            }
        }
    }
}
