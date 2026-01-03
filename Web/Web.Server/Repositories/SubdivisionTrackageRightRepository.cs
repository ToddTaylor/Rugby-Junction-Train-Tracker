using Microsoft.EntityFrameworkCore;
using Web.Server.Data;
using Web.Server.Entities;

namespace Web.Server.Repositories
{
    public interface ISubdivisionTrackageRightRepository
    {
        Task<IEnumerable<SubdivisionTrackageRight>> GetByFromSubdivisionAsync(int fromSubdivisionID);
        Task<SubdivisionTrackageRight?> GetByIdAsync(int id);
        Task<SubdivisionTrackageRight> AddAsync(SubdivisionTrackageRight trackageRight);
        Task DeleteAsync(int id);
        Task DeleteByFromSubdivisionAsync(int fromSubdivisionID);
    }

    public class SubdivisionTrackageRightRepository : ISubdivisionTrackageRightRepository
    {
        private readonly TelemetryDbContext _context;

        public SubdivisionTrackageRightRepository(TelemetryDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<SubdivisionTrackageRight>> GetByFromSubdivisionAsync(int fromSubdivisionID)
        {
            return await _context.SubdivisionTrackageRights
                .Where(str => str.FromSubdivisionID == fromSubdivisionID)
                .Include(str => str.FromSubdivision)
                .Include(str => str.ToSubdivision)
                .ToListAsync();
        }

        public async Task<SubdivisionTrackageRight?> GetByIdAsync(int id)
        {
            return await _context.SubdivisionTrackageRights
                .Include(str => str.FromSubdivision)
                .Include(str => str.ToSubdivision)
                .FirstOrDefaultAsync(str => str.ID == id);
        }

        public async Task<SubdivisionTrackageRight> AddAsync(SubdivisionTrackageRight trackageRight)
        {
            _context.SubdivisionTrackageRights.Add(trackageRight);
            await _context.SaveChangesAsync();
            return trackageRight;
        }

        public async Task DeleteAsync(int id)
        {
            var trackageRight = await GetByIdAsync(id);
            if (trackageRight != null)
            {
                _context.SubdivisionTrackageRights.Remove(trackageRight);
                await _context.SaveChangesAsync();
            }
        }

        public async Task DeleteByFromSubdivisionAsync(int fromSubdivisionID)
        {
            var trackageRights = await GetByFromSubdivisionAsync(fromSubdivisionID);
            if (trackageRights.Any())
            {
                _context.SubdivisionTrackageRights.RemoveRange(trackageRights);
                await _context.SaveChangesAsync();
            }
        }
    }
}
