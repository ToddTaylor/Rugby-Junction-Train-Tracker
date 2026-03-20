using Microsoft.EntityFrameworkCore;
using Web.Server.Data;
using Web.Server.Entities;

namespace Web.Server.Repositories
{
    public class UserTrackedPinRepository : IUserTrackedPinRepository
    {
        private readonly TelemetryDbContext _context;

        public UserTrackedPinRepository(TelemetryDbContext context)
        {
            _context = context;
        }

        public async Task<UserTrackedPin?> GetByIdAsync(int id)
        {
            return await _context.UserTrackedPins.FindAsync(id);
        }

        public async Task<IEnumerable<UserTrackedPin>> GetByUserIdAsync(int userId)
        {
            return await _context.UserTrackedPins
                .Where(utp => utp.UserId == userId && utp.ExpiresUtc > DateTime.UtcNow)
                .ToListAsync();
        }

        public async Task<UserTrackedPin?> GetByUserAndMapPinAsync(int userId, int mapPinId)
        {
            return await _context.UserTrackedPins
                .FirstOrDefaultAsync(utp => utp.UserId == userId && utp.MapPinId == mapPinId && utp.ExpiresUtc > DateTime.UtcNow);
        }

        public async Task<IEnumerable<UserTrackedPin>> GetByMapPinIdAsync(int mapPinId)
        {
            return await _context.UserTrackedPins
                .Where(utp => utp.MapPinId == mapPinId)
                .ToListAsync();
        }

        public async Task<UserTrackedPin> AddAsync(UserTrackedPin trackedPin)
        {
            _context.UserTrackedPins.Add(trackedPin);
            await _context.SaveChangesAsync();
            return trackedPin;
        }

        public async Task UpdateAsync(UserTrackedPin trackedPin)
        {
            _context.UserTrackedPins.Update(trackedPin);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var trackedPin = await GetByIdAsync(id);
            if (trackedPin != null)
            {
                _context.UserTrackedPins.Remove(trackedPin);
                await _context.SaveChangesAsync();
            }
        }

        public async Task DeleteByUserAndMapPinAsync(int userId, int mapPinId)
        {
            var trackedPin = await GetByUserAndMapPinAsync(userId, mapPinId);
            if (trackedPin != null)
            {
                _context.UserTrackedPins.Remove(trackedPin);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<IEnumerable<UserTrackedPin>> GetExpiredAsync(DateTime nowUtc)
        {
            return await _context.UserTrackedPins
                .Where(utp => utp.ExpiresUtc <= nowUtc)
                .ToListAsync();
        }

        public async Task DeleteExpiredAsync()
        {
            var expired = await GetExpiredAsync(DateTime.UtcNow);
            if (expired.Any())
            {
                _context.UserTrackedPins.RemoveRange(expired);
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateMapPinIdAsync(int oldMapPinId, int newMapPinId)
        {
            var sourceTrackedPins = await _context.UserTrackedPins
                .Where(utp => utp.MapPinId == oldMapPinId)
                .ToListAsync();

            if (sourceTrackedPins.Count == 0)
            {
                return;
            }

            var destinationPins = (await _context.UserTrackedPins
                .Where(utp => utp.MapPinId == newMapPinId)
                .ToListAsync())
                .ToDictionary(utp => utp.UserId, utp => utp);

            var toDelete = new List<UserTrackedPin>();
            var toMove = new List<UserTrackedPin>();
            var destinationToUpdate = new Dictionary<int, UserTrackedPin>();

            foreach (var sourcePin in sourceTrackedPins)
            {
                if (destinationPins.TryGetValue(sourcePin.UserId, out var destinationPin))
                {
                    // If both pins are tracked by the same user, keep a single tracking row
                    // and preserve the first-tracked symbol text.
                    if (!string.IsNullOrWhiteSpace(sourcePin.Symbol))
                    {
                        destinationPin.Symbol = sourcePin.Symbol;
                    }

                    destinationPin.LastUpdate = DateTime.UtcNow;
                    destinationToUpdate[destinationPin.ID] = destinationPin;
                    toDelete.Add(sourcePin);
                }
                else
                {
                    sourcePin.MapPinId = newMapPinId;
                    sourcePin.LastUpdate = DateTime.UtcNow;
                    toMove.Add(sourcePin);
                }
            }

            if (toDelete.Count > 0)
            {
                _context.UserTrackedPins.RemoveRange(toDelete);
            }

            if (destinationToUpdate.Count > 0)
            {
                _context.UserTrackedPins.UpdateRange(destinationToUpdate.Values);
            }

            if (toMove.Count > 0)
            {
                _context.UserTrackedPins.UpdateRange(toMove);
            }

            if (toDelete.Count > 0 || toMove.Count > 0 || destinationToUpdate.Count > 0)
            {
                await _context.SaveChangesAsync();
            }
        }
    }
}
