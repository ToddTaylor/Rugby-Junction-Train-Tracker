using Web.Server.Enums;

namespace Web.Server.DTOs
{
    /// <summary>
    /// One beacon PC's row on the Server Health dashboard.
    /// </summary>
    public class ServerHealthDTO
    {
        public int BeaconID { get; set; }
        public string BeaconName { get; set; } = string.Empty;
        public ServerStatus Status { get; set; }

        /// <summary>
        /// Plain-language problems behind an Offline or Degraded status. Empty when Online.
        /// </summary>
        public List<string> Reasons { get; set; } = [];

        /// <summary>
        /// Most recent health check or telemetry received from the PC, in UTC.
        /// </summary>
        public DateTime? LastCheckIn { get; set; }

        public int OfflineThresholdMinutes { get; set; }

        /// <summary>
        /// False when the PC runs a ConsoleApp version that does not send diagnostics.
        /// </summary>
        public bool HasDiagnostics { get; set; }

        public List<string> Subdivisions { get; set; } = [];
        public int? ExpectedSdrSharpCount { get; set; }
        public int? ExpectedSoftEotCount { get; set; }
        public int? ExpectedSoftDpuCount { get; set; }
        public List<ServerProgramDTO> Programs { get; set; } = [];
        public ServerDiagnosticsDTO? Diagnostics { get; set; }
    }

    public class ServerProgramDTO
    {
        public string Name { get; set; } = string.Empty;
        public int? Expected { get; set; }

        /// <summary>
        /// Null when the PC has not reported diagnostics.
        /// </summary>
        public int? Running { get; set; }

        public int NotResponding { get; set; }
    }

    public class ServerDiagnosticsDTO
    {
        public DateTime ReceivedAt { get; set; }
        public DateTime CollectedAt { get; set; }
        public string? Hostname { get; set; }
        public string? ClientVersion { get; set; }
        public string? OsVersion { get; set; }
        public DateTime? LastBootAt { get; set; }
        public long? LogDriveFreeBytes { get; set; }
        public int HealthPingIntervalMinutes { get; set; }
        public DateTime? LastTelemetrySentAt { get; set; }
        public int TelemetrySendFailuresLastHour { get; set; }
        public List<DiagnosticsLogFileStatusDTO> Logs { get; set; } = [];
        public List<DiagnosticsStartupTaskStatusDTO> StartupTasks { get; set; } = [];
    }

    public class UpdateServerExpectationsDTO
    {
        public int? ExpectedSdrSharpCount { get; set; }
        public int? ExpectedSoftEotCount { get; set; }
        public int? ExpectedSoftDpuCount { get; set; }
    }
}
