using Web.Server.Entities;
using Web.Server.Repositories;

namespace Web.Server.Services
{
    public class BeaconRailroadService : IBeaconRailroadService
    {
        private readonly IBeaconRailroadRepository _repository;

        public BeaconRailroadService(IBeaconRailroadRepository repository)
        {
            _repository = repository;
        }

        public async Task<BeaconRailroad> AddAsync(BeaconRailroad beaconRailroad)
        {
            return await _repository.AddAsync(beaconRailroad);
        }

        public async Task<IEnumerable<BeaconRailroad>> GetAllAsync()
        {
            return await _repository.GetAllAsync();
        }

        public async Task<BeaconRailroad?> GetByIdAsync(int beaconId, int railroadId)
        {
            return await _repository.GetByIdAsync(beaconId, railroadId);
        }

        public async Task<BeaconRailroad> UpdateAsync(BeaconRailroad beaconRailroad)
        {
            return await _repository.UpdateAsync(beaconRailroad);
        }

        public async Task<bool> DeleteAsync(int beaconId, int railroadId)
        {
            return await _repository.DeleteAsync(beaconId, railroadId);
        }
    }
}
