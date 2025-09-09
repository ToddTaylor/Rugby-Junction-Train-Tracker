using Services.Models;

namespace Services.EventArgs;

public class HotEotPacketEventArgs(HotEotPacket hotPacket) : System.EventArgs
{
    public HotEotPacket Packet { get; } = hotPacket;
}
