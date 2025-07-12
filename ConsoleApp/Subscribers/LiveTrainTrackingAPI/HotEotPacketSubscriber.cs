using ConsoleApp.EventArgs;
using Microsoft.Extensions.Configuration;

namespace ConsoleApp.Subscribers.LiveTrainTrackerAPI
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
                AddressID = int.Parse(e.Packet.ID),
                BeaconID = configuration.GetValue<string>("Subscribers:0:Beacon:BeaconID"),
                Detector = configuration.GetValue<string>("Subscribers:0:Beacon:DetectorID"),
                Motion = (e.Packet.MOT.HasValue) ? Convert.ToBoolean(e.Packet.MOT) : null,
                Source = e.Packet.SRC,
                TrainID = null
            };

            var apiClient = new TelemetryApiClient();
            apiClient.SendTelemetryAsync(alert).GetAwaiter().GetResult();

            // Let web page respond to previous request before sending the next one.
            Thread.Sleep(100);
        }
    }
}
