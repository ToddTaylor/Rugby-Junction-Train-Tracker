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
        public int? ExpectedSdrSliceCount { get; set; }

        /// <summary>
        /// When the server entered its current status, as far as the alert monitor knows. Null until the monitor has seen it.
        /// </summary>
        public DateTime? StatusSince { get; set; }

        public bool AlertsMuted { get; set; }

        /// <summary>
        /// When muted alerts turn back on. Null while muted means muted until someone turns them back on.
        /// </summary>
        public DateTime? AlertsMutedUntil { get; set; }

        public string? AlertsMuteNote { get; set; }
        public string? AlertsMutedBy { get; set; }

        /// <summary>
        /// When the beacon's device key was created. Null when it has none, so remote commands are not possible.
        /// </summary>
        public DateTime? DeviceKeySetAt { get; set; }

        public string? DeviceKeySetBy { get; set; }

        /// <summary>
        /// Last time a check-in was refused because the device key was missing or wrong.
        /// </summary>
        public DateTime? DeviceKeyRejectedAt { get; set; }

        /// <summary>
        /// Reboots the PC started in the last 24 hours, against MaxRebootsPerDay.
        /// </summary>
        public int RebootsLast24Hours { get; set; }

        public int MaxRebootsPerDay { get; set; }

        /// <summary>
        /// Recent reboot and restart-apps commands, newest first.
        /// </summary>
        public List<BeaconCommandDTO> Commands { get; set; } = [];

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
        public bool RemoteCommandsEnabled { get; set; }
        public bool RestartAppsAvailable { get; set; }
        public List<DiagnosticsLogFileStatusDTO> Logs { get; set; } = [];
        public List<DiagnosticsStartupTaskStatusDTO> StartupTasks { get; set; } = [];
    }

    public class MuteServerAlertsDTO
    {
        /// <summary>
        /// When alerts turn back on, in UTC. Null mutes them until someone turns them back on.
        /// </summary>
        public DateTime? MutedUntil { get; set; }

        public string? Note { get; set; }
    }

    /// <summary>
    /// SignalR payload sent to support users when a server's status changes.
    /// </summary>
    public class ServerStatusChangedDTO
    {
        public int BeaconID { get; set; }
        public string BeaconName { get; set; } = string.Empty;
        public ServerStatus PreviousStatus { get; set; }
        public ServerStatus Status { get; set; }
        public List<string> Reasons { get; set; } = [];
        public DateTime ChangedAt { get; set; }

        /// <summary>
        /// True when the server's alerts are muted: the dashboard still refreshes, but no pop-up is shown.
        /// </summary>
        public bool AlertsMuted { get; set; }
    }

    public class UpdateServerExpectationsDTO
    {
        public int? ExpectedSdrSharpCount { get; set; }
        public int? ExpectedSoftEotCount { get; set; }
        public int? ExpectedSoftDpuCount { get; set; }
        public int? ExpectedSdrSliceCount { get; set; }
    }
}
