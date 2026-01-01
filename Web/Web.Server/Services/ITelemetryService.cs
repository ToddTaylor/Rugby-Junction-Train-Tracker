using Web.Server.Entities;

namespace Web.Server.Services
{
    public interface ITelemetryService
    {
        Task<Telemetry> CreateMapPinAsync(Telemetry telemetry);
        Task<IEnumerable<Telemetry>> GetTelemetriesAsync();
        Task<Telemetry?> GetTelemetryByIdAsync(int id);
    }
}