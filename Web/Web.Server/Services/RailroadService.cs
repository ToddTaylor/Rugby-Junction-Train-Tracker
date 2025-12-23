using Web.Server.Entities;
using Web.Server.Repositories;

namespace Web.Server.Services
{
    public class RailroadService : IRailroadService
    {
        private readonly IRailroadRepository _railroadRepository;

        public RailroadService(IRailroadRepository railroadRepository)
        {
            _railroadRepository = railroadRepository;
        }

        public async Task<Railroad> GetRailroadAsync(int id)
        {
            return await _railroadRepository.GetByIdAsync(id);
        }

        public async Task<IEnumerable<Railroad>> GetRailroadsAsync()
        {
            return await _railroadRepository.GetAllAsync();
        }

        public async Task<Railroad> CreateRailroadAsync(Railroad railroad)
        {
            return await _railroadRepository.AddAsync(railroad);
        }

        public async Task<Railroad> UpdateRailroadAsync(Railroad railroad)
        {
            return await _railroadRepository.UpdateAsync(railroad);
        }

        public async Task DeleteRailroadAsync(int id)
        {
            await _railroadRepository.DeleteAsync(id);
        }
    }
}

