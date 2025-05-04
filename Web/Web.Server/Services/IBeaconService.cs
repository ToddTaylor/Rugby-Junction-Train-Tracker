using Web.Server.Entities;

namespace Web.Server.Services
{
    public interface IBeaconService
    {
        Task<IEnumerable<Beacon>> GetBeaconsAsync();
        Task<Beacon?> GetBeaconByIdAsync(int id);
        Task<Beacon> CreateBeaconAsync(Beacon beacon);
        Task<Beacon> UpdateBeaconAsync(int id, Beacon beacon);
        Task<bool> DeleteBeaconAsync(int id);
    }
}
