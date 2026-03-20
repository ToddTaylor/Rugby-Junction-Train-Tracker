using Microsoft.EntityFrameworkCore;
using Web.Server.Data;
using Web.Server.Entities;
using Web.Server.Providers;

namespace Web.Server.Repositories
{
    public class MapPinHistoryRepository : IMapPinHistoryRepository
    {
        private readonly TelemetryDbContext _context;
        private readonly ITimeProvider _timeProvider;

        public MapPinHistoryRepository(TelemetryDbContext context, ITimeProvider timeProvider)
        {
            _context = context;
            _timeProvider = timeProvider;
        }

        public async Task<IEnumerable<MapPinHistory>> GetByBeaconIdAsync(int beaconId, int? subdivisionId = null, int? limit = null)
        {
            var query = _context.MapPinHistories
                .Where(mph => mph.BeaconID == beaconId);

            if (subdivisionId.HasValue)
            {
                query = query.Where(mph => mph.SubdivisionId == subdivisionId.Value);
            }

            query = query.OrderByDescending(mph => mph.LastUpdate);

            if (limit.HasValue)
            {
                query = (IOrderedQueryable<MapPinHistory>)query.Take(limit.Value);
            }

            return await query.ToListAsync();
        }

        public async Task<MapPinHistory?> GetByOriginalMapPinIdAsync(int mapPinId)
        {
            return await _context.MapPinHistories
                .Where(mph => mph.OriginalMapPinID == mapPinId)
                .OrderByDescending(mph => mph.LastUpdate)
                .FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<MapPinHistory>> GetLatestPerBeaconAsync()
        {
            var histories = await _context.MapPinHistories
                .GroupBy(mph => new { mph.BeaconID, mph.SubdivisionId })
                .Select(g => g.OrderByDescending(mph => mph.LastUpdate).First())
                .ToListAsync();

            return histories;
        }

        public async Task<MapPinHistory> AddAsync(MapPinHistory mapPinHistory)
        {
            mapPinHistory.CreatedAt = _timeProvider.UtcNow;
            mapPinHistory.LastUpdate = mapPinHistory.CreatedAt;

            _context.MapPinHistories.Add(mapPinHistory);
            await _context.SaveChangesAsync();
            return mapPinHistory;
        }

        public async Task<MapPinHistory> UpdateAsync(MapPinHistory mapPinHistory)
        {
            mapPinHistory.LastUpdate = _timeProvider.UtcNow;

            _context.MapPinHistories.Update(mapPinHistory);
            await _context.SaveChangesAsync();
            return mapPinHistory;
        }

            public async Task DeleteByOriginalMapPinIdAsync(int mapPinId)
            {
                var records = await _context.MapPinHistories
                    .Where(mph => mph.OriginalMapPinID == mapPinId)
                    .ToListAsync();

                if (records.Count > 0)
                {
                    _context.MapPinHistories.RemoveRange(records);
                    await _context.SaveChangesAsync();
                }
            }
    }
}
