using Microsoft.EntityFrameworkCore;
using Web.Server.Data;
using Web.Server.Entities;

namespace Web.Server.Services
{
    public class TelemetryRepository : ITelemetryRepository
    {
        private readonly TelemetryDbContext _context;

        public TelemetryRepository(TelemetryDbContext context)
        {
            _context = context;
        }

        public async Task<Telemetry> AddAsync(Telemetry telemetry)
        {
            _context.Entry(telemetry).State = EntityState.Unchanged;

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
            existingTelemetry.Timestamp = telemetry.Timestamp;

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
