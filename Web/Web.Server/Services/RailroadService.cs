using Web.Server.Data;
using Web.Server.Entities;

namespace Web.Server.Services
{
    public class RailroadService : IRailroadService
    {
        private TelemetryDbContext _dbContext;
        private static readonly Random _random = new Random();

        public RailroadService(
            TelemetryDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public Task<Railroad> GetRailroad(int ID)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<Railroad>> GetRailroads()
        {
            throw new NotImplementedException();
        }

        public Task<Railroad> CreateRailroad(Railroad railroad)
        {
            throw new NotImplementedException();
        }

        public async void UpdateRailroad(Railroad railroad)
        {
            throw new NotImplementedException();
        }

        public async void DeleteRailroad(int ID)
        {
            throw new NotImplementedException();
        }
    }
}
