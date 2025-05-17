using Web.Server.Entities;
using Web.Server.Repositories;

namespace Web.Server.Services
{
    public class MapPinService : IMapPinsService
    {
        private readonly IMapPinRepository _mapPinRepository;

        public MapPinService(IMapPinRepository mapPinRepository)
        {
            _mapPinRepository = mapPinRepository;
        }
        public async Task<MapPin> UpsertMapPin(MapPin mapPin)
        {
            return await _mapPinRepository.UpsertAsync(mapPin);
        }

        public async Task<IEnumerable<MapPin>> GetMapPinsAsync()
        {
            return await _mapPinRepository.GetAllAsync();
        }
    }
}
