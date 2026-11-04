using Services.Diagnostics;
using Services.Models;
using Services.RemoteCommands;

namespace Services.Subscribers.RugbyJunctionAPIBeta
{
    public class BeaconHealthService : IDisposable
    {
        private readonly AppSettings _appSettings;
        private readonly BeaconApiClient _beaconApiClient;
        private readonly MachineDiagnosticsCollector _diagnosticsCollector;
        private readonly RemoteCommandHandler _commandHandler;
        private readonly Subscriber _subscriber;
        private Timer? _timer;

        public BeaconHealthService(AppSettings appSettings)
        {
            var subscriber = appSettings.Subscribers
                .First(s => s.ID == Constants.SUBSCRIBER_ID);

            _appSettings = appSettings;
            _beaconApiClient = new BeaconApiClient(appSettings, subscriber);
            _diagnosticsCollector = new MachineDiagnosticsCollector(new WindowsSystemProbe());
            _commandHandler = RemoteCommandHandler.CreateDefault(appSettings, message => WriteLine(ConsoleColor.Cyan, message));

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
                var diagnostics = _diagnosticsCollector.Collect(_appSettings, _subscriber, DateTime.UtcNow);
                var command = await _beaconApiClient.SendBeaconHealthAsync(diagnostics);

                WriteLine(ConsoleColor.DarkGray, "Beacon health sent successfully.");

                if (command != null)
                {
                    // Runs apart from the timer, so a long restart-apps run does not hold up the next health check.
                    _ = Task.Run(() => HandleCommandAsync(command));
                }
            }
            catch (Exception ex)
            {
                WriteLine(ConsoleColor.Red, $"Error in beacon health service: {ex.Message}");
            }
        }

        private async Task HandleCommandAsync(RemoteCommand command)
        {
            try
            {
                await _commandHandler.HandleAsync(_subscriber.ID, command, _beaconApiClient.ReportCommandAsync);
            }
            catch (Exception ex)
            {
                WriteLine(ConsoleColor.Red, $"Error handling remote {command.Type} command: {ex.Message}");
            }
        }

        private void WriteLine(ConsoleColor color, string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy/MM/dd-HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            Console.ForegroundColor = color;
            Console.WriteLine($"{timestamp} [{_subscriber.ID}] {message}");
            Console.ResetColor();
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
