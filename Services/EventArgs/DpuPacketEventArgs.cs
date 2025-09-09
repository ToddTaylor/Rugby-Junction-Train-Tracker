using Services.Models;

namespace Services.EventArgs;

public class DpuPacketEventArgs(DpuPacket dpuPacket) : System.EventArgs
{
    public DpuPacket Packet { get; } = dpuPacket;
}
