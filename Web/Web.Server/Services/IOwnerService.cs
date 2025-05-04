namespace Web.Server.Services
{
    public interface IOwnerService
    {
        Task<IEnumerable<Owner>> GetOwnersAsync();
        Task<Owner?> GetOwnerByIdAsync(int id);
        Task<Owner> CreateOwnerAsync(Owner owner);
        Task<Owner> UpdateOwnerAsync(int id, Owner owner);
        Task<bool> DeleteOwnerAsync(int id);
    }
}
