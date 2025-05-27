using Microsoft.Extensions.Configuration;

namespace ConsoleApp.Subscribers.APILogger
{
    public class BeaconHealthService : IDisposable
    {
        private readonly int _beaconHealthIntervalMinutes;
        private readonly BeaconApiClient _beaconApiClient;
        private Timer? _timer;

        public BeaconHealthService(BeaconApiClient beaconApiClient, IConfiguration configuration)
        {
            _beaconApiClient = beaconApiClient;

            // Read from config: "Beacon:HealthPingIntervalMinutes"
            _beaconHealthIntervalMinutes = configuration.GetValue<int>("Beacon:HealthPingIntervalMinutes", 15);
        }

        public void Start()
        {
            // Call immediately, then every configured interval
            _timer = new Timer(async _ => await SendHealthAsync(), null, TimeSpan.Zero, TimeSpan.FromMinutes(_beaconHealthIntervalMinutes));
        }

        private async Task SendHealthAsync()
        {
            try
            {
                await _beaconApiClient.SendBeaconHealthAsync();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Beacon health sent at {DateTime.Now}");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error in BeaconHealthService: {ex.Message}");
                Console.ResetColor();
            }
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}