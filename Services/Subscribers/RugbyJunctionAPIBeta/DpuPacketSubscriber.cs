using Services.EventArgs;
using Services.Models;

namespace Services.Subscribers.RugbyJunctionAPIBeta
{
    public class DpuPacketSubscriber
    {
        private readonly AppSettings _appSettings;
        private readonly Subscriber _subscriber;
        private readonly TelemetryThrottleService _throttleService;

        // Address IDs that should be ignored.
        private readonly int[] addressIdBlackList = new int[] { 0, 999999 };

        public DpuPacketSubscriber(AppSettings appSettings)
        {
            _appSettings = appSettings;
            _subscriber = appSettings.Subscribers
                .First(s => s.ID == Constants.SUBSCRIBER_ID);
            _throttleService = new TelemetryThrottleService(_subscriber.TelemetryThrottleIntervalSeconds);
        }

        private void OnDpuPacketReceived(object sender, DpuPacketEventArgs e)
        {
            if (!int.TryParse(e.Packet.ADDR, out var addressId)) { return; }

            if (addressIdBlackList.Contains(addressId)) { return; }

            var sendInvalidMessages = _subscriber.SendInvalidMessages;

            if (sendInvalidMessages == false && e.Packet.ADDR == "INV") { return; }

            // Check if this message should be throttled based on AddressID
            if (!_throttleService.ShouldSendMessage(addressId))
            {
                var utc = e.Packet.TimeReceived.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(e.Packet.TimeReceived, DateTimeKind.Utc)
                    : e.Packet.TimeReceived;

                var localTimestamp = utc.ToLocalTime()
                    .ToString("yyyy/MM/dd-HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"{localTimestamp}  0.0 {addressId} << Throttled");
                Console.ResetColor();

                return;
            }

            var alert = new Telemetry
            {
                BeaconID = _subscriber.Beacon.BeaconID,
                AddressID = int.Parse(e.Packet.ADDR),
                TrainID = int.Parse(e.Packet.TRID),
                BrakePipePressure = e.Packet.BP,
                Moving = null,
                Source = "DPU",
                Timestamp = e.Packet.TimeReceived
            };

            var apiClient = new TelemetryApiClient(_appSettings, Constants.SUBSCRIBER_ID);
            apiClient.SendTelemetryAsync(alert).GetAwaiter().GetResult();

            // Let web page respond to previous request before sending the next one.
            Thread.Sleep(100);
        }
    }
}
