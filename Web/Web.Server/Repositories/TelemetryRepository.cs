using Microsoft.EntityFrameworkCore;
using Web.Server.Data;
using Web.Server.Entities;
using Web.Server.Providers;

namespace Web.Server.Repositories
{
    public class TelemetryRepository : ITelemetryRepository
    {
        private readonly TelemetryDbContext _context;
        private readonly ITimeProvider _timeProvider;

        public TelemetryRepository(TelemetryDbContext context, ITimeProvider timeProvider)
        {
            _context = context;
            _timeProvider = timeProvider;
        }

        public async Task<Telemetry> AddAsync(Telemetry telemetry)
        {

            telemetry.Beacon = null;

            _context.Telemetries.Add(telemetry);
            await _context.SaveChangesAsync();

            return await _context.Telemetries
                .Include(t => t.Beacon)
                .ThenInclude(b => b.BeaconRailroads)
                .ThenInclude(br => br.Subdivision)
                .FirstAsync(t => t.ID == telemetry.ID);
        }

        public async Task<IEnumerable<Telemetry>> GetAllAsync()
        {
            return await _context.Telemetries
                .Include(t => t.Beacon)
                .ThenInclude(b => b.BeaconRailroads)
                .ThenInclude(br => br.Subdivision)
                .OrderByDescending(t => t.LastUpdate)
                .ToListAsync();
        }

        public async Task<Telemetry?> GetByIdAsync(int id)
        {
            return await _context.Telemetries
                .Include(t => t.Beacon)
                .ThenInclude(b => b.BeaconRailroads)
                .ThenInclude(br => br.Subdivision)
                .FirstOrDefaultAsync(t => t.ID == id);
        }

        public async Task<Telemetry> UpdateAsync(Telemetry telemetry)
        {
            var existingTelemetry = await _context.Telemetries.FindAsync(telemetry.ID);
            if (existingTelemetry == null)
            {
                throw new KeyNotFoundException("Telemetry not found.");
            }

            existingTelemetry.Beacon = telemetry.Beacon;
            existingTelemetry.AddressID = telemetry.AddressID;
            existingTelemetry.TrainID = telemetry.TrainID;
            existingTelemetry.BrakePipePressure = telemetry.BrakePipePressure;
            existingTelemetry.Moving = telemetry.Moving;
            existingTelemetry.Source = telemetry.Source;
            existingTelemetry.Discarded = telemetry.Discarded;
            existingTelemetry.DiscardReason = telemetry.DiscardReason;
            existingTelemetry.LastUpdate = telemetry.LastUpdate;

            await _context.SaveChangesAsync();
            return existingTelemetry;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var telemetry = await _context.Telemetries.FindAsync(id);
            if (telemetry == null)
            {
                return false;
            }

            _context.Telemetries.Remove(telemetry);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<Telemetry?> GetRecentWithinTimeOffsetAsync(int trainId, int beaconId, int railroadId, DateTime sinceUtc)
        {
            return await _context.Telemetries
                .Where(t => t.TrainID == trainId
                    && t.BeaconID == beaconId
                    && t.CreatedAt >= sinceUtc
                    && t.Beacon.BeaconRailroads.Any(br => br.BeaconID == t.BeaconID && br.Subdivision.RailroadID == railroadId)
                    && t.Discarded == false)
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<List<Telemetry>> GetRecentsWithinTimeOffsetAsync(int addressId, int railroadId, DateTime sinceUtc)
        {
            return await _context.Telemetries
                .Where(t => t.AddressID == addressId
                    && t.CreatedAt >= sinceUtc
                    && t.Beacon.BeaconRailroads.Any(br => br.Subdivision.RailroadID == railroadId)
                    && t.Discarded == false)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
        }

        public async Task<Telemetry?> GetMostRecentByAddressAsync(int addressId)
        {
            return await _context.Telemetries
                .Where(t => t.AddressID == addressId && t.Discarded == false)
                .Include(t => t.Beacon)
                    .ThenInclude(b => b.BeaconRailroads)
                    .ThenInclude(br => br.Subdivision)
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<List<Telemetry>> GetRecentsForTrainWithinTimeOffsetAsync(int trainId, int railroadId, DateTime sinceUtc)
        {
            return await _context.Telemetries
                .Where(t => t.TrainID == trainId
                    && t.CreatedAt >= sinceUtc
                    && t.Beacon.BeaconRailroads.Any(br => br.Subdivision.RailroadID == railroadId)
                    && t.Discarded == false)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
        }
    }
}
