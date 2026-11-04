using System.ComponentModel.DataAnnotations;

namespace Web.Server.Entities
{
    /// <summary>
    /// Latest diagnostics report sent by a beacon PC with its health check. One row per beacon,
    /// overwritten on every report. LastUpdate is when the server received it; all times are UTC.
    /// </summary>
    public class BeaconDiagnostics : EntityBase
    {
        [Key]
        public int BeaconID { get; set; }

        public Beacon? Beacon { get; set; }

        /// <summary>
        /// When the PC collected the report, by the PC's clock.
        /// </summary>
        public DateTime CollectedAt { get; set; }

        public string? Hostname { get; set; }

        public string? ClientVersion { get; set; }

        public string? OsVersion { get; set; }

        public DateTime? LastBootAt { get; set; }

        public long? LogDriveFreeBytes { get; set; }

        public int HealthPingIntervalMinutes { get; set; }

        public DateTime? LastTelemetrySentAt { get; set; }

        public int TelemetrySendFailuresLastHour { get; set; }

        /// <summary>
        /// True when the PC allows remote commands (AllowRemoteCommands) and has a device key set.
        /// </summary>
        public bool RemoteCommandsEnabled { get; set; }

        /// <summary>
        /// True when the PC has a RestartAppsCommand set and the file exists.
        /// </summary>
        public bool RestartAppsAvailable { get; set; }

        public List<DiagnosticsProgramStatus> Programs { get; set; } = [];

        public List<DiagnosticsLogFileStatus> Logs { get; set; } = [];

        public List<DiagnosticsStartupTaskStatus> StartupTasks { get; set; } = [];
    }

    public class DiagnosticsProgramStatus
    {
        public string Name { get; set; } = string.Empty;
        public int Running { get; set; }
        public int NotResponding { get; set; }
    }

    public class DiagnosticsLogFileStatus
    {
        public string Prefix { get; set; } = string.Empty;
        public DateTime? LastWriteAt { get; set; }
    }

    public class DiagnosticsStartupTaskStatus
    {
        public string Name { get; set; } = string.Empty;
        public bool Found { get; set; }
        public DateTime? LastRunAt { get; set; }
        public int? LastResult { get; set; }
    }
}
