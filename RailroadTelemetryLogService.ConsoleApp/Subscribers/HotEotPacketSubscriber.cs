using ConsoleApp.EventArgs;
using System.Text.Json;

namespace RailroadTelemetryLogService.ConsoleApp
{
    public class HotEotPacketSubscriber
    {
        private void OnHotPacketReceived(object sender, HotPacketEventArgs e)
        {
            var jsonString = JsonSerializer.Serialize(e.HotPacket);

            Console.WriteLine($"HotPacket received: {jsonString}");
        }
    }
}
