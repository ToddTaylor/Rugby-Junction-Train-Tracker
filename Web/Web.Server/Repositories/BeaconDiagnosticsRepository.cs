using Microsoft.EntityFrameworkCore;
using Web.Server.Data;
using Web.Server.Entities;
using Web.Server.Providers;

namespace Web.Server.Repositories
{
    public class BeaconDiagnosticsRepository : IBeaconDiagnosticsRepository
    {
        private readonly TelemetryDbContext _context;
        private readonly ITimeProvider _timeProvider;

        public BeaconDiagnosticsRepository(TelemetryDbContext context, ITimeProvider timeProvider)
        {
            _context = context;
            _timeProvider = timeProvider;
        }

        public async Task<IEnumerable<BeaconDiagnostics>> GetAllAsync()
        {
            return await _context.BeaconDiagnostics.AsNoTracking().ToListAsync();
        }

        public async Task<BeaconDiagnostics> UpsertAsync(BeaconDiagnostics diagnostics)
        {
            var utcNow = _timeProvider.UtcNow;
            var existing = await _context.BeaconDiagnostics.FindAsync(diagnostics.BeaconID);

            if (existing == null)
            {
                diagnostics.CreatedAt = utcNow;
                diagnostics.LastUpdate = utcNow;
                _context.BeaconDiagnostics.Add(diagnostics);
                await _context.SaveChangesAsync();
                return diagnostics;
            }

            existing.LastUpdate = utcNow;
            existing.CollectedAt = diagnostics.CollectedAt;
            existing.Hostname = diagnostics.Hostname;
            existing.ClientVersion = diagnostics.ClientVersion;
            existing.OsVersion = diagnostics.OsVersion;
            existing.LastBootAt = diagnostics.LastBootAt;
            existing.LogDriveFreeBytes = diagnostics.LogDriveFreeBytes;
            existing.HealthPingIntervalMinutes = diagnostics.HealthPingIntervalMinutes;
            existing.LastTelemetrySentAt = diagnostics.LastTelemetrySentAt;
            existing.TelemetrySendFailuresLastHour = diagnostics.TelemetrySendFailuresLastHour;
            existing.Programs = diagnostics.Programs;
            existing.Logs = diagnostics.Logs;
            existing.StartupTasks = diagnostics.StartupTasks;

            await _context.SaveChangesAsync();
            return existing;
        }
    }
}
