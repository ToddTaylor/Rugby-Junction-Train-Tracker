using ConsoleApp.EventArgs;
using Microsoft.Extensions.Configuration;

namespace ConsoleApp.Subscribers.APILogger
{
    public class HotEotPacketSubscriber
    {
        private readonly IConfiguration configuration;

        public HotEotPacketSubscriber()
        {
            configuration = LoadConfiguration();
        }

        private void OnHotEotPacketReceived(object sender, HotEotPacketEventArgs e)
        {
            var sendInvalidMessages = configuration.GetValue<bool>("SendInvalidMessages");

            if (sendInvalidMessages == false && e.Packet.SRC == "INV") { return; }

            var alert = new Alert
            {
                BeaconID = configuration.GetValue<string>("Beacon:BeaconID"),
                AddressID = int.Parse(e.Packet.ID),
                TrainID = null,
                Latitude = configuration.GetValue<double>("Beacon:Latitude"),
                Longitude = configuration.GetValue<double>("Beacon:Longitude"),
                Moving = (e.Packet.MOT.HasValue) ? Convert.ToBoolean(e.Packet.MOT) : null,
                Source = e.Packet.SRC,
                Timestamp = e.Packet.TimeReceived
            };

            var apiClient = new RailroadTelemetryApiClient();
            apiClient.SendAlertAsync(alert).GetAwaiter().GetResult();

            // Let web page respond to previous request before sending the next one.
            Thread.Sleep(100);
        }

        private static IConfiguration LoadConfiguration()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            return builder.Build();
        }
    }
}
