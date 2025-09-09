using Microsoft.Extensions.Configuration;

namespace Services.Subscribers.LiveTrainTrackingAPI
{
    public class BeaconHealthService : IDisposable
    {
        private readonly string? _beaconID;
        private readonly int _healthPingIntervalMinutes;
        private readonly BeaconApiClient _beaconApiClient;
        private readonly IConfiguration _configuration;
        private Timer? _timer;

        public BeaconHealthService()
        {
            _beaconApiClient = new BeaconApiClient();

            _configuration = ConfigurationHelper.LoadConfiguration();

            _beaconID = _configuration.GetValue<string>("Subscribers:0:ID");
            _healthPingIntervalMinutes = _configuration.GetValue<int>("Subscribers:0:Beacon:HealthPingIntervalMinutes", 15);
        }

        public void Start()
        {
            // Call immediately, then every configured interval
            _timer = new Timer(async _ => await SendHealthAsync(), null, TimeSpan.Zero, TimeSpan.FromMinutes(_healthPingIntervalMinutes));
        }

        private async Task SendHealthAsync()
        {

            try
            {
                await _beaconApiClient.SendBeaconHealthAsync();

                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine($"[{_beaconID}] Beacon health sent at {DateTime.Now}");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[{_beaconID}] Error in beacon health service: {ex.Message}");
                Console.ResetColor();
            }
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}