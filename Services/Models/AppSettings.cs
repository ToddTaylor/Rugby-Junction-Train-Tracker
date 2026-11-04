namespace Services.Models
{
    public class AppSettings
    {
        public string LogDirectoryPath { get; set; } = string.Empty;
        public string? OptionalSubscribersPath { get; set; }
        public List<Subscriber> Subscribers { get; set; } = new();

        /// <summary>
        /// Programs whose running instances are counted and reported with each beacon health check.
        /// Name is the label shown on the Server Health dashboard; ProcessName is the exe name without ".exe".
        /// A trailing "*" matches any exe name starting with the text before it, so "SDRSharp*" covers SDRSharp.dotnet9.exe.
        /// </summary>
        public List<MonitoredProcess> MonitoredProcesses { get; set; } = new()
        {
            new MonitoredProcess { Name = "SDR#", ProcessName = "SDRSharp*" },
            new MonitoredProcess { Name = "SoftEOT", ProcessName = "SoftEOT" },
            new MonitoredProcess { Name = "SoftDPU", ProcessName = "SoftDPU" },
        };

        /// <summary>
        /// Windows Task Scheduler task names (for example the AutoIt startup tasks) whose last run result is reported with each beacon health check.
        /// </summary>
        public List<string> MonitoredScheduledTasks { get; set; } = new();
    }

    public class MonitoredProcess
    {
        public string Name { get; set; } = string.Empty;
        public string ProcessName { get; set; } = string.Empty;
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