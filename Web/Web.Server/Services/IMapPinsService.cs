using Web.Server.Entities;

namespace Web.Server.Services
{
    public interface IMapPinsService
    {
        Task<MapPin?> GetMapPinByIdAsync(int addressID);
        Task<IEnumerable<MapPin>> GetMapPinsAsync(int? minutes);
        Task UpsertMapPin(Telemetry telemetry, Beacon telemetryBeacon);
    }
}