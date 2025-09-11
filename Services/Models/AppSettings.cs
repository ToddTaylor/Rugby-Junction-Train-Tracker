namespace Services.Models
{
    public class AppSettings
    {
        public string LogDirectoryPath { get; set; } = string.Empty;
        public List<Subscriber> Subscribers { get; set; } = new();
        public BeaconHealthServices BeaconHealthServices { get; set; } = new();
        public HotEotPacketSubscription HotEotPacketSubscription { get; set; } = new();
        public DpuPacketSubscription DpuPacketSubscription { get; set; } = new();
    }

    public class Subscriber
    {
        public string ID { get; set; } = string.Empty;
        public ApiSettings ApiSettings { get; set; } = new();
        public Beacon Beacon { get; set; } = new();
        public bool SendInvalidMessages { get; set; }
    }

    public class ApiSettings
    {
        public string ApiKey { get; set; } = string.Empty;
        public string? Url { get; set; }
    }

    public class Beacon
    {
        public int? BeaconID { get; set; }
        public string? DetectorID { get; set; }
        public int HealthPingIntervalMinutes { get; set; }
    }

    public class BeaconHealthServices
    {
        public List<string> Services { get; set; } = new();
    }

    public class HotEotPacketSubscription
    {
        public List<string> Subscribers { get; set; } = new();
    }

    public class DpuPacketSubscription
    {
        public List<string> Subscribers { get; set; } = new();
    }
}