using Microsoft.Extensions.Configuration;
using Services.EventArgs;

namespace Services.Subscribers.RugbyJunctionAPI
{
    public class HotEotPacketSubscriber
    {
        private readonly IConfiguration configuration;

        public HotEotPacketSubscriber()
        {
            configuration = ConfigurationHelper.LoadConfiguration();
        }

        private void OnHotEotPacketReceived(object sender, HotEotPacketEventArgs e)
        {
            var sendInvalidMessages = configuration.GetValue<bool>("SendInvalidMessages");

            if (sendInvalidMessages == false && e.Packet.SRC == "INV") { return; }

            var alert = new Telemetry
            {
                BeaconID = configuration.GetValue<int>("Subscribers:0:Beacon:BeaconID"),
                AddressID = int.Parse(e.Packet.ID),
                TrainID = null,
                Moving = (e.Packet.MOT.HasValue) ? Convert.ToBoolean(e.Packet.MOT) : null,
                Source = e.Packet.SRC,
                Timestamp = e.Packet.TimeReceived
            };

            var apiClient = new TelemetryApiClient();
            apiClient.SendTelemetryAsync(alert).GetAwaiter().GetResult();

            // Let web page respond to previous request before sending the next one.
            Thread.Sleep(100);
        }
    }
}
