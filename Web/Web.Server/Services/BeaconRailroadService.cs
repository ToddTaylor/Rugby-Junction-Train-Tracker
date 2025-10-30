using Web.Server.Entities;
using Web.Server.Providers;
using Web.Server.Repositories;

namespace Web.Server.Services
{
    public class BeaconRailroadService : IBeaconRailroadService
    {
        private readonly IBeaconRailroadRepository _repository;
        private readonly ITimeProvider _timeProvider;

        public BeaconRailroadService(IBeaconRailroadRepository repository, ITimeProvider timeProvider)
        {
            _repository = repository;
            _timeProvider = timeProvider;
        }

        public async Task<BeaconRailroad> AddAsync(BeaconRailroad beaconRailroad)
        {
            return await _repository.AddAsync(beaconRailroad);
        }

        public async Task<IEnumerable<BeaconRailroad>> GetAllAsync()
        {
            return await _repository.GetAllAsync();
        }

        public async Task<BeaconRailroad?> GetByIdAsync(int beaconId, int subdivisionId)
        {
            return await _repository.GetByIdAsync(beaconId, subdivisionId);
        }

        public async Task<BeaconRailroad> UpdateAsync(BeaconRailroad beaconRailroad)
        {
            return await _repository.UpdateAsync(beaconRailroad);
        }

        public async Task<ICollection<BeaconRailroad>> UpdateAsync(ICollection<BeaconRailroad> beaconRailroads)
        {
            List<BeaconRailroad> updatedRailroads = new();

            foreach (var beaconRailroad in beaconRailroads)
            {
                beaconRailroad.LastUpdate = _timeProvider.UtcNow;

                BeaconRailroad updated = await UpdateAsync(beaconRailroad);

                updatedRailroads.Add(updated);
            }

            return updatedRailroads;
        }

        public async Task<bool> DeleteAsync(int beaconId, int railroadId)
        {
            return await _repository.DeleteAsync(beaconId, railroadId);
        }
    }
}
