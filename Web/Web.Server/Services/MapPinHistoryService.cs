using System.Text.Json;
using Web.Server.Entities;
using Web.Server.Providers;
using Web.Server.Repositories;

namespace Web.Server.Services
{
    public class MapPinHistoryService : IMapPinHistoryService
    {
        private readonly int _historyTimeThresholdMinutes;
        private readonly int _stationaryDirectionNullThresholdHours;

        private readonly IMapPinHistoryRepository _repository;
        private readonly IBeaconRailroadService _beaconRailroadService;
        private readonly ITimeProvider _timeProvider;

        public MapPinHistoryService(
            IMapPinHistoryRepository repository,
            IBeaconRailroadService beaconRailroadService,
            ITimeProvider timeProvider,
            IConfiguration configuration)
        {
            _repository = repository;
            _beaconRailroadService = beaconRailroadService;
            _timeProvider = timeProvider;
            _historyTimeThresholdMinutes = configuration.GetValue<int>("ApplicationSettings:HistoryTimeThresholdMinutes", 360);
            _stationaryDirectionNullThresholdHours = configuration.GetValue<int>("ApplicationSettings:StationaryDirectionNullThresholdHours", 6);
        }

        public async Task<MapPinHistory?> GetHistoryByOriginalMapPinIdAsync(int mapPinId)
        {
            return await _repository.GetByOriginalMapPinIdAsync(mapPinId);
        }

        public async Task<IEnumerable<MapPinHistory>> GetHistoryByBeaconIdAsync(int beaconId, int? limit = null)
        {
            var histories = await _repository.GetByBeaconIdAsync(beaconId, limit);

            // Populate BeaconRailroad data for each history record
            foreach (var history in histories)
            {
                history.BeaconRailroad = await _beaconRailroadService.GetByIdAsync(history.BeaconID, history.SubdivisionId);
            }

            return histories;
        }

        public async Task CreateOrUpdateHistoryFromMapPin(MapPin mapPin, bool isNewMapPin)
        {
            // Serialize addresses to JSON
            var addressesJson = JsonSerializer.Serialize(mapPin.Addresses.Select(a => new
            {
                a.AddressID,
                a.Source,
                a.DpuTrainID,
                a.CreatedAt,
                a.LastUpdate
            }));

            if (isNewMapPin)
            {
                // Create new history record
                var history = new MapPinHistory
                {
                    BeaconID = mapPin.BeaconID,
                    SubdivisionId = mapPin.SubdivisionId,
                    Direction = mapPin.Direction,
                    Moving = mapPin.Moving,
                    IsLocal = mapPin.IsLocal,
                    AddressesJson = addressesJson,
                    OriginalMapPinID = mapPin.ID
                };

                await _repository.AddAsync(history);
            }
            else
            {
                // Find history record by the original map pin ID
                // This ensures we only update history for the SAME logical map pin,
                // respecting all the grouping rules (single-track, DPU-capable, etc.)
                var existingHistory = await _repository.GetByOriginalMapPinIdAsync(mapPin.ID);

                if (existingHistory != null)
                {
                    // Check if the beacon has changed (train moved to a new location)
                    if (existingHistory.BeaconID != mapPin.BeaconID)
                    {
                        // Beacon changed - create NEW history record for the new location
                        var newHistory = new MapPinHistory
                        {
                            BeaconID = mapPin.BeaconID,
                            SubdivisionId = mapPin.SubdivisionId,
                            Direction = mapPin.Direction,
                            Moving = mapPin.Moving,
                            IsLocal = mapPin.IsLocal,
                            AddressesJson = addressesJson,
                            OriginalMapPinID = mapPin.ID
                        };

                        await _repository.AddAsync(newHistory);
                    }
                    else
                    {
                        // Same beacon - check if enough time has passed to warrant a new history entry
                        var timeSinceLastUpdate = _timeProvider.UtcNow - existingHistory.LastUpdate;

                        if (timeSinceLastUpdate.TotalMinutes >= _historyTimeThresholdMinutes)
                        {
                            // Enough time has passed (15+ minutes) - create NEW history record
                            // This represents a new train passage through the same beacon
                            var newHistory = new MapPinHistory
                            {
                                BeaconID = mapPin.BeaconID,
                                SubdivisionId = mapPin.SubdivisionId,
                                Direction = mapPin.Direction,
                                Moving = mapPin.Moving,
                                IsLocal = mapPin.IsLocal,
                                AddressesJson = addressesJson,
                                OriginalMapPinID = mapPin.ID
                            };

                            await _repository.AddAsync(newHistory);
                        }
                        else
                        {
                            // Less than threshold - update the existing history with the latest addresses
                            // This is likely telemetry from the same train
                            existingHistory.AddressesJson = addressesJson;

                            // Check if the map pin has been stationary for threshold time at same beacon
                            var timeSinceCreated = _timeProvider.UtcNow - existingHistory.CreatedAt;
                            if (timeSinceCreated.TotalHours >= _stationaryDirectionNullThresholdHours)
                            {
                                // Map pin has been at the same beacon for threshold time - set direction to null
                                existingHistory.Direction = null;
                            }
                            else
                            {
                                existingHistory.Direction = mapPin.Direction;
                            }
                            existingHistory.Moving = mapPin.Moving;
                            existingHistory.IsLocal = mapPin.IsLocal;

                            await _repository.UpdateAsync(existingHistory);
                        }
                    }
                }
                else
                {
                    // No existing history found - create initial history record for this existing MapPin
                    // This handles MapPins that existed before history tracking was implemented
                    var newHistory = new MapPinHistory
                    {
                        BeaconID = mapPin.BeaconID,
                        SubdivisionId = mapPin.SubdivisionId,
                        Direction = mapPin.Direction,
                        Moving = mapPin.Moving,
                        IsLocal = mapPin.IsLocal,
                        AddressesJson = addressesJson,
                        OriginalMapPinID = mapPin.ID
                    };

                    await _repository.AddAsync(newHistory);
                }
            }
        }
    }
}
