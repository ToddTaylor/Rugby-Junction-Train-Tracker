using ConsoleApp.EventArgs;
using ConsoleApp.Models;
using Microsoft.Extensions.Configuration;

namespace ConsoleApp.Subscribers.APILogger
{
    public class DpuPacketSubscriber
    {
        private readonly IConfiguration configuration;

        public DpuPacketSubscriber()
        {
            configuration = ConfigurationHelper.LoadConfiguration();
        }

        private void OnDpuPacketReceived(object sender, DpuPacketEventArgs e)
        {
            var sendInvalidMessages = configuration.GetValue<bool>("SendInvalidMessages");

            if (sendInvalidMessages == false && e.Packet.ADDR == "INV") { return; }

            var alert = new Telemetry
            {
                BeaconID = configuration.GetValue<int>("Beacon:BeaconID"),
                AddressID = int.Parse(e.Packet.ADDR),
                TrainID = int.Parse(e.Packet.TRID),
                Moving = this.IsMoving(e.Packet),
                Source = "DPU",
                Timestamp = e.Packet.TimeReceived
            };

            var apiClient = new RailroadTelemetryApiClient();
            apiClient.SendTelemetryAsync(alert).GetAwaiter().GetResult();

            // Let web page respond to previous request before sending the next one.
            Thread.Sleep(100);
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
            var minimumBrakePSI = 70;

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
