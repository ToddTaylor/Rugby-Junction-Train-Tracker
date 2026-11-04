using Microsoft.EntityFrameworkCore;
using Web.Server.Data;
using Web.Server.Entities;
using Web.Server.Providers;

namespace Web.Server.Repositories
{
    public class BeaconDeviceKeyRepository : IBeaconDeviceKeyRepository
    {
        private readonly TelemetryDbContext _context;
        private readonly ITimeProvider _timeProvider;

        public BeaconDeviceKeyRepository(TelemetryDbContext context, ITimeProvider timeProvider)
        {
            _context = context;
            _timeProvider = timeProvider;
        }

        public async Task<BeaconDeviceKey?> GetAsync(int beaconId)
        {
            return await _context.BeaconDeviceKeys.AsNoTracking().FirstOrDefaultAsync(k => k.BeaconID == beaconId);
        }

        public async Task<IEnumerable<BeaconDeviceKey>> GetAllAsync()
        {
            return await _context.BeaconDeviceKeys.AsNoTracking().ToListAsync();
        }

        public async Task SetAsync(int beaconId, string keyHash, string createdBy)
        {
            var utcNow = _timeProvider.UtcNow;
            var existing = await _context.BeaconDeviceKeys.FindAsync(beaconId);
            if (existing == null)
            {
                existing = new BeaconDeviceKey { BeaconID = beaconId };
                _context.BeaconDeviceKeys.Add(existing);
            }

            existing.KeyHash = keyHash;
            existing.CreatedBy = createdBy;
            existing.CreatedAt = utcNow;
            existing.LastUpdate = utcNow;
            existing.LastRejectedAt = null;
            await _context.SaveChangesAsync();
        }

        public async Task<bool> DeleteAsync(int beaconId)
        {
            var existing = await _context.BeaconDeviceKeys.FindAsync(beaconId);
            if (existing == null)
            {
                return false;
            }

            _context.BeaconDeviceKeys.Remove(existing);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task RecordRejectedAsync(int beaconId)
        {
            var existing = await _context.BeaconDeviceKeys.FindAsync(beaconId);
            if (existing == null)
            {
                return;
            }

            existing.LastRejectedAt = _timeProvider.UtcNow;
            await _context.SaveChangesAsync();
        }
    }
}
