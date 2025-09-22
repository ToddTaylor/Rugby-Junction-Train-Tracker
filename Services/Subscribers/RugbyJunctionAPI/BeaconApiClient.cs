using Polly.Retry;
using Services.Models;

namespace Services.Subscribers.RugbyJunctionAPI
{
    public class BeaconApiClient : ApiClient
    {
        private readonly Beacon _beacon;
        private readonly Subscriber _subscriber;
        private readonly AsyncRetryPolicy _retryPolicy;

        public BeaconApiClient(AppSettings appSettings, Subscriber subscriber) : base(appSettings)
        {
            _subscriber = subscriber;
            _beacon = subscriber.Beacon;

            _retryPolicy = PollyPolicies.GetExponentialBackoffPolicy(_subscriber.ID);
        }

        public async Task SendBeaconHealthAsync()
        {
            await _retryPolicy.ExecuteAsync(async () =>
            {
                var beaconID = this.GetBeaconID() ?? throw new InvalidOperationException("Beacon ID is not configured.");

                var url = $"{base.GetApUrl()}/Beacons/Health/{beaconID}";

                var request = new HttpRequestMessage(HttpMethod.Post, url);

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

        protected string? GetBeaconID()
        {
            try
            {
                return _beacon.BeaconID;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception during Beacon ID retrieval from configuration: {ex.Message}");
                throw;
            }
        }
    }
}
