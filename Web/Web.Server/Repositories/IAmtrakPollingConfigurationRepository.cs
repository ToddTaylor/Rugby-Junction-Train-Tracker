using Web.Server.Entities;

namespace Web.Server.Repositories
{
    public interface IAmtrakPollingConfigurationRepository
    {
        Task<AmtrakPollingConfiguration?> GetAsync();
        Task<AmtrakPollingConfiguration> UpsertAsync(int pollIntervalMinutes);
    }
}