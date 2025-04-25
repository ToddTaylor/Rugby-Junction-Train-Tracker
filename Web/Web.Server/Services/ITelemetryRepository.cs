using Web.Server.Models;

namespace Web.Server.Services
{
    public interface ITelemetryRepository
    {
        Task<Telemetry> AddAsync(Telemetry alert);
    }
}
