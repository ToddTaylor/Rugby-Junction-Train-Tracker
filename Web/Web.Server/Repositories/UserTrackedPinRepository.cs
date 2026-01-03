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
    }
}
