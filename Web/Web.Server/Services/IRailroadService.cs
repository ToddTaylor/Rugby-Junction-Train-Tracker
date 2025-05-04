using Web.Server.Entities;

namespace Web.Server.Services
{
    public interface IRailroadService
    {
        Task<Railroad> CreateRailroad(Railroad railroad);
        void DeleteRailroad(int ID);
        Task<Railroad> GetRailroad(int ID);
        Task<IEnumerable<Railroad>> GetRailroads();
        void UpdateRailroad(Railroad railroad);
    }
}