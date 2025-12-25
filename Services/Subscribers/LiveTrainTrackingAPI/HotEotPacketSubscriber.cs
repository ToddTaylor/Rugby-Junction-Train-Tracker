using Services.EventArgs;
using Services.Models;

namespace Services.Subscribers.LiveTrainTrackingAPI
{
    public class HotEotPacketSubscriber
    {
        private readonly AppSettings _appSettings;
        private readonly Subscriber _subscriber;
        private readonly TelemetryThrottleService _throttleService;

        public HotEotPacketSubscriber(AppSettings appSettings)
        {
            _appSettings = appSettings;
            _subscriber = appSettings.Subscribers
                .First(s => s.ID == Constants.SUBSCRIBER_ID);
            _throttleService = new TelemetryThrottleService(_subscriber.TelemetryThrottleIntervalSeconds);
        }

        private void OnHotEotPacketReceived(object sender, HotEotPacketEventArgs e)
        {
            var sendInvalidMessages = _subscriber.SendInvalidMessages;

            if (sendInvalidMessages == false && e.Packet.SRC == "INV") { return; }

            if (!int.TryParse(e.Packet.ID, out var addressId)) { return; }

            // Check if this message should be throttled based on AddressID
            if (!_throttleService.ShouldSendMessage(addressId))
            {
                var timestamp = e.Packet.TimeReceived.ToString("yyyy/MM/dd-HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
                var source = e.Packet.SRC;

                Console.ForegroundColor = ConsoleColor.DarkGray; 
                Console.WriteLine($"{timestamp}  0.0 {source} {addressId} << Throttled");
                Console.ResetColor();

                return;
            }

            var alert = new Telemetry
            {
                AddressID = int.Parse(e.Packet.ID),
                BeaconID = _subscriber.Beacon.BeaconID,
                Detector = _subscriber.Beacon.DetectorID,
                Motion = IsMoving(e.Packet),
                Source = e.Packet.SRC,
                TrainID = null
            };

            var apiClient = new TelemetryApiClient(_appSettings);
            apiClient.SendTelemetryAsync(alert).GetAwaiter().GetResult();

            // Let web page respond to previous request before sending the next one.
            Thread.Sleep(100);
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
