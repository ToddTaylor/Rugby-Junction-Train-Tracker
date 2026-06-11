using Microsoft.AspNetCore.SignalR;
using Web.Server.DTOs;
using Web.Server.Entities;
using Web.Server.Hubs;
using Web.Server.Repositories;

namespace Web.Server.Services
{
    public class AmtrakConfigurationService : IAmtrakConfigurationService
    {
        private readonly IAmtrakTrackedTrainRepository _trackedTrainRepository;
        private readonly IAmtrakPollingConfigurationRepository _pollingConfigurationRepository;
        private readonly IPassengerMapPinRepository _passengerMapPinRepository;
        private readonly IHubContext<NotificationHub> _hubContext;

        public AmtrakConfigurationService(
            IAmtrakTrackedTrainRepository trackedTrainRepository,
            IAmtrakPollingConfigurationRepository pollingConfigurationRepository,
            IPassengerMapPinRepository passengerMapPinRepository,
            IHubContext<NotificationHub> hubContext)
        {
            _trackedTrainRepository = trackedTrainRepository;
            _pollingConfigurationRepository = pollingConfigurationRepository;
            _passengerMapPinRepository = passengerMapPinRepository;
            _hubContext = hubContext;
        }

        public async Task<IEnumerable<AmtrakTrackedTrainDTO>> GetTrackedTrainsAsync()
        {
            var entities = await _trackedTrainRepository.GetAllAsync();
            return entities.Select(MapTrackedTrain);
        }

        public async Task<AmtrakTrackedTrainDTO> AddTrackedTrainAsync(string trainNumber)
        {
            var normalized = NormalizeTrainNumber(trainNumber);
            var existing = await _trackedTrainRepository.GetByTrainNumberAsync(normalized);
            if (existing != null)
            {
                throw new InvalidOperationException($"Train number {normalized} is already configured.");
            }

            var currentCount = (await _trackedTrainRepository.GetAllAsync()).Count();
            if (currentCount >= 25)
            {
                throw new InvalidOperationException("A maximum of 25 tracked Amtrak trains is allowed.");
            }

            var entity = await _trackedTrainRepository.AddAsync(new AmtrakTrackedTrain
            {
                TrainNumber = normalized,
                IsActive = true
            });

            return MapTrackedTrain(entity);
        }

        public async Task<bool> DeleteTrackedTrainAsync(int id)
        {
            var existing = await _trackedTrainRepository.GetByIdAsync(id);
            if (existing == null)
            {
                return false;
            }

            await _trackedTrainRepository.DeleteAsync(id);

            if (await _passengerMapPinRepository.DeleteByTrainNumberAsync(existing.TrainNumber))
            {
                await _hubContext.Clients.All.SendAsync(NotificationMethods.PassengerPinRemoved, new { trainNum = existing.TrainNumber });
            }

            return true;
        }

        public async Task<AmtrakPollingConfigurationDTO> GetPollingConfigurationAsync()
        {
            var entity = await _pollingConfigurationRepository.GetAsync();
            if (entity == null)
            {
                entity = await _pollingConfigurationRepository.UpsertAsync(2);
            }

            return MapPollingConfiguration(entity);
        }

        public async Task<AmtrakPollingConfigurationDTO> UpdatePollingConfigurationAsync(int pollIntervalMinutes)
        {
            if (pollIntervalMinutes < 1 || pollIntervalMinutes > 30)
            {
                throw new ArgumentOutOfRangeException(nameof(pollIntervalMinutes), "Poll interval must be between 1 and 30 minutes.");
            }

            var entity = await _pollingConfigurationRepository.UpsertAsync(pollIntervalMinutes);
            return MapPollingConfiguration(entity);
        }

        private static string NormalizeTrainNumber(string trainNumber)
        {
            if (string.IsNullOrWhiteSpace(trainNumber))
            {
                throw new ArgumentException("Train number is required.", nameof(trainNumber));
            }

            return trainNumber.Trim();
        }

        private static AmtrakTrackedTrainDTO MapTrackedTrain(AmtrakTrackedTrain entity)
        {
            return new AmtrakTrackedTrainDTO
            {
                ID = entity.ID,
                TrainNumber = entity.TrainNumber,
                IsActive = entity.IsActive,
                CreatedAt = entity.CreatedAt,
                LastUpdate = entity.LastUpdate
            };
        }

        private static AmtrakPollingConfigurationDTO MapPollingConfiguration(AmtrakPollingConfiguration entity)
        {
            return new AmtrakPollingConfigurationDTO
            {
                ID = entity.ID,
                PollIntervalMinutes = entity.PollIntervalMinutes,
                CreatedAt = entity.CreatedAt,
                LastUpdate = entity.LastUpdate
            };
        }
    }
}