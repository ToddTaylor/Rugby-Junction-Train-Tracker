namespace Web.Server.DTOs
{
    /// <summary>
    /// Diagnostics report a beacon PC sends as the body of POST api/v1/Beacons/Health/{id}.
    /// Older ConsoleApp versions send an empty body. All times are UTC.
    /// </summary>
    public class BeaconDiagnosticsReportDTO
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
        public List<DiagnosticsProgramStatusDTO> Programs { get; set; } = [];
        public List<DiagnosticsLogFileStatusDTO> Logs { get; set; } = [];
        public List<DiagnosticsStartupTaskStatusDTO> StartupTasks { get; set; } = [];
    }

    public class DiagnosticsProgramStatusDTO
    {
        public string Name { get; set; } = string.Empty;
        public int Running { get; set; }
        public int NotResponding { get; set; }
    }

    public class DiagnosticsLogFileStatusDTO
    {
        public string Prefix { get; set; } = string.Empty;
        public DateTime? LastWriteAt { get; set; }
    }

    public class DiagnosticsStartupTaskStatusDTO
    {
        public string Name { get; set; } = string.Empty;
        public bool Found { get; set; }
        public DateTime? LastRunAt { get; set; }
        public int? LastResult { get; set; }
    }
}
