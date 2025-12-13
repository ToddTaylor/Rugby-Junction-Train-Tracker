using Services.Models;

namespace Services.Subscribers.RugbyJunctionAPI
{
    public class BeaconHealthService : IDisposable
    {
        private readonly BeaconApiClient _beaconApiClient;
        private readonly Subscriber _subscriber;
        private Timer? _timer;

        public BeaconHealthService(AppSettings appSettings)
        {
            var subscriber = appSettings.Subscribers
                .First(s => s.ID == Constants.SUBSCRIBER_ID);

            _beaconApiClient = new BeaconApiClient(appSettings, subscriber);

            _subscriber = subscriber;
        }

        public void Start()
        {
            // Call immediately, then every configured interval
            int healthPingIntervalMinutes = _subscriber.Beacon.HealthPingIntervalMinutes;
            _timer = new Timer(async _ => await SendHealthAsync(), null, TimeSpan.Zero, TimeSpan.FromMinutes(healthPingIntervalMinutes));
        }

        private async Task SendHealthAsync()
        {
            try
            {
                await _beaconApiClient.SendBeaconHealthAsync();

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"[{DateTime.Now}] [{_subscriber.ID}] Beacon health sent at {DateTime.Now}");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[{DateTime.Now}] [{_subscriber.ID}] Error in beacon health service: {ex.Message}");
                Console.ResetColor();
            }
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}