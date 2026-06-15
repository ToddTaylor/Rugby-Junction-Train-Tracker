using Web.Server.Entities;
using Web.Server.Repositories;

namespace Web.Server.Services
{
    public class SubdivisionService : ISubdivisionService
    {
        private readonly ISubdivisionRepository _subdivisionRepository;
        private readonly IUserService _userService;

        public SubdivisionService(ISubdivisionRepository subdivisionRepository, IUserService userService)
        {
            _subdivisionRepository = subdivisionRepository;
            _userService = userService;
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
            if (subdivision.CustodianId.HasValue)
            {
                await ValidateCustodianAssignmentAsync(subdivision.CustodianId.Value);
            }

            return await _subdivisionRepository.AddAsync(subdivision);
        }

        public async Task<Subdivision> UpdateSubdivisionAsync(Subdivision subdivision)
        {
            var existingSubdivision = await _subdivisionRepository.GetByIdAsync(subdivision.ID);
            if (existingSubdivision == null)
            {
                throw new KeyNotFoundException("Subdivision not found.");
            }

            var custodianChanged = existingSubdivision.CustodianId != subdivision.CustodianId;
            if (custodianChanged && subdivision.CustodianId.HasValue)
            {
                await ValidateCustodianAssignmentAsync(subdivision.CustodianId.Value);
            }

            return await _subdivisionRepository.UpdateAsync(subdivision);
        }

        public async Task DeleteSubdivisionAsync(int id)
        {
            await _subdivisionRepository.DeleteAsync(id);
        }

        private async Task ValidateCustodianAssignmentAsync(int custodianId)
        {
            var user = await _userService.GetUserByIdAsync(custodianId);
            if (user == null)
            {
                throw new ArgumentException("Assigned custodian user was not found.");
            }

            if (!user.IsActive)
            {
                throw new ArgumentException("Assigned custodian user must be active.");
            }

            var hasCustodianRole = user.UserRoles?.Any(ur =>
                string.Equals(ur.Role?.RoleName, "Custodian", StringComparison.OrdinalIgnoreCase)) == true;

            if (!hasCustodianRole)
            {
                throw new ArgumentException("Assigned user must have the Custodian role.");
            }
        }
    }
}

