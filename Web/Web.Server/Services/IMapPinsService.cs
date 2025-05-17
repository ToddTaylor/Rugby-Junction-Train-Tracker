using Web.Server.Entities;

namespace Web.Server.Services
{
    public interface IMapPinsService
    {
        Task<MapPin> UpsertMapPin(MapPin mapPin);
        Task<IEnumerable<MapPin>> GetMapPinsAsync(int? minutes);
    }
}