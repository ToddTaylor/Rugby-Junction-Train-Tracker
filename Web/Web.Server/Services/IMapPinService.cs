using Web.Server.Entities;

namespace Web.Server.Services
{
    public interface IMapPinService
    {
        Task<MapPin?> GetMapPinByIdAsync(int addressID);
        Task<IEnumerable<MapPin>> GetMapPinsAsync(int? minutes);
        Task UpsertMapPin(Telemetry telemetry, ICollection<BeaconRailroad> beaconRailroads);
    }
}