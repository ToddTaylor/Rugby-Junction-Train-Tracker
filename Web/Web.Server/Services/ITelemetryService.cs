using Web.Server.Entities;

namespace Web.Server.Services
{
    public interface ITelemetryService
    {
        Task<IEnumerable<Telemetry>> GetTelemetries();

        void CreateTelemetry(Telemetry alert);
    }
}