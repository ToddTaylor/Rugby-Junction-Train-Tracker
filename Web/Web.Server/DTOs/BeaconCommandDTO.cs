using Web.Server.Enums;

namespace Web.Server.DTOs
{
    /// <summary>
    /// A reboot or restart-apps command as shown on the Server Health page. All times are UTC.
    /// </summary>
    public class BeaconCommandDTO
    {
        public int ID { get; set; }
        public BeaconCommandType Type { get; set; }
        public BeaconCommandStatus Status { get; set; }
        public string RequestedBy { get; set; } = string.Empty;
        public DateTime RequestedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? FinishedAt { get; set; }
        public string? ResultMessage { get; set; }
        public string? CancelledBy { get; set; }
    }

    /// <summary>
    /// Body of POST api/v1/ServerHealth/{beaconId}/commands.
    /// </summary>
    public class RequestBeaconCommandDTO
    {
        public BeaconCommandType Type { get; set; }
    }

    /// <summary>
    /// Returned once when a device key is created. The key cannot be shown again.
    /// </summary>
    public class DeviceKeyCreatedDTO
    {
        public string DeviceKey { get; set; } = string.Empty;
    }

    /// <summary>
    /// The command a beacon PC receives in the health check response: 200 { command } when one is waiting.
    /// </summary>
    public class BeaconHealthResponseDTO
    {
        public PendingBeaconCommandDTO? Command { get; set; }
    }

    public class PendingBeaconCommandDTO
    {
        public int ID { get; set; }
        public BeaconCommandType Type { get; set; }
        public string RequestedBy { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }

    /// <summary>
    /// Body of POST api/v1/Beacons/Health/{id}/commands/{commandId}, sent by the beacon PC.
    /// </summary>
    public class BeaconCommandReportDTO
    {
        public BeaconCommandReportStatus Status { get; set; }
        public string? Message { get; set; }
    }

    /// <summary>
    /// SignalR payload sent to support users when a command is requested, cancelled or reported on by the PC.
    /// </summary>
    public class ServerCommandUpdatedDTO
    {
        public int BeaconID { get; set; }
        public string BeaconName { get; set; } = string.Empty;
        public BeaconCommandDTO Command { get; set; } = new();
    }
}
