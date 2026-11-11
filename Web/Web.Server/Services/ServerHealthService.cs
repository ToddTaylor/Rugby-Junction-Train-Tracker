using MapsterMapper;
using Web.Server.DTOs;
using Web.Server.Entities;
using Web.Server.Enums;
using Web.Server.Providers;
using Web.Server.Repositories;

namespace Web.Server.Services
{
    public class ServerHealthService : IServerHealthService
    {
        /// <summary>
        /// How long a change between Online and Degraded must last before support users are alerted.
        /// </summary>
        public const int StatusConfirmMinutes = 3;

        /// <summary>
        /// How far back the Server Health page lists a server's reboot and restart-apps commands.
        /// </summary>
        public const int CommandHistoryDays = 30;

        public const int CommandHistoryLimit = 10;

        private readonly IBeaconRepository _beaconRepository;
        private readonly IBeaconDiagnosticsRepository _diagnosticsRepository;
        private readonly IBeaconServerStateRepository _stateRepository;
        private readonly IBeaconDeviceKeyRepository _deviceKeyRepository;
        private readonly IBeaconCommandRepository _commandRepository;
        private readonly IUserRepository _userRepository;
        private readonly IMapper _mapper;
        private readonly ITimeProvider _timeProvider;

        public ServerHealthService(
            IBeaconRepository beaconRepository,
            IBeaconDiagnosticsRepository diagnosticsRepository,
            IBeaconServerStateRepository stateRepository,
            IBeaconDeviceKeyRepository deviceKeyRepository,
            IBeaconCommandRepository commandRepository,
            IUserRepository userRepository,
            IMapper mapper,
            ITimeProvider timeProvider)
        {
            _beaconRepository = beaconRepository;
            _diagnosticsRepository = diagnosticsRepository;
            _stateRepository = stateRepository;
            _deviceKeyRepository = deviceKeyRepository;
            _commandRepository = commandRepository;
            _userRepository = userRepository;
            _mapper = mapper;
            _timeProvider = timeProvider;
        }

        public async Task RecordDiagnosticsAsync(int beaconId, BeaconDiagnostics diagnostics)
        {
            diagnostics.BeaconID = beaconId;
            await _diagnosticsRepository.UpsertAsync(diagnostics);
        }

        public async Task<IReadOnlyList<ServerHealthDTO>> GetServerHealthAsync(User user)
        {
            var isAdmin = HasRole(user, "Admin");
            var isCustodian = HasRole(user, "Custodian");
            if (!isAdmin && !isCustodian)
            {
                return [];
            }

            var beacons = (await _beaconRepository.GetAllAsync())
                .Where(b => isAdmin || IsCustodianOf(user, b));
            var diagnosticsByBeacon = (await _diagnosticsRepository.GetAllAsync()).ToDictionary(d => d.BeaconID);
            var statesByBeacon = (await _stateRepository.GetAllAsync()).ToDictionary(s => s.BeaconID);
            var keysByBeacon = (await _deviceKeyRepository.GetAllAsync()).ToDictionary(k => k.BeaconID);
            var utcNow = _timeProvider.UtcNow;
            var commandsByBeacon = (await _commandRepository.GetRequestedSinceAsync(utcNow.AddDays(-CommandHistoryDays)))
                .ToLookup(c => c.BeaconID);

            return beacons
                .Select(b => BuildRow(
                    b,
                    diagnosticsByBeacon.GetValueOrDefault(b.ID),
                    statesByBeacon.GetValueOrDefault(b.ID),
                    keysByBeacon.GetValueOrDefault(b.ID),
                    commandsByBeacon[b.ID].ToList(),
                    utcNow))
                .OrderBy(r => r.BeaconName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task<IReadOnlyDictionary<int, BeaconMapStatus>> GetMapStatusesAsync()
        {
            var beacons = await _beaconRepository.GetAllAsync();
            var diagnosticsByBeacon = (await _diagnosticsRepository.GetAllAsync()).ToDictionary(d => d.BeaconID);
            var statesByBeacon = (await _stateRepository.GetAllAsync()).ToDictionary(s => s.BeaconID);
            var utcNow = _timeProvider.UtcNow;

            return beacons.ToDictionary(
                b => b.ID,
                b => BeaconMapStatus.Resolve(b, diagnosticsByBeacon.GetValueOrDefault(b.ID), statesByBeacon.GetValueOrDefault(b.ID), utcNow));
        }

        public async Task<bool> CanManageBeaconAsync(User user, int beaconId)
        {
            if (HasRole(user, "Admin"))
            {
                return true;
            }

            if (!HasRole(user, "Custodian"))
            {
                return false;
            }

            var beacon = await _beaconRepository.GetByIdAsync(beaconId);
            return beacon != null && IsCustodianOf(user, beacon);
        }

        public async Task<bool> UpdateExpectedProgramsAsync(int beaconId, UpdateServerExpectationsDTO expectations)
        {
            var beacon = await _beaconRepository.UpdateExpectedProgramCountsAsync(
                beaconId,
                expectations.ExpectedSdrSharpCount,
                expectations.ExpectedSoftEotCount,
                expectations.ExpectedSoftDpuCount,
                expectations.ExpectedSdrSliceCount);
            return beacon != null;
        }

        public async Task<bool> MuteAlertsAsync(int beaconId, MuteServerAlertsDTO mute, User mutedBy)
        {
            if (await _beaconRepository.GetByIdAsync(beaconId) == null)
            {
                return false;
            }

            DateTime? mutedUntil = mute.MutedUntil.HasValue ? ToUtc(mute.MutedUntil.Value) : null;
            var note = string.IsNullOrWhiteSpace(mute.Note) ? null : mute.Note.Trim();
            await _stateRepository.SetMuteAsync(beaconId, mutedUntil, note, ServerSupportUsers.DisplayName(mutedBy));
            return true;
        }

        public async Task<bool> UnmuteAlertsAsync(int beaconId)
        {
            if (await _beaconRepository.GetByIdAsync(beaconId) == null)
            {
                return false;
            }

            await _stateRepository.ClearMuteAsync(beaconId);
            return true;
        }

        public async Task<IReadOnlyList<ServerStatusChange>> DetectStatusChangesAsync()
        {
            var utcNow = _timeProvider.UtcNow;
            var beacons = (await _beaconRepository.GetAllAsync()).ToList();
            var diagnosticsByBeacon = (await _diagnosticsRepository.GetAllAsync()).ToDictionary(d => d.BeaconID);
            var statesByBeacon = (await _stateRepository.GetAllAsync()).ToDictionary(s => s.BeaconID);
            var announced = new List<(Beacon Beacon, ServerStatusChangedDTO Notification)>();

            foreach (var beacon in beacons)
            {
                var diagnostics = diagnosticsByBeacon.GetValueOrDefault(beacon.ID);
                var result = BeaconServerStatusEvaluator.Evaluate(beacon, diagnostics, GetLastCheckIn(beacon, diagnostics), utcNow);

                if (!statesByBeacon.TryGetValue(beacon.ID, out var state))
                {
                    // First time the monitor sees this server: record where it stands without alerting,
                    // so a deploy or a new beacon does not set off a burst of pop-ups.
                    await _stateRepository.UpdateStatusAsync(new BeaconServerState
                    {
                        BeaconID = beacon.ID,
                        Status = result.Status,
                        StatusSince = utcNow,
                    });
                    continue;
                }

                var previousStatus = state.Status;
                var (stateChanged, isAnnounced) = ApplyObservedStatus(state, result.Status, utcNow);
                if (stateChanged)
                {
                    await _stateRepository.UpdateStatusAsync(state);
                }

                if (isAnnounced)
                {
                    announced.Add((beacon, new ServerStatusChangedDTO
                    {
                        BeaconID = beacon.ID,
                        BeaconName = beacon.Name,
                        PreviousStatus = previousStatus,
                        Status = state.Status,
                        Reasons = result.Reasons.ToList(),
                        ChangedAt = state.StatusSince,
                        AlertsMuted = state.IsMuted(utcNow),
                    }));
                }
            }

            if (announced.Count == 0)
            {
                return [];
            }

            var users = (await _userRepository.GetAllAsync()).ToList();

            return announced
                .Select(a => new ServerStatusChange(a.Notification, ServerSupportUsers.GetRecipientIds(a.Beacon, users)))
                .ToList();
        }

        /// <summary>
        /// Moves the announced status toward the status just observed. A change to or from Offline is announced at
        /// once, because the offline threshold already allows for a late check-in. A change between Online and
        /// Degraded must last StatusConfirmMinutes first, so a program restarting or briefly not responding does
        /// not cause a pop-up. Returns whether the state needs saving and whether the change should be announced.
        /// </summary>
        public static (bool StateChanged, bool Announce) ApplyObservedStatus(BeaconServerState state, ServerStatus observed, DateTime utcNow)
        {
            if (observed == state.Status)
            {
                if (state.PendingStatus == null)
                {
                    return (false, false);
                }

                state.PendingStatus = null;
                state.PendingSince = null;
                return (true, false);
            }

            var pendingChanged = state.PendingStatus != observed;
            if (pendingChanged)
            {
                state.PendingStatus = observed;
                state.PendingSince = utcNow;
            }

            var involvesOffline = observed == ServerStatus.Offline || state.Status == ServerStatus.Offline;
            if (!involvesOffline && utcNow - state.PendingSince!.Value < TimeSpan.FromMinutes(StatusConfirmMinutes))
            {
                return (pendingChanged, false);
            }

            state.Status = observed;
            state.StatusSince = state.PendingSince!.Value;
            state.PendingStatus = null;
            state.PendingSince = null;
            return (true, true);
        }

        private static DateTime ToUtc(DateTime value) => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };

        private ServerHealthDTO BuildRow(
            Beacon beacon,
            BeaconDiagnostics? diagnostics,
            BeaconServerState? state,
            BeaconDeviceKey? deviceKey,
            IReadOnlyList<BeaconCommand> commands,
            DateTime utcNow)
        {
            var lastCheckIn = GetLastCheckIn(beacon, diagnostics);
            var result = BeaconServerStatusEvaluator.Evaluate(beacon, diagnostics, lastCheckIn, utcNow);
            var reportedDiagnostics = result.UsesDiagnostics ? diagnostics : null;
            var isMuted = state?.IsMuted(utcNow) == true;

            return new ServerHealthDTO
            {
                BeaconID = beacon.ID,
                BeaconName = beacon.Name,
                Status = result.Status,
                Reasons = result.Reasons.ToList(),
                LastCheckIn = lastCheckIn,
                OfflineThresholdMinutes = result.OfflineThresholdMinutes,
                HasDiagnostics = diagnostics != null,
                Subdivisions = beacon.BeaconRailroads
                    .Select(br => br.Subdivision?.Railroad != null
                        ? $"{br.Subdivision.Railroad.Name} {br.Subdivision.Name}"
                        : br.Subdivision?.Name ?? string.Empty)
                    .Where(name => name.Length > 0)
                    .Distinct()
                    .ToList(),
                ExpectedSdrSharpCount = beacon.ExpectedSdrSharpCount,
                ExpectedSoftEotCount = beacon.ExpectedSoftEotCount,
                ExpectedSoftDpuCount = beacon.ExpectedSoftDpuCount,
                ExpectedSdrSliceCount = beacon.ExpectedSdrSliceCount,
                StatusSince = state?.Status == result.Status ? state.StatusSince : null,
                AlertsMuted = isMuted,
                AlertsMutedUntil = isMuted ? state!.AlertsMutedUntil : null,
                AlertsMuteNote = isMuted ? state!.AlertsMuteNote : null,
                AlertsMutedBy = isMuted ? state!.AlertsMutedBy : null,
                DeviceKeySetAt = deviceKey?.CreatedAt,
                DeviceKeySetBy = deviceKey?.CreatedBy,
                DeviceKeyRejectedAt = deviceKey?.LastRejectedAt,
                RebootsLast24Hours = ServerCommandService.CountStartedSince(commands, BeaconCommandType.Reboot, utcNow.AddDays(-1)),
                MaxRebootsPerDay = ServerCommandService.MaxRebootsPerDay,
                Commands = commands
                    .Take(CommandHistoryLimit)
                    .Select(c => ServerCommandService.ToDto(c, utcNow))
                    .ToList(),
                Programs = BeaconServerStatusEvaluator.GetExpectedPrograms(beacon)
                    .Select(p =>
                    {
                        var reported = reportedDiagnostics?.Programs
                            .FirstOrDefault(rp => string.Equals(rp.Name, p.Name, StringComparison.OrdinalIgnoreCase));
                        return new ServerProgramDTO
                        {
                            Name = p.Name,
                            Expected = p.Expected,
                            Running = reportedDiagnostics == null ? null : reported?.Running ?? 0,
                            NotResponding = reported?.NotResponding ?? 0,
                        };
                    })
                    .ToList(),
                Diagnostics = diagnostics == null ? null : _mapper.Map<ServerDiagnosticsDTO>(diagnostics),
            };
        }

        /// <summary>
        /// Health checks update every BeaconRailroad's LastUpdate, so the newest of those (or of the
        /// diagnostics report, for a beacon not yet linked to a railroad) is the last time the PC was heard from.
        /// </summary>
        public static DateTime? GetLastCheckIn(Beacon beacon, BeaconDiagnostics? diagnostics)
        {
            var times = beacon.BeaconRailroads
                .Select(br => br.LastUpdate)
                .Append(diagnostics?.LastUpdate ?? default)
                .Where(t => t != default)
                .ToList();

            return times.Count == 0 ? null : times.Max();
        }

        private static bool IsCustodianOf(User user, Beacon beacon) => ServerSupportUsers.IsCustodianOf(user, beacon);

        private static bool HasRole(User user, string roleName) => ServerSupportUsers.HasRole(user, roleName);
    }
}
