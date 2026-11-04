using Services.Diagnostics;
using Services.Models;

namespace Services.Subscribers.RugbyJunctionAPIBeta
{
    public class BeaconHealthService : IDisposable
    {
        private readonly AppSettings _appSettings;
        private readonly BeaconApiClient _beaconApiClient;
        private readonly MachineDiagnosticsCollector _diagnosticsCollector;
        private readonly Subscriber _subscriber;
        private Timer? _timer;

        public BeaconHealthService(AppSettings appSettings)
        {
            var subscriber = appSettings.Subscribers
                .First(s => s.ID == Constants.SUBSCRIBER_ID);

            _appSettings = appSettings;
            _beaconApiClient = new BeaconApiClient(appSettings, subscriber);
            _diagnosticsCollector = new MachineDiagnosticsCollector(new WindowsSystemProbe());

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
            var timestamp = DateTime.Now.ToString("yyyy/MM/dd-HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);

            try
            {
                var diagnostics = _diagnosticsCollector.Collect(_appSettings, _subscriber, DateTime.UtcNow);
                await _beaconApiClient.SendBeaconHealthAsync(diagnostics);

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