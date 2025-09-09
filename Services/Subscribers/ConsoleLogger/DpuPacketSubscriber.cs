using Services.EventArgs;
using System.Text.Json;

namespace Services.Subscribers.ConsoleLogger
{
    public class DpuPacketSubscriber
    {
        private void OnDpuPacketReceived(object sender, DpuPacketEventArgs e)
        {
            var jsonString = JsonSerializer.Serialize(e.Packet);

            Console.WriteLine($"DPU packet received: {jsonString}");
        }
    }
}
