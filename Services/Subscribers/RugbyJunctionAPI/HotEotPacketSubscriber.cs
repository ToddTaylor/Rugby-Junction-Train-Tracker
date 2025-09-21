using Services.EventArgs;
using Services.Models;

namespace Services.Subscribers.RugbyJunctionAPI
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
                BeaconID = _subscriber.Beacon.BeaconID,
                AddressID = int.Parse(e.Packet.ID),
                TrainID = null,
                Moving = (e.Packet.MOT.HasValue) ? Convert.ToBoolean(e.Packet.MOT) : null,
                Source = e.Packet.SRC,
                Timestamp = e.Packet.TimeReceived
            };

            var apiClient = new TelemetryApiClient(_appSettings);
            apiClient.SendTelemetryAsync(alert).GetAwaiter().GetResult();

            // Let web page respond to previous request before sending the next one.
            Thread.Sleep(100);
        }
    }
}
