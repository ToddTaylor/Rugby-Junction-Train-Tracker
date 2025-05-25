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
            telemetry.CreatedAt = _timeProvider.UtcNow;
            telemetry.LastUpdate = telemetry.CreatedAt;

            telemetry.Beacon = null;

            _context.Telemetries.Add(telemetry);
            await _context.SaveChangesAsync();

            return await _context.Telemetries
                .Include(t => t.Beacon)
                .ThenInclude(b => b.BeaconRailroads)
                .ThenInclude(br => br.Railroad)
                .FirstAsync(t => t.ID == telemetry.ID);
        }

        public async Task<IEnumerable<Telemetry>> GetAllAsync()
        {
            return await _context.Telemetries
                .Include(t => t.Beacon)
                .ThenInclude(b => b.BeaconRailroads)
                .ThenInclude(br => br.Railroad)
                .OrderByDescending(t => t.LastUpdate)
                .ToListAsync();
        }

        public async Task<Telemetry?> GetByIdAsync(int id)
        {
            return await _context.Telemetries
                .Include(t => t.Beacon)
                .ThenInclude(b => b.BeaconRailroads)
                .ThenInclude(br => br.Railroad)
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
            existingTelemetry.Moving = telemetry.Moving;
            existingTelemetry.Source = telemetry.Source;
            existingTelemetry.LastUpdate = _timeProvider.UtcNow;

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
    }
}
