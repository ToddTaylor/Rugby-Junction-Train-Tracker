using Microsoft.EntityFrameworkCore;
using Web.Server.Entities;
using Web.Server.Providers;

namespace Web.Server.Repositories
{
    public class BeaconRailroadRepository : IBeaconRailroadRepository
    {
        private readonly Data.TelemetryDbContext _context;
        private readonly ITimeProvider _timeProvider;

        public BeaconRailroadRepository(Data.TelemetryDbContext context, ITimeProvider timeProvider)
        {
            _context = context;
            _timeProvider = timeProvider;
        }

        public async Task<BeaconRailroad> AddAsync(BeaconRailroad beaconRailroad)
        {
            beaconRailroad.CreatedAt = _timeProvider.UtcNow;
            beaconRailroad.LastUpdate = beaconRailroad.CreatedAt;

            _context.BeaconRailroads.Add(beaconRailroad);
            await _context.SaveChangesAsync();
            return beaconRailroad;
        }

        public async Task<IEnumerable<BeaconRailroad>> GetAllAsync()
        {
            return await _context.BeaconRailroads
                .Include(br => br.Beacon)
                .Include(br => br.Railroad)
                .OrderByDescending(br => br.LastUpdate)
                .ToListAsync();
        }

        public async Task<BeaconRailroad?> GetByIdAsync(int beaconId, int railroadId)
        {
            return await _context.BeaconRailroads
                .Include(br => br.Beacon)
                .Include(br => br.Railroad)
                .FirstOrDefaultAsync(br => br.BeaconID == beaconId && br.RailroadID == railroadId);
        }

        public async Task<BeaconRailroad> UpdateAsync(BeaconRailroad beaconRailroad)
        {
            var existing = await _context.BeaconRailroads
                .FirstOrDefaultAsync(br => br.BeaconID == beaconRailroad.BeaconID && br.RailroadID == beaconRailroad.RailroadID);

            if (existing == null)
            {
                throw new KeyNotFoundException("BeaconRailroad not found.");
            }

            existing.Latitude = beaconRailroad.Latitude;
            existing.Longitude = beaconRailroad.Longitude;
            existing.Milepost = beaconRailroad.Milepost;
            existing.LastUpdate = _timeProvider.UtcNow;

            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<bool> DeleteAsync(int beaconId, int railroadId)
        {
            var beaconRailroad = await _context.BeaconRailroads
                .FirstOrDefaultAsync(br => br.BeaconID == beaconId && br.RailroadID == railroadId);

            if (beaconRailroad == null)
            {
                return false;
            }

            _context.BeaconRailroads.Remove(beaconRailroad);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
