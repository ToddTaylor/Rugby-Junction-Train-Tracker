using Services.EventArgs;
using Services.Models;

namespace Services.Subscribers.LiveTrainTrackingAPI
{
    public class DpuPacketSubscriber
    {
        private readonly AppSettings _appSettings;
        private readonly Subscriber _subscriber;
        private readonly TelemetryThrottleService _throttleService;

        public DpuPacketSubscriber(AppSettings appSettings)
        {
            _appSettings = appSettings;
            _subscriber = appSettings.Subscribers
                .First(s => s.ID == Constants.SUBSCRIBER_ID);
            _throttleService = new TelemetryThrottleService(_subscriber.TelemetryThrottleIntervalSeconds);
        }

        private void OnDpuPacketReceived(object sender, DpuPacketEventArgs e)
        {
            var sendInvalidMessages = _subscriber.SendInvalidMessages;

            if (sendInvalidMessages == false && e.Packet.ADDR == "INV") { return; }

            if (!int.TryParse(e.Packet.ADDR, out var addressId)) { return; }

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
                AddressID = int.Parse(e.Packet.ADDR),
                BeaconID = _subscriber.Beacon.BeaconID,
                Detector = _subscriber.Beacon.DetectorID,
                Motion = IsMoving(e.Packet),
                Source = "DPU",
                TrainID = int.Parse(e.Packet.TRID),
            };

            var apiClient = new TelemetryApiClient(_appSettings);
            apiClient.SendTelemetryAsync(alert).GetAwaiter().GetResult();

            // Let web page respond to previous request before sending the next one.
            Thread.Sleep(100);
        }

        /// <summary>
        /// If a DPU's brake pressure is below 85 PSI or the parking brake is applied, the DPU is not moving.
        /// 
        /// Brake pressure information is only provided via DPU status messages (TP = ST && OR = RM).
        /// 
        /// Parking brake value of 0 does not mean the locomotive is moving, it means the parking brake is not applied.
        /// </summary>
        /// <param name="dpuPacket">DPU packet containing brake pressure and parking brake information.</param>
        /// <returns>Returns true if DPU is moving, else false.</returns>
        private bool? IsMoving(DpuPacket dpuPacket)
        {
            var minimumBrakePSI = 85;

            if (dpuPacket.BP.HasValue)
            {
                return dpuPacket.BP.Value >= minimumBrakePSI;
            }

            var parkingBrakeOn = 1;

            if (dpuPacket.PRK.HasValue && dpuPacket.PRK.Value == parkingBrakeOn)
            {
                return false;
            }

            return null;
        }
    }
}
