using Web.Server.Entities;
using Web.Server.Providers;
using Web.Server.Repositories;

namespace Web.Server.Services
{
    public class BeaconService : IBeaconService
    {
        private readonly IBeaconRepository _beaconRepository;
        private readonly ITimeProvider _timeProvider;

        public BeaconService(IBeaconRepository beaconRepository,
            ITimeProvider timeProvider)
        {
            _beaconRepository = beaconRepository;
            _timeProvider = timeProvider;
        }

        public async Task<IEnumerable<Beacon>> GetBeaconsAsync()
        {
            return await _beaconRepository.GetAllAsync();
        }

        public async Task<Beacon?> GetBeaconByIdAsync(int id)
        {
            return await _beaconRepository.GetByIdAsync(id);
        }

        public async Task<Beacon> CreateBeaconAsync(Beacon beacon)
        {
            return await _beaconRepository.AddAsync(beacon);
        }

        public async Task<Beacon> UpdateBeaconAsync(int id, Beacon beacon)
        {
            return await _beaconRepository.UpdateAsync(beacon);
        }

        public async Task<Beacon> UpdateBeaconHealthAsync(int id, Beacon beacon)
        {
            // Update the timestamp on all beacon railroads the physical beacon is responbile for.
            foreach (var beaconRailroad in beacon.BeaconRailroads)
            {
                beaconRailroad.LastUpdate = _timeProvider.UtcNow;
            }

            return await _beaconRepository.UpdateAsync(beacon);
        }

        public async Task<bool> DeleteBeaconAsync(int id)
        {
            return await _beaconRepository.DeleteAsync(id);
        }
    }
}

