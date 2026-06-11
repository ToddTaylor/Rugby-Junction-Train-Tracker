using Web.Server.Entities;

namespace Web.Server.Repositories
{
    public interface IAmtrakTrackedTrainRepository
    {
        Task<IEnumerable<AmtrakTrackedTrain>> GetAllAsync();
        Task<IEnumerable<AmtrakTrackedTrain>> GetActiveAsync();
        Task<AmtrakTrackedTrain?> GetByIdAsync(int id);
        Task<AmtrakTrackedTrain?> GetByTrainNumberAsync(string trainNumber);
        Task<AmtrakTrackedTrain> AddAsync(AmtrakTrackedTrain trackedTrain);
        Task<bool> DeleteAsync(int id);
    }
}