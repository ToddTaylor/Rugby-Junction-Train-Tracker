using Services.EventArgs;
using Services.Models;

namespace Services.Subscribers.RugbyJunctionAPIBeta
{
    public class DpuPacketSubscriber
    {
        private readonly Subscriber _subscriber;
        private readonly TelemetryThrottleService _throttleService;
        private readonly TelemetryApiClient _apiClient;
        private readonly TelemetrySendQueue _sendQueue;

        // Address IDs that should be ignored.
        private readonly int[] addressIdBlackList = new int[] { 0, 999999 };

        public DpuPacketSubscriber(AppSettings appSettings)
        {
            _subscriber = appSettings.Subscribers
                .First(s => s.ID == Constants.SUBSCRIBER_ID);
            _throttleService = new TelemetryThrottleService(_subscriber.TelemetryThrottleIntervalSeconds);
            _apiClient = new TelemetryApiClient(appSettings, Constants.SUBSCRIBER_ID);
            _sendQueue = TelemetrySendQueue.ForSubscriber(Constants.SUBSCRIBER_ID);
        }

        private void OnDpuPacketReceived(object sender, DpuPacketEventArgs e)
        {
            // Also drops ADDR "INV", which is not a number.
            if (!int.TryParse(e.Packet.ADDR, out var addressId)) { return; }

            if (addressIdBlackList.Contains(addressId)) { return; }

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
                AddressID = addressId,
                // TRID is "---" in some packets; the deserializer turns that into null.
                TrainID = int.TryParse(e.Packet.TRID, out var trainId) ? trainId : null,
                BrakePipePressure = e.Packet.BP,
                Moving = null,
                Source = "DPU",
                Timestamp = e.Packet.TimeReceived
            };

            // Sent from this subscriber's own queue, so a slow or unreachable API does not hold up the log reader
            // or the other subscribers.
            _sendQueue.Enqueue(() => _apiClient.SendTelemetryAsync(alert));
        }
    }
}
