using Web.Server.Entities;

namespace Web.Server.Repositories
{
    public interface IMapPinRepository
    {
        Task<MapPin?> GetByIdAsync(int addressID);

        Task<IEnumerable<MapPin>> GetAllAsync(int? minutes);
        Task<MapPin> UpsertAsync(MapPin mapPin);
    }
}
