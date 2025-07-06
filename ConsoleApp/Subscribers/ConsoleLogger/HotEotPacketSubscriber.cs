using ConsoleApp.EventArgs;
using System.Text.Json;

namespace ConsoleApp.Subscribers.ConsoleLogger
{
    public class HotEotPacketSubscriber
    {
        private void OnHotEotPacketReceived(object sender, HotEotPacketEventArgs e)
        {
            var jsonString = JsonSerializer.Serialize(e.Packet);

            Console.WriteLine($"HOT/EOT packet received: {jsonString}");
        }
    }
}
