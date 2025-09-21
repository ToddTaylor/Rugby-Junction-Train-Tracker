using Services.EventArgs;
using Services.Models;

namespace Services.Subscribers.LiveTrainTrackingAPI
{
    public class HotEotPacketSubscriber
    {
        private readonly AppSettings _appSettings;
        private readonly Subscriber _subscriber;

        public HotEotPacketSubscriber(AppSettings appSettings)
        {
            _appSettings = appSettings;
            _subscriber = appSettings.Subscribers
                .First(s => s.ID == Constants.SUBSCRIBER_ID);
        }

        private void OnHotEotPacketReceived(object sender, HotEotPacketEventArgs e)
        {
            var sendInvalidMessages = _subscriber.SendInvalidMessages;

            if (sendInvalidMessages == false && e.Packet.SRC == "INV") { return; }

            var alert = new Telemetry
            {
                AddressID = int.Parse(e.Packet.ID),
                BeaconID = _subscriber.Beacon.BeaconID,
                Detector = _subscriber.Beacon.DetectorID,
                Motion = (e.Packet.MOT.HasValue) ? Convert.ToBoolean(e.Packet.MOT) : null,
                Source = e.Packet.SRC,
                TrainID = null
            };

            var apiClient = new TelemetryApiClient(_appSettings);
            apiClient.SendTelemetryAsync(alert).GetAwaiter().GetResult();

            // Let web page respond to previous request before sending the next one.
            Thread.Sleep(100);
        }
    }
}
