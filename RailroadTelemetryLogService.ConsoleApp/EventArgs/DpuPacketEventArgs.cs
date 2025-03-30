using ConsoleApp.Models;

namespace ConsoleApp.EventArgs;

public class DpuPacketEventArgs(DpuPacket dpuPacket) : System.EventArgs
{
    public DpuPacket Packet { get; } = dpuPacket;
}
