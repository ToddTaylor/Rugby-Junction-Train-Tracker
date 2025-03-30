using ConsoleApp.EventArgs;
using ConsoleApp.Models;
using Microsoft.Extensions.Configuration;

namespace ConsoleApp.Subscribers.APILogger
{
    public class DpuPacketSubscriber
    {
        private readonly IConfiguration configuration;

        private RailroadTelemetryApiClient apiClient = new RailroadTelemetryApiClient();

        public DpuPacketSubscriber()
        {
            configuration = LoadConfiguration();
        }

        private void OnDpuPacketReceived(object sender, DpuPacketEventArgs e)
        {
            var sendInvalidMessages = configuration.GetValue<bool>("SendInvalidMessages");

            if (sendInvalidMessages == false && e.Packet.ADDR == "INV") { return; }

            var alert = new Alert
            {
                BeaconID = configuration.GetValue<string>("Beacon:BeaconID"),
                AddressID = int.Parse(e.Packet.ADDR),
                TrainID = int.Parse(e.Packet.TRID),
                Latitude = configuration.GetValue<double>("Beacon:Latitude"),
                Longitude = configuration.GetValue<double>("Beacon:Longitude"),
                Moving = this.IsMoving(e.Packet),
                Source = "DPU",
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

        /// <summary>
        /// If a DPU's brake pressure is below 85 PSI or the parking brake is applied, the DPU is not moving.
        /// 
        /// Brake pressure information is only provided via DPU status messages (TP = ST && OR = RM).
        /// </summary>
        /// <param name="dpuPacket">DPU packet containing brake pressure and parking brake information.</param>
        /// <returns>Returns true if DPU is moving, else false.</returns>
        private bool? IsMoving(DpuPacket dpuPacket)
        {
            var parkingBrakeOff = 0;
            var minimumBrakePSI = 85;

            if (dpuPacket.BP.HasValue)
            {
                return (dpuPacket.BP.Value >= minimumBrakePSI);
            }

            if (dpuPacket.PRK.HasValue)
            {
                return (dpuPacket.PRK.Value == parkingBrakeOff);
            }

            return null;
        }
    }
}
