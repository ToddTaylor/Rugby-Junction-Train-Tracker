namespace ConsoleApp.Subscribers.APILogger
{
    public class RailroadTelemetryApiClient : ApiClient
    {
        public RailroadTelemetryApiClient() : base(new HttpClient())
        {
        }

        public async Task SendAlertAsync(Alert alert)
        {
            var apiUrl = "https://localhost:44331/api/v1/Telemetry/Alert";

            try
            {
                await PostAsync<Alert>(apiUrl, alert);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending alert: {ex.Message}");
            }
        }
    }
}
