using Services.Models;

namespace Services.Subscribers.RugbyJunctionAPI
{
    public class BeaconApiClient : ApiClient
    {
        private readonly Beacon _beacon;

        public BeaconApiClient(AppSettings appSettings) : base(appSettings)
        {
            _beacon = appSettings.Subscribers
                .First(s => s.ID == Constants.SUBSCRIBER_ID)
                .Beacon;
        }

        public async Task SendBeaconHealthAsync()
        {
            try
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending beacon health ID to API: {ex.Message}");
            }
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
