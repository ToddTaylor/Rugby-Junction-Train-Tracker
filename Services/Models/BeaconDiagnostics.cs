namespace Services.Models
{
    /// <summary>
    /// Snapshot of the beacon PC's state, sent as the body of each beacon health check.
    /// All times are UTC.
    /// </summary>
    public class BeaconDiagnostics
    {
        public DateTime CollectedAt { get; set; }
        public string? Hostname { get; set; }
        public string? ClientVersion { get; set; }
        public string? OsVersion { get; set; }
        public DateTime? LastBootAt { get; set; }
        public long? LogDriveFreeBytes { get; set; }
        public int HealthPingIntervalMinutes { get; set; }
        public DateTime? LastTelemetrySentAt { get; set; }
        public int TelemetrySendFailuresLastHour { get; set; }
        public List<ProgramStatus> Programs { get; set; } = new();
        public List<LogFileStatus> Logs { get; set; } = new();
        public List<ScheduledTaskStatus> StartupTasks { get; set; } = new();
    }

    public class ProgramStatus
    {
        public string Name { get; set; } = string.Empty;
        public int Running { get; set; }
        public int NotResponding { get; set; }
    }

    public class LogFileStatus
    {
        public string Prefix { get; set; } = string.Empty;
        public DateTime? LastWriteAt { get; set; }
    }

    public class ScheduledTaskStatus
    {
        public string Name { get; set; } = string.Empty;
        public bool Found { get; set; }
        public DateTime? LastRunAt { get; set; }
        public int? LastResult { get; set; }
    }
}
