using Web.Server.Entities;

namespace Web.Server.Services
{
    public interface IMapPinService
    {
        Task<MapPin?> GetMapPinByIdAsync(int addressID, int? trainID);

        Task<IEnumerable<MapPin>> GetMapPinsAsync(int? minutes);

        Task<IEnumerable<MapPin>> GetMapPinsLatestAsync();

        Task UpsertMapPin(Telemetry telemetry, ICollection<BeaconRailroad> beaconRailroads);

        Task<bool> DeleteMapPinAsync(int id);
    }
}