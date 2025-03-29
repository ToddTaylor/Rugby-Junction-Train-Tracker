namespace RailroadTelemetryLogService.Models
{
    public abstract class BasePacket
    {
        public int ID { get; set; }
        public bool Valid { get; set; }
    }
}
