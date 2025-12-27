using Services.Models;

namespace Services.Subscribers.LiveTrainTrackingAPI
{
    public class BeaconHealthService : IDisposable
    {
        private readonly int _healthPingIntervalMinutes;
        private readonly BeaconApiClient _beaconApiClient;
        private readonly Subscriber _subscriber;
        private Timer? _timer;

        public BeaconHealthService(AppSettings appSettings)
        {
            _beaconApiClient = new BeaconApiClient(appSettings);

            _subscriber = appSettings.Subscribers
                .First(s => s.ID == Constants.SUBSCRIBER_ID);

            _healthPingIntervalMinutes = _subscriber.Beacon.HealthPingIntervalMinutes;
        }

        public void Start()
        {
            // Call immediately, then every configured interval
            _timer = new Timer(async _ => await SendHealthAsync(), null, TimeSpan.Zero, TimeSpan.FromMinutes(_healthPingIntervalMinutes));
        }

        private async Task SendHealthAsync()
        {
            var timestamp = DateTime.Now.ToString("yyyy/MM/dd-HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);

            try
            {
                await _beaconApiClient.SendBeaconHealthAsync();

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"{timestamp} [{_subscriber.ID}] Beacon health sent successfully.");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"{timestamp} [{_subscriber.ID}] Error in beacon health service: {ex.Message}");
                Console.ResetColor();
            }
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}