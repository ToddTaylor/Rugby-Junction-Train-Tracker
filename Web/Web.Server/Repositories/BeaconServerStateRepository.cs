using Microsoft.EntityFrameworkCore;
using Web.Server.Data;
using Web.Server.Entities;
using Web.Server.Providers;

namespace Web.Server.Repositories
{
    public class BeaconServerStateRepository : IBeaconServerStateRepository
    {
        private readonly TelemetryDbContext _context;
        private readonly ITimeProvider _timeProvider;

        public BeaconServerStateRepository(TelemetryDbContext context, ITimeProvider timeProvider)
        {
            _context = context;
            _timeProvider = timeProvider;
        }

        public async Task<IEnumerable<BeaconServerState>> GetAllAsync()
        {
            return await _context.BeaconServerStates.AsNoTracking().ToListAsync();
        }

        public async Task UpdateStatusAsync(BeaconServerState state)
        {
            var existing = await GetOrAddAsync(state.BeaconID);
            existing.Status = state.Status;
            existing.StatusSince = state.StatusSince;
            existing.PendingStatus = state.PendingStatus;
            existing.PendingSince = state.PendingSince;
            await _context.SaveChangesAsync();
        }

        public async Task SetMuteAsync(int beaconId, DateTime? mutedUntil, string? note, string mutedBy)
        {
            var existing = await GetOrAddAsync(beaconId);
            existing.AlertsMutedAt = _timeProvider.UtcNow;
            existing.AlertsMutedUntil = mutedUntil;
            existing.AlertsMuteNote = note;
            existing.AlertsMutedBy = mutedBy;
            await _context.SaveChangesAsync();
        }

        public async Task ClearMuteAsync(int beaconId)
        {
            var existing = await _context.BeaconServerStates.FindAsync(beaconId);
            if (existing == null)
            {
                return;
            }

            existing.AlertsMutedAt = null;
            existing.AlertsMutedUntil = null;
            existing.AlertsMuteNote = null;
            existing.AlertsMutedBy = null;
            existing.LastUpdate = _timeProvider.UtcNow;
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// A beacon seen for the first time starts Online; the caller sets the real status before saving.
        /// </summary>
        private async Task<BeaconServerState> GetOrAddAsync(int beaconId)
        {
            var utcNow = _timeProvider.UtcNow;
            var existing = await _context.BeaconServerStates.FindAsync(beaconId);
            if (existing == null)
            {
                existing = new BeaconServerState { BeaconID = beaconId, StatusSince = utcNow, CreatedAt = utcNow };
                _context.BeaconServerStates.Add(existing);
            }

            existing.LastUpdate = utcNow;
            return existing;
        }
    }
}
