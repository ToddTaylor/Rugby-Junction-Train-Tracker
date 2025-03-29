namespace RailroadTelemetryLogService.Models
{
    public class DpuPacket : BasePacket, IEquatable<DpuPacket?>
    {
        public string EngineStatus { get; set; }
        public string EngineStatusText { get; set; }
        public string BatteryCharge { get; set; }
        public int Speed { get; set; }
        public short Temperature { get; set; }
        public int UnitAddress { get; set; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as DpuPacket);
        }

        public bool Equals(DpuPacket? other)
        {
            return other is not null &&
                   BatteryCharge == other.BatteryCharge &&
                   EngineStatus == other.EngineStatus &&
                   EngineStatusText == other.EngineStatusText &&
                   Speed == other.Speed &&
                   Temperature == other.Temperature &&
                   UnitAddress == other.UnitAddress;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(BatteryCharge, EngineStatus, EngineStatusText, Speed, Temperature);
        }

        public static bool operator ==(DpuPacket? left, DpuPacket? right)
        {
            return EqualityComparer<DpuPacket>.Default.Equals(left, right);
        }

        public static bool operator !=(DpuPacket? left, DpuPacket? right)
        {
            return !(left == right);
        }
    }
}
