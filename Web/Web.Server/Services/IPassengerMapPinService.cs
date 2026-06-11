using Web.Server.DTOs;
using Web.Server.Entities;

namespace Web.Server.Services
{
    public interface IPassengerMapPinService
    {
        Task<IEnumerable<PassengerMapPinDTO>> GetPassengerMapPinsAsync();
        Task<PassengerMapPinDTO> UpsertAsync(PassengerMapPin passengerMapPin);
        Task DeleteByTrainIdAsync(string trainId);
        Task DeleteByTrainNumberAsync(string trainNum);
    }
}