namespace Services.Models
{
    public class AppSettings
    {
        public string LogDirectoryPath { get; set; } = string.Empty;
        public string? OptionalSubscribersPath { get; set; }
        public List<Subscriber> Subscribers { get; set; } = new();
    }

    public class Subscriber
    {
        public string ID { get; set; } = string.Empty;
        public ApiSettings ApiSettings { get; set; } = new();
        public Beacon Beacon { get; set; } = new();
        public bool SendInvalidMessages { get; set; }

        /// <summary>
        /// Time interval in seconds to throttle telemetry messages per AddressID.
        /// Default is 60 seconds (1 minute).
        /// Only one message per AddressID will be sent within this interval.
        /// </summary>
        public int TelemetryThrottleIntervalSeconds { get; set; } = 60;

        /// <summary>
        /// Fully qualified type name for the beacon health service (e.g., "Services.Subscribers.RugbyJunctionAPI.BeaconHealthService")
        /// </summary>
        public string HealthService { get; set; }

        /// <summary>
        /// Fully qualified type name for the HOT/EOT packet subscriber (e.g., "Services.Subscribers.RugbyJunctionAPI.HotEotPacketSubscriber")
        /// </summary>
        public string HotEotPacketSubscriber { get; set; }

        /// <summary>
        /// Fully qualified type name for the DPU packet subscriber (e.g., "Services.Subscribers.RugbyJunctionAPI.DpuPacketSubscriber")
        /// </summary>
        public string DpuPacketSubscriber { get; set; }
    }

    public class ApiSettings
    {
        public string ApiKey { get; set; } = string.Empty;
        public string? HealthUrl { get; set; }
        public string? Url { get; set; }
    }

    public class Beacon
    {
        public string? BeaconID { get; set; }
        public string? DetectorID { get; set; }
        public int HealthPingIntervalMinutes { get; set; }
    }
}