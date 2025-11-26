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
            return await GetByIdAsync(beaconRailroad.BeaconID, beaconRailroad.SubdivisionID);
        }

        public async Task<IEnumerable<BeaconRailroad>> GetAllAsync()
        {
            return await _context.BeaconRailroads
                .Include(br => br.Beacon)
                .Include(br => br.Subdivision)
                .ThenInclude(s => s.Railroad)
                .OrderByDescending(br => br.LastUpdate)
                .ToListAsync();
        }

        public async Task<BeaconRailroad?> GetByIdAsync(int beaconId, int subdivisionId)
        {
            return await _context.BeaconRailroads
                .Include(br => br.Beacon)
                .Include(br => br.Subdivision)
                .ThenInclude(s => s.Railroad)
                .FirstOrDefaultAsync(br => br.BeaconID == beaconId && br.SubdivisionID == subdivisionId);
        }

        public async Task<BeaconRailroad> UpdateAsync(BeaconRailroad beaconRailroad)
        {
            var existing = await _context.BeaconRailroads
                .FirstOrDefaultAsync(br => br.BeaconID == beaconRailroad.BeaconID && br.SubdivisionID == beaconRailroad.SubdivisionID);

            if (existing == null)
            {
                throw new KeyNotFoundException("BeaconRailroad not found.");
            }

            existing.Direction = beaconRailroad.Direction;
            existing.Latitude = beaconRailroad.Latitude;
            existing.Longitude = beaconRailroad.Longitude;
            existing.Milepost = beaconRailroad.Milepost;
            existing.MultipleTracks = beaconRailroad.MultipleTracks;
            existing.LastUpdate = _timeProvider.UtcNow;

            await _context.SaveChangesAsync();

            // Re-query with all necessary includes for a fully hydrated object
            return await GetByIdAsync(beaconRailroad.BeaconID, beaconRailroad.SubdivisionID);
        }

        public async Task<bool> DeleteAsync(int beaconId, int railroadId)
        {
            var beaconRailroad = await _context.BeaconRailroads
                .FirstOrDefaultAsync(br => br.BeaconID == beaconId && br.SubdivisionID == railroadId);

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
