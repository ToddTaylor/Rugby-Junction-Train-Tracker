using Web.Server.Entities;

namespace Web.Server.Services
{
    public interface IRailroadService
    {
        Task<Railroad> CreateRailroadAsync(Railroad railroad);
        Task DeleteRailroadAsync(int ID);
        Task<Railroad> GetRailroadAsync(int ID);
        Task<IEnumerable<Railroad>> GetRailroadsAsync();
        Task<Railroad> UpdateRailroadAsync(Railroad railroad);
    }
}