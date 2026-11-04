using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Polly;
using Polly.Retry;
using Services.Models;
using Services.RemoteCommands;

namespace Services.Subscribers.RugbyJunctionAPIBeta
{
    public class BeaconApiClient : ApiClient
    {
        private const string DeviceKeyHeaderName = "X-Beacon-Key";

        private static readonly JsonSerializerOptions ResponseJsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly Beacon _beacon;
        private readonly Subscriber _subscriber;
        private readonly AsyncRetryPolicy _retryPolicy;

        public BeaconApiClient(AppSettings appSettings, Subscriber subscriber) : base(appSettings)
        {
            _subscriber = subscriber;
            _beacon = subscriber.Beacon;

            _retryPolicy = PollyPolicies.GetExponentialBackoffPolicy(_subscriber.ID);
        }

        /// <summary>
        /// Sends the health check and diagnostics report. Returns the reboot or restart-apps command the server has
        /// waiting for this PC, if any.
        /// </summary>
        public async Task<RemoteCommand?> SendBeaconHealthAsync(BeaconDiagnostics diagnostics)
        {
            var url = $"{GetBaseUrl()}/Beacons/Health/{GetRequiredBeaconID()}";
            RemoteCommand? command = null;

            await _retryPolicy.ExecuteAsync(async _ =>
            {
                using var request = CreateRequest(url, diagnostics);
                using var response = await base._httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode == false)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"API error: {response.StatusCode} - {errorContent}");
                }

                command = response.StatusCode == HttpStatusCode.OK
                    ? (await response.Content.ReadFromJsonAsync<BeaconHealthResponse>(ResponseJsonOptions))?.Command
                    : null;
            }, new Context { ["RequestUrl"] = url });

            return command;
        }

        /// <summary>
        /// Reports on a command. Returns Refused when the server answers 409, meaning the command must not run.
        /// </summary>
        public async Task<RemoteCommandReportResult> ReportCommandAsync(int commandId, string status, string? message)
        {
            var url = $"{GetBaseUrl()}/Beacons/Health/{GetRequiredBeaconID()}/commands/{commandId}";
            var result = RemoteCommandReportResult.Accepted;

            await _retryPolicy.ExecuteAsync(async _ =>
            {
                using var request = CreateRequest(url, new { status, message });
                using var response = await base._httpClient.SendAsync(request);

                if (response.StatusCode == HttpStatusCode.Conflict)
                {
                    result = RemoteCommandReportResult.Refused;
                    return;
                }

                if (response.IsSuccessStatusCode == false)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"API error: {response.StatusCode} - {errorContent}");
                }
            }, new Context { ["RequestUrl"] = url });

            return result;
        }

        private HttpRequestMessage CreateRequest(string url, object body)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(body)
            };

            request.Headers.Add("X-Api-Key", base.GetApiKey());

            if (!string.IsNullOrWhiteSpace(_beacon.DeviceKey))
            {
                request.Headers.Add(DeviceKeyHeaderName, _beacon.DeviceKey.Trim());
            }

            return request;
        }

        private string GetBaseUrl()
        {
            var baseUrl = base.GetApUrl()?.TrimEnd('/');
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new InvalidOperationException("API base URL is not configured.");
            }

            return baseUrl;
        }

        private string GetRequiredBeaconID() => GetBeaconID() ?? throw new InvalidOperationException("Beacon ID is not configured.");

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
