using MapsterMapper;
using Web.Server.DTOs;
using Web.Server.Entities;
using Web.Server.Providers;
using Web.Server.Repositories;

namespace Web.Server.Services
{
    public class ServerHealthService : IServerHealthService
    {
        private readonly IBeaconRepository _beaconRepository;
        private readonly IBeaconDiagnosticsRepository _diagnosticsRepository;
        private readonly IMapper _mapper;
        private readonly ITimeProvider _timeProvider;

        public ServerHealthService(
            IBeaconRepository beaconRepository,
            IBeaconDiagnosticsRepository diagnosticsRepository,
            IMapper mapper,
            ITimeProvider timeProvider)
        {
            _beaconRepository = beaconRepository;
            _diagnosticsRepository = diagnosticsRepository;
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
            var utcNow = _timeProvider.UtcNow;

            return beacons
                .Select(b => BuildRow(b, diagnosticsByBeacon.GetValueOrDefault(b.ID), utcNow))
                .OrderBy(r => r.BeaconName, StringComparer.OrdinalIgnoreCase)
                .ToList();
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
                expectations.ExpectedSoftDpuCount);
            return beacon != null;
        }

        private ServerHealthDTO BuildRow(Beacon beacon, BeaconDiagnostics? diagnostics, DateTime utcNow)
        {
            var lastCheckIn = GetLastCheckIn(beacon, diagnostics);
            var result = BeaconServerStatusEvaluator.Evaluate(beacon, diagnostics, lastCheckIn, utcNow);
            var reportedDiagnostics = result.UsesDiagnostics ? diagnostics : null;

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
        private static DateTime? GetLastCheckIn(Beacon beacon, BeaconDiagnostics? diagnostics)
        {
            var times = beacon.BeaconRailroads
                .Select(br => br.LastUpdate)
                .Append(diagnostics?.LastUpdate ?? default)
                .Where(t => t != default)
                .ToList();

            return times.Count == 0 ? null : times.Max();
        }

        private static bool IsCustodianOf(User user, Beacon beacon) =>
            beacon.BeaconRailroads.Any(br => br.Subdivision?.CustodianId == user.ID);

        private static bool HasRole(User user, string roleName)
        {
            return user.UserRoles?.Any(ur =>
                string.Equals(ur.Role?.RoleName, roleName, StringComparison.OrdinalIgnoreCase)) == true;
        }
    }
}
