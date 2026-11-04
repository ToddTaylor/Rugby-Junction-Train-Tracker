using Web.Server.DTOs;
using Web.Server.Entities;
using Web.Server.Enums;

namespace Web.Server.Services
{
    /// <summary>
    /// Device keys and remote commands (reboot, restart apps) for beacon PCs. A command is stored here, handed to the
    /// PC with its next health check response, and reported on by the PC. Callers check the user's permission first.
    /// </summary>
    public interface IServerCommandService
    {
        /// <summary>
        /// Creates or replaces the beacon's device key and returns it. Only its hash is kept, so it cannot be shown
        /// again. Returns null when the beacon does not exist.
        /// </summary>
        Task<string?> CreateDeviceKeyAsync(int beaconId, User createdBy);

        /// <summary>
        /// Returns false when the beacon has no key.
        /// </summary>
        Task<bool> RemoveDeviceKeyAsync(int beaconId);

        /// <summary>
        /// Checks the key a PC sent in X-Beacon-Key. A refused key is recorded so the Server Health page can show it.
        /// </summary>
        Task<DeviceKeyCheck> CheckDeviceKeyAsync(int beaconId, string? presentedKey);

        Task<CommandRequestResult> RequestCommandAsync(int beaconId, BeaconCommandType type, User requestedBy);

        Task<CommandChangeResult> CancelCommandAsync(int beaconId, int commandId, User cancelledBy);

        /// <summary>
        /// Called for each health check from a PC that sent a valid device key. Marks a reboot finished once the PC
        /// reports a boot time after the reboot started, and returns the waiting command when the PC allows remote
        /// commands.
        /// </summary>
        Task<PendingBeaconCommandDTO?> HandleCheckInAsync(int beaconId, BeaconDiagnostics? diagnostics);

        /// <summary>
        /// Records what the PC reports about a command. A Started report on a command that was cancelled, expired or
        /// already handled returns Conflict, and the PC does not carry it out.
        /// </summary>
        Task<CommandChangeResult> ReportCommandAsync(int beaconId, int commandId, BeaconCommandReportDTO report);
    }

    public enum DeviceKeyCheck
    {
        /// <summary>
        /// The beacon has no device key: health checks are accepted as before, and no commands are handed out.
        /// </summary>
        NotConfigured,
        Valid,
        Invalid
    }

    public enum CommandRequestError
    {
        None,
        BeaconNotFound,
        NoDeviceKey,
        ServerOffline,
        RemoteCommandsOff,
        RestartAppsUnavailable,
        CommandPending,
        DailyLimitReached
    }

    public record CommandRequestResult(CommandRequestError Error, string? Message, BeaconCommandDTO? Command = null);

    public enum CommandChangeResult
    {
        Updated,
        NotFound,
        Conflict
    }
}
