using Web.Server.Entities;
using Web.Server.Enums;
using Web.Server.Repositories;

namespace Web.Server.Services.Processors
{
    /// <summary>
    /// Processes HOT (head-of-train) and EOT (end-of-train) telemetry: finds existing map pin by address,
    /// adds address if missing, and returns a candidate map pin.
    /// Delegates create/update/discard validation to MapPinService.
    /// </summary>
    public class HotEotMapPinProcessor : IMapPinProcessor
    {
        public string[] SupportedSources => new[] { SourceEnum.HOT, SourceEnum.EOT };

        private readonly IMapPinRepository _mapPinRepository;
        private readonly ILogger<HotEotMapPinProcessor> _logger;

        public HotEotMapPinProcessor(
            IMapPinRepository mapPinRepository,
            ILogger<HotEotMapPinProcessor> logger)
        {
            _mapPinRepository = mapPinRepository;
            _logger = logger;
        }

        public async Task<MapPinProcessingResult> ProcessAsync(Telemetry telemetry)
        {
            var existingMapPin = await GetExistingHotOrEotMapPinAsync(telemetry);

            if (existingMapPin == null)
            {
                // Return null MapPin to signal MapPinService to create new
                return new MapPinProcessingResult
                {
                    MapPin = null,
                    IsNewMapPin = true,
                    DiscardReason = null
                };
            }

            // Existing map pin found by address
            AddHotOrEotAddressIfMissing(existingMapPin, telemetry);

            // Return existing map pin; MapPinService will update it
            return new MapPinProcessingResult
            {
                MapPin = existingMapPin,
                IsNewMapPin = false,
                DiscardReason = null
            };
        }

        private async Task<MapPin?> GetExistingHotOrEotMapPinAsync(Telemetry telemetry)
        {
            // Address match is only for HOT/EOT addresses (DpuTrainID == null).
            return await _mapPinRepository.GetByAddressIdAsync(telemetry.AddressID);
        }

        private void AddHotOrEotAddressIfMissing(MapPin existingMapPinToUpdate, Telemetry telemetry)
        {
            var matchingAddress = existingMapPinToUpdate.Addresses.FirstOrDefault(a =>
                a.AddressID == telemetry.AddressID &&
                a.DpuTrainID == null &&
                a.Source == telemetry.Source);

            if (matchingAddress != null)
            {
                matchingAddress.LastUpdate = telemetry.LastUpdate;
                return;
            }

            existingMapPinToUpdate.Addresses.Add(new Address
            {
                AddressID = telemetry.AddressID,
                DpuTrainID = null,  // HOT/EOT sources have no train ID tracking
                Source = telemetry.Source,
                CreatedAt = telemetry.LastUpdate,
                LastUpdate = telemetry.LastUpdate
            });
        }
    }
}
