using Web.Server.DTOs;

namespace Web.Server.Services
{
    public interface IAmtrakConfigurationService
    {
        Task<IEnumerable<AmtrakTrackedTrainDTO>> GetTrackedTrainsAsync();
        Task<AmtrakTrackedTrainDTO> AddTrackedTrainAsync(string trainNumber);
        Task<bool> DeleteTrackedTrainAsync(int id);
        Task<AmtrakPollingConfigurationDTO> GetPollingConfigurationAsync();
        Task<AmtrakPollingConfigurationDTO> UpdatePollingConfigurationAsync(int pollIntervalMinutes);
    }
}