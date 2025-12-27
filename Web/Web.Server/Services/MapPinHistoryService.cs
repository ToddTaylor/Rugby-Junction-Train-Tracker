using System.Text.Json;
using Web.Server.Entities;
using Web.Server.Providers;
using Web.Server.Repositories;

namespace Web.Server.Services
{
    public class MapPinHistoryService : IMapPinHistoryService
    {
        private const int HISTORY_TIME_THRESHOLD_MINUTES = 360; // 6 hours - telemetry at same beacon within this time is considered the same train
        
        private readonly IMapPinHistoryRepository _repository;
        private readonly IBeaconRailroadService _beaconRailroadService;
        private readonly ITimeProvider _timeProvider;

        public MapPinHistoryService(
            IMapPinHistoryRepository repository,
            IBeaconRailroadService beaconRailroadService,
            ITimeProvider timeProvider)
        {
            _repository = repository;
            _beaconRailroadService = beaconRailroadService;
            _timeProvider = timeProvider;
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
                        
                        if (timeSinceLastUpdate.TotalMinutes >= HISTORY_TIME_THRESHOLD_MINUTES)
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
                            // Less than 15 minutes - update the existing history with the latest addresses
                            // This is likely telemetry from the same train
                            existingHistory.AddressesJson = addressesJson;
                            existingHistory.Direction = mapPin.Direction;
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
