using Services.EventArgs;
using Services.Models;

namespace Services.Subscribers.LiveTrainTrackingAPI
{
    public class HotEotPacketSubscriber
    {
        private readonly Subscriber _subscriber;
        private readonly TelemetryThrottleService _throttleService;
        private readonly TelemetryApiClient _apiClient;
        private readonly TelemetrySendQueue _sendQueue;

        public HotEotPacketSubscriber(AppSettings appSettings)
        {
            _subscriber = appSettings.Subscribers
                .First(s => s.ID == Constants.SUBSCRIBER_ID);
            _throttleService = new TelemetryThrottleService(_subscriber.TelemetryThrottleIntervalSeconds);
            _apiClient = new TelemetryApiClient(appSettings);
            _sendQueue = TelemetrySendQueue.ForSubscriber(Constants.SUBSCRIBER_ID);
        }

        private void OnHotEotPacketReceived(object sender, HotEotPacketEventArgs e)
        {
            var sendInvalidMessages = _subscriber.SendInvalidMessages;

            if (sendInvalidMessages == false && e.Packet.SRC == "INV") { return; }

            if (!int.TryParse(e.Packet.ID, out var addressId)) { return; }

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
                AddressID = addressId,
                BeaconID = _subscriber.Beacon.BeaconID,
                Detector = _subscriber.Beacon.DetectorID,
                Motion = IsMoving(e.Packet),
                Source = e.Packet.SRC,
                TrainID = null
            };

            // Sent from this subscriber's own queue, so a slow or unreachable API does not hold up the log reader
            // or the other subscribers.
            _sendQueue.Enqueue(() => _apiClient.SendTelemetryAsync(alert));
        }

        /// <summary>
        /// If a EOT's brake pressure is below 85 PSI or the motion flag is zero, the train is not moving.
        /// 
        /// Brake pressure information is only provided via BRK status messages.
        /// </summary>
        /// <param name="hotEotpacket">HOT/EOT packet containing brake pressure and motion information.</param>
        /// <returns>Returns true if train is moving, else false.</returns>
        private bool? IsMoving(HotEotPacket hotEotpacket)
        {
            var minimumBrakePSI = 85;

            if (hotEotpacket.BP.HasValue)
            {
                return hotEotpacket.BP.Value >= minimumBrakePSI;
            }

            var parkingBrakeOn = 1;

            if (hotEotpacket.MOT.HasValue && hotEotpacket.MOT.Value == parkingBrakeOn)
            {
                return Convert.ToBoolean(hotEotpacket.MOT.Value);
            }

            return null;
        }
    }
}
