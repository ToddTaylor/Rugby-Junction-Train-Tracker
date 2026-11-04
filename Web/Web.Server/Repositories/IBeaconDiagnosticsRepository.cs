using Web.Server.Entities;

namespace Web.Server.Repositories
{
    public interface IBeaconDiagnosticsRepository
    {
        Task<IEnumerable<BeaconDiagnostics>> GetAllAsync();

        Task<BeaconDiagnostics?> GetAsync(int beaconId);

        /// <summary>
        /// Replaces the stored report for the beacon, or adds one if none exists.
        /// </summary>
        Task<BeaconDiagnostics> UpsertAsync(BeaconDiagnostics diagnostics);
    }
}
