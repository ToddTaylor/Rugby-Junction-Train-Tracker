using Microsoft.EntityFrameworkCore;
using Web.Server.Data;
using Web.Server.Entities;
using Web.Server.Providers;

namespace Web.Server.Repositories
{
    public class AmtrakTrackedTrainRepository : IAmtrakTrackedTrainRepository
    {
        private readonly TelemetryDbContext _context;
        private readonly ITimeProvider _timeProvider;

        public AmtrakTrackedTrainRepository(TelemetryDbContext context, ITimeProvider timeProvider)
        {
            _context = context;
            _timeProvider = timeProvider;
        }

        public async Task<IEnumerable<AmtrakTrackedTrain>> GetAllAsync()
        {
            return await _context.AmtrakTrackedTrains
                .OrderBy(t => t.TrainNumber)
                .ToListAsync();
        }

        public async Task<IEnumerable<AmtrakTrackedTrain>> GetActiveAsync()
        {
            return await _context.AmtrakTrackedTrains
                .Where(t => t.IsActive)
                .OrderBy(t => t.TrainNumber)
                .ToListAsync();
        }

        public async Task<AmtrakTrackedTrain?> GetByIdAsync(int id)
        {
            return await _context.AmtrakTrackedTrains.FirstOrDefaultAsync(t => t.ID == id);
        }

        public async Task<AmtrakTrackedTrain?> GetByTrainNumberAsync(string trainNumber)
        {
            var normalized = trainNumber.Trim();
            return await _context.AmtrakTrackedTrains.FirstOrDefaultAsync(t => t.TrainNumber == normalized);
        }

        public async Task<AmtrakTrackedTrain> AddAsync(AmtrakTrackedTrain trackedTrain)
        {
            trackedTrain.CreatedAt = _timeProvider.UtcNow;
            trackedTrain.LastUpdate = trackedTrain.CreatedAt;
            trackedTrain.TrainNumber = trackedTrain.TrainNumber.Trim();
            _context.AmtrakTrackedTrains.Add(trackedTrain);
            await _context.SaveChangesAsync();
            return trackedTrain;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var entity = await _context.AmtrakTrackedTrains.FindAsync(id);
            if (entity == null)
            {
                return false;
            }

            _context.AmtrakTrackedTrains.Remove(entity);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}