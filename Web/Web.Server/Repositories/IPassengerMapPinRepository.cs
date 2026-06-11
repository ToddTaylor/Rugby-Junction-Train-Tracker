using Web.Server.Entities;

namespace Web.Server.Repositories
{
    public interface IPassengerMapPinRepository
    {
        Task<IEnumerable<PassengerMapPin>> GetAllAsync();
        Task<PassengerMapPin?> GetByTrainIdAsync(string trainId);
        Task<PassengerMapPin?> GetByTrainNumberAsync(string trainNum);
        Task<PassengerMapPin> UpsertAsync(PassengerMapPin passengerMapPin);
        Task<bool> MarkStaleByTrainNumberAsync(string trainNum);
        Task<bool> DeleteByTrainIdAsync(string trainId);
        Task<bool> DeleteByTrainNumberAsync(string trainNum);
    }
}