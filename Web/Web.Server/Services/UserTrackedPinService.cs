using AutoMapper;
using Microsoft.AspNetCore.SignalR;
using Web.Server.DTOs;
using Web.Server.Entities;
using Web.Server.Hubs;
using Web.Server.Providers;
using Web.Server.Repositories;

namespace Web.Server.Services
{
    public class UserTrackedPinService : IUserTrackedPinService
    {
        private readonly IUserTrackedPinRepository _repository;
        private readonly IMapPinRepository _mapPinRepository;
        private readonly ITimeProvider _timeProvider;
        private readonly IMapper _mapper;
        private readonly ILogger<UserTrackedPinService> _logger;
        private readonly IHubContext<NotificationHub> _hubContext;

        public const int TrackingDurationHours = 12;

        public UserTrackedPinService(
            IUserTrackedPinRepository repository,
            IMapPinRepository mapPinRepository,
            ITimeProvider timeProvider,
            IMapper mapper,
            ILogger<UserTrackedPinService> logger,
            IHubContext<NotificationHub> hubContext)
        {
            _repository = repository;
            _mapPinRepository = mapPinRepository;
            _timeProvider = timeProvider;
            _mapper = mapper;
            _logger = logger;
            _hubContext = hubContext;
        }



        public async Task<IEnumerable<UserTrackedPinDTO>> GetByUserIdAsync(int userId)
        {
            var entities = await _repository.GetByUserIdAsync(userId);
            return _mapper.Map<IEnumerable<UserTrackedPinDTO>>(entities);
        }

        public async Task<UserTrackedPinDTO> AddAsync(int userId, int mapPinId, int? beaconId, int? subdivisionId, string? beaconName, string? symbol, string color)
        {
            try
            {
                var trackedPin = new UserTrackedPin
                {
                    UserId = userId,
                    MapPinId = mapPinId,
                    BeaconID = beaconId,
                    SubdivisionID = subdivisionId,
                    Symbol = symbol,
                    Color = color,
                    ExpiresUtc = _timeProvider.UtcNow.AddHours(TrackingDurationHours),
                    CreatedAt = _timeProvider.UtcNow,
                    LastUpdate = _timeProvider.UtcNow
                };

                var result = await _repository.AddAsync(trackedPin);
                _logger.LogDebug("User {UserId} tracked pin {MapPinId} with symbol {Symbol}", userId, mapPinId, symbol);
                var dto = _mapper.Map<UserTrackedPinDTO>(result);
                await SafeBroadcastAsync("TrackedPinAdded", userId, dto);
                return dto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding tracked pin for user {UserId} and pin {MapPinId}", userId, mapPinId);
                throw;
            }
        }

        public async Task UpdateSymbolAsync(int userId, int mapPinId, string? symbol)
        {
            try
            {
                var trackedPin = await _repository.GetByUserAndMapPinAsync(userId, mapPinId);
                if (trackedPin == null)
                {
                    // If the tracked pin doesn't exist, create it
                    var mapPin = await _mapPinRepository.GetByIdAsync(mapPinId);
                    if (mapPin == null)
                    {
                        throw new InvalidOperationException($"Map pin {mapPinId} not found");
                    }

                    // Get existing colors for the user
                    var existingPins = await _repository.GetByUserIdAsync(userId);
                    var usedColors = existingPins.Select(p => p.Color).ToList();
                    var availableColors = new[] { "#FF3366", "#00FFFF", "#00FF00", "#FF00FF", "#FFFF00", "#FF6600", "#00FF99", "#FF0099", "#66FF00", "#0099FF" };
                    var color = availableColors.FirstOrDefault(c => !usedColors.Contains(c)) ?? "orange";

                    trackedPin = new UserTrackedPin
                    {
                        UserId = userId,
                        MapPinId = mapPinId,
                        BeaconID = mapPin.BeaconID,
                        SubdivisionID = mapPin.SubdivisionId,
                        Symbol = symbol,
                        Color = color,
                        LastUpdate = _timeProvider.UtcNow,
                        ExpiresUtc = _timeProvider.UtcNow.AddHours(TrackingDurationHours)
                    };
                    await _repository.AddAsync(trackedPin);
                    _logger.LogDebug("Created tracked pin for user {UserId} and pin {MapPinId} with symbol {Symbol}", userId, mapPinId, symbol);
                }
                else
                {
                    trackedPin.Symbol = symbol;
                    trackedPin.LastUpdate = _timeProvider.UtcNow;
                    await _repository.UpdateAsync(trackedPin);
                    _logger.LogDebug("Updated tracked pin symbol for user {UserId} and pin {MapPinId} to {Symbol}", userId, mapPinId, symbol);
                }

                var dto = _mapper.Map<UserTrackedPinDTO>(trackedPin);
                await SafeBroadcastAsync("TrackedPinUpdated", userId, dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating tracked pin symbol for user {UserId} and pin {MapPinId}", userId, mapPinId);
                throw;
            }
        }

        public async Task UpdateLocationAsync(int userId, int mapPinId, int? beaconId, int? subdivisionId, string? beaconName)
        {
            try
            {
                var trackedPin = await _repository.GetByUserAndMapPinAsync(userId, mapPinId);
                if (trackedPin == null)
                {
                    _logger.LogWarning("Tracked pin not found for user {UserId} and pin {MapPinId} when updating location", userId, mapPinId);
                    return;
                }

                trackedPin.BeaconID = beaconId;
                trackedPin.SubdivisionID = subdivisionId;
                trackedPin.LastUpdate = _timeProvider.UtcNow;
                await _repository.UpdateAsync(trackedPin);
                _logger.LogDebug("Updated tracked pin location for user {UserId} and pin {MapPinId} to beacon {BeaconId}", userId, mapPinId, beaconId);
                var dto = _mapper.Map<UserTrackedPinDTO>(trackedPin);
                await SafeBroadcastAsync("TrackedPinUpdated", userId, dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating tracked pin location for user {UserId} and pin {MapPinId}", userId, mapPinId);
                throw;
            }
        }

        public async Task DeleteAsync(int userId, int mapPinId)
        {
            try
            {
                await _repository.DeleteByUserAndMapPinAsync(userId, mapPinId);
                _logger.LogDebug("User {UserId} untracked pin {MapPinId}", userId, mapPinId);
                await SafeBroadcastAsync("TrackedPinRemoved", userId, new { mapPinId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting tracked pin for user {UserId} and pin {MapPinId}", userId, mapPinId);
                throw;
            }
        }

        private async Task SafeBroadcastAsync(string method, int userId, object payload)
        {
            try
            {
                await _hubContext.Clients.Group(GetUserGroup(userId)).SendAsync(method, payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to broadcast {Method} for user {UserId}", method, userId);
            }
        }

        private static string GetUserGroup(int userId) => $"user-{userId}";
    }
}
