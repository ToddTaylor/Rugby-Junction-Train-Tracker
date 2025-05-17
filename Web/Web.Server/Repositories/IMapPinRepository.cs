using Web.Server.Entities;

namespace Web.Server.Repositories
{
    public interface IMapPinRepository
    {
        Task<MapPin> UpsertAsync(MapPin mapPin);
        Task<IEnumerable<MapPin>> GetAllAsync(int? minutes);
    }
}
