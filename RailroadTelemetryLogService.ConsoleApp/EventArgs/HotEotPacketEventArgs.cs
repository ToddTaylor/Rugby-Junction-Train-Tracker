using ConsoleApp.Models;

namespace ConsoleApp.EventArgs;

public class HotEotPacketEventArgs(HotEotPacket hotPacket) : System.EventArgs
{
    public HotEotPacket Packet { get; } = hotPacket;
}
