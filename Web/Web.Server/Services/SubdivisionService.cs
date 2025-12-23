using Web.Server.Entities;
using Web.Server.Repositories;

namespace Web.Server.Services
{
    public class SubdivisionService : ISubdivisionService
    {
        private readonly ISubdivisionRepository _subdivisionRepository;

        public SubdivisionService(ISubdivisionRepository subdivisionRepository)
        {
            _subdivisionRepository = subdivisionRepository;
        }

        public async Task<Subdivision> GetSubdivisionAsync(int id)
        {
            return await _subdivisionRepository.GetByIdAsync(id);
        }

        public async Task<IEnumerable<Subdivision>> GetSubdivisionsAsync()
        {
            return await _subdivisionRepository.GetAllAsync();
        }

        public async Task<Subdivision> CreateSubdivisionAsync(Subdivision subdivision)
        {
            return await _subdivisionRepository.AddAsync(subdivision);
        }

        public async Task<Subdivision> UpdateSubdivisionAsync(Subdivision subdivision)
        {
            return await _subdivisionRepository.UpdateAsync(subdivision);
        }

        public async Task DeleteSubdivisionAsync(int id)
        {
            await _subdivisionRepository.DeleteAsync(id);
        }
    }
}

