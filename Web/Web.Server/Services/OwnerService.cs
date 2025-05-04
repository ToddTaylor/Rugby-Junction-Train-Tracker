using Web.Server.Entities;

namespace Web.Server.Services
{
    public class OwnerService : IOwnerService
    {
        private readonly IOwnerRepository _ownerRepository;

        public OwnerService(IOwnerRepository ownerRepository)
        {
            _ownerRepository = ownerRepository;
        }

        public async Task<IEnumerable<Owner>> GetOwnersAsync()
        {
            return await _ownerRepository.GetAllAsync();
        }

        public async Task<Owner?> GetOwnerByIdAsync(int id)
        {
            return await _ownerRepository.GetByIdAsync(id);
        }

        public async Task<Owner> CreateOwnerAsync(Owner owner)
        {
            return await _ownerRepository.AddAsync(owner);
        }

        public async Task<Owner> UpdateOwnerAsync(int id, Owner owner)
        {
            return await _ownerRepository.UpdateAsync(owner);
        }

        public async Task<bool> DeleteOwnerAsync(int id)
        {
            return await _ownerRepository.DeleteAsync(id);
        }
    }
}

