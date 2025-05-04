using Web.Server.Entities;

namespace Web.Server.Services
{
    public interface ITelemetryRepository
    {
        Task<Telemetry> AddAsync(Telemetry telemetry);
        Task<IEnumerable<Telemetry>> GetAllAsync();
        Task<Telemetry?> GetByIdAsync(int id);
        Task<Telemetry> UpdateAsync(Telemetry telemetry);
        Task<bool> DeleteAsync(int id);
    }
}
