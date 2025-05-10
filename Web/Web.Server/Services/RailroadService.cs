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

        public async Task<Railroad> GetRailroad(int id)
        {
            return await _railroadRepository.GetByIdAsync(id);
        }

        public async Task<IEnumerable<Railroad>> GetRailroads()
        {
            return await _railroadRepository.GetAllAsync();
        }

        public async Task<Railroad> CreateRailroad(Railroad railroad)
        {
            return await _railroadRepository.AddAsync(railroad);
        }

        public async Task UpdateRailroad(Railroad railroad)
        {
            await _railroadRepository.UpdateAsync(railroad);
        }

        public async Task DeleteRailroad(int id)
        {
            await _railroadRepository.DeleteAsync(id);
        }
    }
}

