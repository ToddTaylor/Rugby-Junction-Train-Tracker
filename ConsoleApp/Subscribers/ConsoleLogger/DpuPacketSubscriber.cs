using ConsoleApp.EventArgs;
using System.Text.Json;

namespace ConsoleApp.Subscribers.ConsoleLogger
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
