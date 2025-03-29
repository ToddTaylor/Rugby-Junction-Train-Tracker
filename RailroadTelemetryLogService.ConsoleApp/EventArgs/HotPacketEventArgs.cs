using ConsoleApp.Models;

namespace ConsoleApp.EventArgs;

public class HotPacketEventArgs(HotEotPacket hotPacket) : System.EventArgs
{
    public HotEotPacket HotPacket { get; } = hotPacket;
}
