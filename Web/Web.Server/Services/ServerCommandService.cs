using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.SignalR;
using Web.Server.DTOs;
using Web.Server.Entities;
using Web.Server.Enums;
using Web.Server.Hubs;
using Web.Server.Providers;
using Web.Server.Repositories;

namespace Web.Server.Services
{
    public class ServerCommandService : IServerCommandService
    {
        /// <summary>
        /// The PC must start a command within this time of the request, or it is not carried out.
        /// </summary>
        public static readonly TimeSpan CommandLifetime = TimeSpan.FromMinutes(10);

        public const int MaxRebootsPerDay = 3;
        public const int MaxRestartAppsPerDay = 6;
        public const int MaxResultMessageLength = 500;

        /// <summary>
        /// A reboot is marked failed when the PC checks in this long after starting it without having restarted.
        /// The PC waits 60 seconds before restarting, so this leaves time for the shutdown itself.
        /// </summary>
        public static readonly TimeSpan RebootConfirmWindow = TimeSpan.FromMinutes(5);

        /// <summary>
        /// A started command with no result after this long no longer blocks a new one.
        /// </summary>
        public static readonly TimeSpan StaleRunningCommand = TimeSpan.FromMinutes(30);

        // One request at a time, so two clicks cannot both pass the "one command at a time" check.
        private static readonly SemaphoreSlim RequestLock = new(1, 1);

        private readonly IBeaconRepository _beaconRepository;
        private readonly IBeaconDiagnosticsRepository _diagnosticsRepository;
        private readonly IBeaconDeviceKeyRepository _deviceKeyRepository;
        private readonly IBeaconCommandRepository _commandRepository;
        private readonly IUserRepository _userRepository;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ITimeProvider _timeProvider;
        private readonly ILogger<ServerCommandService> _logger;

        public ServerCommandService(
            IBeaconRepository beaconRepository,
            IBeaconDiagnosticsRepository diagnosticsRepository,
            IBeaconDeviceKeyRepository deviceKeyRepository,
            IBeaconCommandRepository commandRepository,
            IUserRepository userRepository,
            IHubContext<NotificationHub> hubContext,
            ITimeProvider timeProvider,
            ILogger<ServerCommandService> logger)
        {
            _beaconRepository = beaconRepository;
            _diagnosticsRepository = diagnosticsRepository;
            _deviceKeyRepository = deviceKeyRepository;
            _commandRepository = commandRepository;
            _userRepository = userRepository;
            _hubContext = hubContext;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<string?> CreateDeviceKeyAsync(int beaconId, User createdBy)
        {
            if (await _beaconRepository.GetByIdAsync(beaconId) == null)
            {
                return null;
            }

            var key = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
            await _deviceKeyRepository.SetAsync(beaconId, HashKey(key), ServerSupportUsers.DisplayName(createdBy));
            _logger.LogWarning("Device key for beacon {BeaconId} set by user {UserId}.", beaconId, createdBy.ID);
            return key;
        }

        public async Task<bool> RemoveDeviceKeyAsync(int beaconId)
        {
            var removed = await _deviceKeyRepository.DeleteAsync(beaconId);
            if (removed)
            {
                _logger.LogWarning("Device key for beacon {BeaconId} removed.", beaconId);
            }

            return removed;
        }

        public async Task<DeviceKeyCheck> CheckDeviceKeyAsync(int beaconId, string? presentedKey)
        {
            var deviceKey = await _deviceKeyRepository.GetAsync(beaconId);
            if (deviceKey == null)
            {
                return DeviceKeyCheck.NotConfigured;
            }

            if (!string.IsNullOrWhiteSpace(presentedKey) && KeyMatches(presentedKey.Trim(), deviceKey.KeyHash))
            {
                return DeviceKeyCheck.Valid;
            }

            await _deviceKeyRepository.RecordRejectedAsync(beaconId);
            return DeviceKeyCheck.Invalid;
        }

        public async Task<CommandRequestResult> RequestCommandAsync(int beaconId, BeaconCommandType type, User requestedBy)
        {
            var beacon = await _beaconRepository.GetByIdAsync(beaconId);
            if (beacon == null)
            {
                return new(CommandRequestError.BeaconNotFound, "Server not found.");
            }

            if (await _deviceKeyRepository.GetAsync(beaconId) == null)
            {
                return new(CommandRequestError.NoDeviceKey, $"{beacon.Name} has no device key. Create one and enter it on the PC first.");
            }

            var utcNow = _timeProvider.UtcNow;
            var diagnostics = await _diagnosticsRepository.GetAsync(beaconId);
            var status = BeaconServerStatusEvaluator.Evaluate(beacon, diagnostics, ServerHealthService.GetLastCheckIn(beacon, diagnostics), utcNow);
            if (status.Status == ServerStatus.Offline)
            {
                return new(CommandRequestError.ServerOffline, $"{beacon.Name} is offline, so it cannot pick up the command.");
            }

            if (!status.UsesDiagnostics || diagnostics?.RemoteCommandsEnabled != true)
            {
                return new(CommandRequestError.RemoteCommandsOff,
                    $"{beacon.Name} does not allow remote commands. Turn on AllowRemoteCommands and enter the device key in the PC's ConsoleApp settings.");
            }

            if (type == BeaconCommandType.RestartApps && !diagnostics.RestartAppsAvailable)
            {
                return new(CommandRequestError.RestartAppsUnavailable, $"{beacon.Name} has no RestartAppsCommand set up.");
            }

            await RequestLock.WaitAsync();
            BeaconCommand command;
            try
            {
                var recent = await _commandRepository.GetForBeaconSinceAsync(beaconId, utcNow.AddDays(-1));
                if (recent.Any(c => BlocksNewCommand(c, utcNow)))
                {
                    return new(CommandRequestError.CommandPending, $"{beacon.Name} already has a command waiting or in progress.");
                }

                var limit = type == BeaconCommandType.Reboot ? MaxRebootsPerDay : MaxRestartAppsPerDay;
                if (CountStartedSince(recent, type, utcNow.AddDays(-1)) >= limit)
                {
                    var what = type == BeaconCommandType.Reboot ? "reboots" : "app restarts";
                    return new(CommandRequestError.DailyLimitReached, $"{beacon.Name} has had {limit} {what} in the last 24 hours, the most allowed.");
                }

                command = await _commandRepository.AddAsync(new BeaconCommand
                {
                    BeaconID = beaconId,
                    Type = type,
                    RequestedByUserId = requestedBy.ID,
                    RequestedBy = ServerSupportUsers.DisplayName(requestedBy),
                    RequestedAt = utcNow,
                    ExpiresAt = utcNow + CommandLifetime,
                });
            }
            finally
            {
                RequestLock.Release();
            }

            _logger.LogWarning("{CommandType} of beacon {BeaconId} requested by user {UserId} (command {CommandId}).",
                type, beaconId, requestedBy.ID, command.ID);

            var dto = ToDto(command, utcNow);
            await NotifyAsync(beacon, dto);
            return new(CommandRequestError.None, null, dto);
        }

        public async Task<CommandChangeResult> CancelCommandAsync(int beaconId, int commandId, User cancelledBy)
        {
            var command = await _commandRepository.GetByIdAsync(commandId);
            if (command == null || command.BeaconID != beaconId)
            {
                return CommandChangeResult.NotFound;
            }

            var utcNow = _timeProvider.UtcNow;
            if (command.GetStatus(utcNow) != BeaconCommandStatus.Pending)
            {
                return CommandChangeResult.Conflict;
            }

            command.CancelledAt = utcNow;
            command.CancelledBy = ServerSupportUsers.DisplayName(cancelledBy);
            await _commandRepository.UpdateAsync(command);
            _logger.LogWarning("Command {CommandId} for beacon {BeaconId} cancelled by user {UserId}.", commandId, beaconId, cancelledBy.ID);

            await NotifyAsync(beaconId, ToDto(command, utcNow));
            return CommandChangeResult.Updated;
        }

        public async Task<PendingBeaconCommandDTO?> HandleCheckInAsync(int beaconId, BeaconDiagnostics? diagnostics)
        {
            var utcNow = _timeProvider.UtcNow;
            var recent = await _commandRepository.GetForBeaconSinceAsync(beaconId, utcNow.AddDays(-1));
            if (recent.Count == 0)
            {
                return null;
            }

            foreach (var reboot in recent.Where(c => c.Type == BeaconCommandType.Reboot && c.GetStatus(utcNow) == BeaconCommandStatus.Running))
            {
                if (diagnostics?.LastBootAt > reboot.StartedAt)
                {
                    await FinishAsync(reboot, BeaconCommandStatus.Succeeded, "The PC restarted and is checking in again.", utcNow);
                }
                else if (diagnostics?.LastBootAt != null && utcNow - reboot.StartedAt!.Value > RebootConfirmWindow)
                {
                    await FinishAsync(reboot, BeaconCommandStatus.Failed,
                        "The PC is still checking in but did not restart. The restart may have been stopped on the PC.", utcNow);
                }
            }

            if (diagnostics?.RemoteCommandsEnabled != true)
            {
                return null;
            }

            var pending = recent
                .Where(c => c.GetStatus(utcNow) == BeaconCommandStatus.Pending)
                .OrderBy(c => c.RequestedAt)
                .FirstOrDefault();
            if (pending == null)
            {
                return null;
            }

            if (pending.DeliveredAt == null)
            {
                pending.DeliveredAt = utcNow;
                await _commandRepository.UpdateAsync(pending);
            }

            return new PendingBeaconCommandDTO
            {
                ID = pending.ID,
                Type = pending.Type,
                RequestedBy = pending.RequestedBy,
                ExpiresAt = pending.ExpiresAt,
            };
        }

        public async Task<CommandChangeResult> ReportCommandAsync(int beaconId, int commandId, BeaconCommandReportDTO report)
        {
            var command = await _commandRepository.GetByIdAsync(commandId);
            if (command == null || command.BeaconID != beaconId)
            {
                return CommandChangeResult.NotFound;
            }

            var utcNow = _timeProvider.UtcNow;
            var status = command.GetStatus(utcNow);
            var message = Truncate(report.Message);

            switch (report.Status)
            {
                case BeaconCommandReportStatus.Started:
                    if (status != BeaconCommandStatus.Pending)
                    {
                        return CommandChangeResult.Conflict;
                    }

                    command.StartedAt = utcNow;
                    command.ResultMessage = message;
                    await _commandRepository.UpdateAsync(command);
                    break;

                case BeaconCommandReportStatus.Rejected:
                    if (status is not (BeaconCommandStatus.Pending or BeaconCommandStatus.Expired))
                    {
                        return CommandChangeResult.Conflict;
                    }

                    await FinishAsync(command, BeaconCommandStatus.Rejected, message ?? "The PC refused the command.", utcNow, notify: false);
                    break;

                case BeaconCommandReportStatus.Succeeded:
                case BeaconCommandReportStatus.Failed:
                    if (status != BeaconCommandStatus.Running)
                    {
                        return CommandChangeResult.Conflict;
                    }

                    var outcome = report.Status == BeaconCommandReportStatus.Succeeded ? BeaconCommandStatus.Succeeded : BeaconCommandStatus.Failed;
                    await FinishAsync(command, outcome, message, utcNow, notify: false);
                    break;

                default:
                    return CommandChangeResult.Conflict;
            }

            _logger.LogInformation("Beacon {BeaconId} reported {ReportStatus} for command {CommandId}.", beaconId, report.Status, commandId);
            await NotifyAsync(beaconId, ToDto(command, utcNow));
            return CommandChangeResult.Updated;
        }

        /// <summary>
        /// Commands of the type that the PC started since the given time, not counting ones it refused.
        /// </summary>
        public static int CountStartedSince(IEnumerable<BeaconCommand> commands, BeaconCommandType type, DateTime since) =>
            commands.Count(c => c.Type == type && c.StartedAt >= since && c.Outcome != BeaconCommandStatus.Rejected);

        public static BeaconCommandDTO ToDto(BeaconCommand command, DateTime utcNow) => new()
        {
            ID = command.ID,
            Type = command.Type,
            Status = command.GetStatus(utcNow),
            RequestedBy = command.RequestedBy,
            RequestedAt = command.RequestedAt,
            ExpiresAt = command.ExpiresAt,
            DeliveredAt = command.DeliveredAt,
            StartedAt = command.StartedAt,
            FinishedAt = command.FinishedAt,
            ResultMessage = command.ResultMessage,
            CancelledBy = command.CancelledBy,
        };

        public static string HashKey(string key) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

        private static bool KeyMatches(string presentedKey, string storedHash)
        {
            var presentedHash = Encoding.ASCII.GetBytes(HashKey(presentedKey));
            var expectedHash = Encoding.ASCII.GetBytes(storedHash);
            return CryptographicOperations.FixedTimeEquals(presentedHash, expectedHash);
        }

        private static bool BlocksNewCommand(BeaconCommand command, DateTime utcNow) => command.GetStatus(utcNow) switch
        {
            BeaconCommandStatus.Pending => true,
            BeaconCommandStatus.Running => utcNow - command.StartedAt!.Value < StaleRunningCommand,
            _ => false,
        };

        private async Task FinishAsync(BeaconCommand command, BeaconCommandStatus outcome, string? message, DateTime utcNow, bool notify = true)
        {
            command.Outcome = outcome;
            command.FinishedAt = utcNow;
            command.ResultMessage = message;
            await _commandRepository.UpdateAsync(command);

            if (notify)
            {
                await NotifyAsync(command.BeaconID, ToDto(command, utcNow));
            }
        }

        private async Task NotifyAsync(int beaconId, BeaconCommandDTO command)
        {
            var beacon = await _beaconRepository.GetByIdAsync(beaconId);
            if (beacon != null)
            {
                await NotifyAsync(beacon, command);
            }
        }

        /// <summary>
        /// Tells the support users who look after the beacon, so every open Server Health page shows the change.
        /// A failure to send is logged and does not undo the change.
        /// </summary>
        private async Task NotifyAsync(Beacon beacon, BeaconCommandDTO command)
        {
            try
            {
                var recipients = ServerSupportUsers.GetRecipientIds(beacon, await _userRepository.GetAllAsync());
                if (recipients.Count == 0)
                {
                    return;
                }

                await _hubContext.Clients
                    .Groups(recipients.Select(NotificationHub.GetUserGroup).ToList())
                    .SendAsync(NotificationMethods.ServerCommandUpdated, new ServerCommandUpdatedDTO
                    {
                        BeaconID = beacon.ID,
                        BeaconName = beacon.Name,
                        Command = command,
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not send the command update for beacon {BeaconId}.", beacon.ID);
            }
        }

        private static string? Truncate(string? message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return null;
            }

            var trimmed = message.Trim();
            return trimmed.Length <= MaxResultMessageLength ? trimmed : trimmed[..MaxResultMessageLength];
        }

        private static string Base64UrlEncode(byte[] bytes) =>
            Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
