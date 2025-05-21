using Web.Server.Entities;

namespace Web.Server.Services
{
    public interface ITelemetryService
    {
        Task<IEnumerable<Telemetry>> GetTelemetriesAsync();
        Task<Telemetry?> GetTelemetryByIdAsync(int id);
        Task<Telemetry> CreateTelemetryAsync(Telemetry alert);
    }
}