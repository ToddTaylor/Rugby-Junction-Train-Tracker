namespace ConsoleApp.Subscribers.APILogger
{
    public class RailroadTelemetryApiClient : ApiClient
    {
        public RailroadTelemetryApiClient() : base()
        {
        }

        public async Task SendTelemetryAsync(Telemetry telemetry)
        {
            try
            {
                await PostAsync<Telemetry>(telemetry);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending alert: {ex.Message}");
            }
        }
    }
}
