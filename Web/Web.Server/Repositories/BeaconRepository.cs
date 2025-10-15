using Microsoft.EntityFrameworkCore;
using Web.Server.Data;
using Web.Server.Entities;
using Web.Server.Providers;

namespace Web.Server.Repositories
{
    public class BeaconRepository : IBeaconRepository
    {
        private readonly TelemetryDbContext _context;
        private readonly ITimeProvider _timeProvider;

        public BeaconRepository(TelemetryDbContext context, ITimeProvider timeProvider)
        {
            _context = context;
            _timeProvider = timeProvider;
        }

        public async Task<Beacon> AddAsync(Beacon beacon)
        {
            beacon.CreatedAt = _timeProvider.UtcNow;
            beacon.LastUpdate = beacon.CreatedAt;

            _context.Beacons.Add(beacon);
            await _context.SaveChangesAsync();
            return beacon;
        }

        public async Task<IEnumerable<Beacon>> GetAllAsync()
        {
            return await _context.Beacons
                .Include(b => b.Owner)
                .Include(b => b.BeaconRailroads)
                .OrderByDescending(b => b.LastUpdate)
                .ToListAsync();
        }

        public async Task<Beacon?> GetByIdAsync(int id)
        {
            return await _context.Beacons
                .Include(b => b.Owner)
                .Include(b => b.BeaconRailroads)
                .FirstOrDefaultAsync(b => b.ID == id);
        }

        public async Task<Beacon> UpdateAsync(Beacon beacon)
        {
            var existingBeacon = await _context.Beacons.FindAsync(beacon.ID);
            if (existingBeacon == null)
            {
                throw new KeyNotFoundException("Beacon not found.");
            }

            existingBeacon.Name = beacon.Name;
            existingBeacon.Owner = beacon.Owner;
            existingBeacon.LastUpdate = _timeProvider.UtcNow;

            await _context.SaveChangesAsync();
            return existingBeacon;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var beacon = await _context.Beacons.FindAsync(id);
            if (beacon == null)
            {
                return false;
            }

            _context.Beacons.Remove(beacon);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
