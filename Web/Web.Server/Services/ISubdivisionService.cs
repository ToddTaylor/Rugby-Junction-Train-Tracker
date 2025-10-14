using Web.Server.Entities;

namespace Web.Server.Services
{
    public interface ISubdivisionService
    {
        Task<Subdivision> CreateSubdivisionAsync(Subdivision subdivision);
        Task DeleteSubdivisionAsync(int ID);
        Task<Subdivision> GetSubdivisionAsync(int ID);
        Task<IEnumerable<Subdivision>> GetSubdivisionsAsync();
        Task UpdateSubdivisionAsync(Subdivision subdivision);
    }
}