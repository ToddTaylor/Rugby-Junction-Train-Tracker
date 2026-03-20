using Web.Server.Entities;

namespace Web.Server.Services
{
    public interface IMapPinService
    {
        Task<MapPin?> GetMapPinByIdAsync(int addressID);

        Task<IEnumerable<MapPin>> GetMapPinsAsync(int? minutes);

        Task<IEnumerable<MapPin>> GetMapPinsLatestAsync();

        Task UpsertMapPin(Telemetry telemetry);

        Task<bool> DeleteMapPinAsync(int id);
    }
}