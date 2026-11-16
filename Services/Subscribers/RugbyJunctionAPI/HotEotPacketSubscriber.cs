using Services.EventArgs;
using Services.Models;

namespace Services.Subscribers.RugbyJunctionAPI
{
    public class HotEotPacketSubscriber
    {
        private readonly Subscriber _subscriber;
        private readonly TelemetryThrottleService _throttleService;
        private readonly TelemetryApiClient _apiClient;
        private readonly TelemetrySendQueue _sendQueue;

        // Address IDs that should be ignored.
        private readonly int[] addressIdBlackList = new int[] { 0, 999999 };

        public HotEotPacketSubscriber(AppSettings appSettings)
        {
            _subscriber = appSettings.Subscribers
                .First(s => s.ID == Constants.SUBSCRIBER_ID);
            _throttleService = new TelemetryThrottleService(_subscriber.TelemetryThrottleIntervalSeconds);
            _apiClient = new TelemetryApiClient(appSettings, Constants.SUBSCRIBER_ID);
            _sendQueue = TelemetrySendQueue.ForSubscriber(Constants.SUBSCRIBER_ID);
        }

        private void OnHotEotPacketReceived(object sender, HotEotPacketEventArgs e)
        {
            if (!int.TryParse(e.Packet.ID, out var addressId)) { return; }

            if (addressIdBlackList.Contains(addressId)) { return; }

            var sendInvalidMessages = _subscriber.SendInvalidMessages;

            if (sendInvalidMessages == false && e.Packet.SRC == "INV") { return; }

            // Check if this message should be throttled based on AddressID
            if (!_throttleService.ShouldSendMessage(addressId))
            {
                var utc = e.Packet.TimeReceived.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(e.Packet.TimeReceived, DateTimeKind.Utc)
                    : e.Packet.TimeReceived;

                var localTimestamp = utc.ToLocalTime()
                    .ToString("yyyy/MM/dd-HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);

                var source = e.Packet.SRC;

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"{localTimestamp}  0.0 {source} {addressId} << Throttled");
                Console.ResetColor();

                return;
            }

            var alert = new Telemetry
            {
                BeaconID = _subscriber.Beacon.BeaconID,
                AddressID = addressId,
                TrainID = null,
                BrakePipePressure = e.Packet.BP,
                Moving = (e.Packet.MOT.HasValue) ? Convert.ToBoolean(e.Packet.MOT) : null,
                Source = e.Packet.SRC,
                Timestamp = e.Packet.TimeReceived
            };

            // Sent from this subscriber's own queue, so a slow or unreachable API does not hold up the log reader
            // or the other subscribers.
            _sendQueue.Enqueue(() => _apiClient.SendTelemetryAsync(alert));
        }
    }
}
