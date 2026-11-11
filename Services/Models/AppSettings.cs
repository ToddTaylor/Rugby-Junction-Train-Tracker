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
        /// An entry with WindowTitleContains counts that program's open windows whose title contains the text instead
        /// of its processes, which is how the SDR# slices opened by the Sharp Slicer plugin are counted.
        /// </summary>
        public List<MonitoredProcess> MonitoredProcesses { get; set; } = new()
        {
            new MonitoredProcess { Name = "SDR#", ProcessName = "SDRSharp*" },
            new MonitoredProcess { Name = "SDR# slices", ProcessName = "SDRSharp*", WindowTitleContains = "Sharp Slicer" },
            new MonitoredProcess { Name = "SoftEOT", ProcessName = "SoftEOT" },
            new MonitoredProcess { Name = "SoftDPU", ProcessName = "SoftDPU" },
        };

        /// <summary>
        /// Windows Task Scheduler task names (for example the AutoIt startup tasks) whose last run result is reported with each beacon health check.
        /// </summary>
        public List<string> MonitoredScheduledTasks { get; set; } = new();

        /// <summary>
        /// Lets support users reboot this PC, or restart its apps, from the Server Health page. Off unless turned on
        /// here. Commands are also only accepted from a subscriber whose Beacon.DeviceKey is set.
        /// </summary>
        public bool AllowRemoteCommands { get; set; }

        /// <summary>
        /// Full path of a program (for example a compiled AutoIt script) that closes and reopens SDR#, SoftEOT and
        /// SoftDPU. Run for the "Restart apps" command; exit code 0 means it worked. Leave empty to offer reboot only.
        /// </summary>
        public string? RestartAppsCommand { get; set; }
    }

    public class MonitoredProcess
    {
        public string Name { get; set; } = string.Empty;
        public string ProcessName { get; set; } = string.Empty;

        /// <summary>
        /// When set, the visible windows of ProcessName whose title contains this text are counted, not the processes.
        /// </summary>
        public string? WindowTitleContains { get; set; }
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

        /// <summary>
        /// The device key created for this beacon on the Server Health page, sent in the X-Beacon-Key header.
        /// </summary>
        public string? DeviceKey { get; set; }
    }
}