using Web.Server.Entities;

namespace Web.Server.Services
{
    public interface ITelemetryRepository
    {
        Task<Telemetry> AddAsync(Telemetry alert);
    }
}
