using Microsoft.AspNetCore.SignalR;
using Web.Server.DTOs;
using Web.Server.Entities;
using Web.Server.Hubs;
using Web.Server.Repositories;

namespace Web.Server.Services
{
    public class PassengerMapPinService : IPassengerMapPinService
    {
        private readonly IPassengerMapPinRepository _repository;
        private readonly IHubContext<NotificationHub> _hubContext;

        public PassengerMapPinService(IPassengerMapPinRepository repository, IHubContext<NotificationHub> hubContext)
        {
            _repository = repository;
            _hubContext = hubContext;
        }

        public async Task<IEnumerable<PassengerMapPinDTO>> GetPassengerMapPinsAsync()
        {
            var entities = await _repository.GetAllAsync();
            return entities.Select(MapPassengerPin);
        }

        public async Task<PassengerMapPinDTO> UpsertAsync(PassengerMapPin passengerMapPin)
        {
            var entity = await _repository.UpsertAsync(passengerMapPin);
            var dto = MapPassengerPin(entity);
            await _hubContext.Clients.All.SendAsync(NotificationMethods.PassengerPinUpdate, dto);
            return dto;
        }

        public async Task DeleteByTrainIdAsync(string trainId)
        {
            if (await _repository.DeleteByTrainIdAsync(trainId))
            {
                await _hubContext.Clients.All.SendAsync(NotificationMethods.PassengerPinRemoved, new { trainId });
            }
        }

        public async Task DeleteByTrainNumberAsync(string trainNum)
        {
            if (await _repository.DeleteByTrainNumberAsync(trainNum))
            {
                await _hubContext.Clients.All.SendAsync(NotificationMethods.PassengerPinRemoved, new { trainNum });
            }
        }

        private static PassengerMapPinDTO MapPassengerPin(PassengerMapPin entity)
        {
            return new PassengerMapPinDTO
            {
                ID = entity.ID,
                Provider = entity.Provider,
                RouteName = entity.RouteName,
                TrainNum = entity.TrainNum,
                TrainId = entity.TrainId,
                Heading = entity.Heading,
                Latitude = entity.Latitude,
                Longitude = entity.Longitude,
                Velocity = entity.Velocity,
                UpdatedAt = entity.UpdatedAt,
                IsStale = entity.IsStale,
                CreatedAt = entity.CreatedAt,
                LastUpdate = entity.LastUpdate
            };
        }
    }
}