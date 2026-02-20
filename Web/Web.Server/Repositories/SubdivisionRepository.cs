using Microsoft.EntityFrameworkCore;
using Web.Server.Data;
using Web.Server.Entities;
using Web.Server.Providers;

namespace Web.Server.Repositories
{
    public class SubdivisionRepository : ISubdivisionRepository
    {
        private readonly TelemetryDbContext _context;
        private readonly ITimeProvider _timeProvider;

        public SubdivisionRepository(TelemetryDbContext context, ITimeProvider timeProvider)
        {
            _context = context;
            _timeProvider = timeProvider;
        }

        public async Task<Subdivision> AddAsync(Subdivision subdivision)
        {
            subdivision.CreatedAt = _timeProvider.UtcNow;
            subdivision.LastUpdate = subdivision.CreatedAt;

            // Attach custodian if provided
            if (subdivision.CustodianId.HasValue)
            {
                subdivision.Custodian = await _context.Users.FindAsync(subdivision.CustodianId.Value);
            }

            _context.Subdivisions.Add(subdivision);
            await _context.SaveChangesAsync();
            return subdivision;
        }

        public async Task<IEnumerable<Subdivision>> GetAllAsync()
        {
            return await _context.Subdivisions
                .Include(s => s.Railroad)
                .OrderByDescending(r => r.LastUpdate)
                .ToListAsync();
        }

        public async Task<Subdivision?> GetByIdAsync(int id)
        {
            return await _context.Subdivisions
                .Include(s => s.Railroad)
                .FirstOrDefaultAsync(r => r.ID == id);
        }

        public async Task<Subdivision> UpdateAsync(Subdivision sudivision)
        {
            var existingSubdivision = await _context.Subdivisions
                .Include(s => s.Railroad)
                .FirstOrDefaultAsync(s => s.ID == sudivision.ID);
            if (existingSubdivision == null)
            {
                throw new KeyNotFoundException("Subdivision not found.");
            }


            existingSubdivision.Name = sudivision.Name;
            existingSubdivision.RailroadID = sudivision.RailroadID;
            existingSubdivision.DpuCapable = sudivision.DpuCapable;
            existingSubdivision.LocalTrainAddressIDs = sudivision.LocalTrainAddressIDs;
            existingSubdivision.LastUpdate = _timeProvider.UtcNow;

            // Update custodian if provided
            if (sudivision.CustodianId.HasValue)
            {
                existingSubdivision.CustodianId = sudivision.CustodianId;
                existingSubdivision.Custodian = await _context.Users.FindAsync(sudivision.CustodianId.Value);
            }
            else
            {
                existingSubdivision.CustodianId = null;
                existingSubdivision.Custodian = null;
            }

            await _context.SaveChangesAsync();
            return existingSubdivision;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var subdivision = await _context.Subdivisions.FindAsync(id);
            if (subdivision == null)
            {
                return false;
            }

            _context.Subdivisions.Remove(subdivision);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
