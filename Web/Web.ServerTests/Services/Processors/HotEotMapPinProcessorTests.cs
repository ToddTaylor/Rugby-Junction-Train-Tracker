using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Diagnostics.CodeAnalysis;
using Web.Server.Entities;
using Web.Server.Enums;
using Web.Server.Repositories;
using Web.Server.Services.Processors;

namespace Web.ServerTests.Services.Processors
{
    [ExcludeFromCodeCoverage]
    [TestClass]
    public class HotEotMapPinProcessorTests
    {
        private readonly Mock<IMapPinRepository> _mapPinRepositoryMock = new();
        private readonly Mock<ITelemetryRepository> _telemetryRepositoryMock = new();

        private HotEotMapPinProcessor _processor = null!;
        private DateTime _utcNow;

        [TestInitialize]
        public void Setup()
        {
            _utcNow = new DateTime(2026, 3, 27, 1, 0, 0, DateTimeKind.Utc);
            _processor = new HotEotMapPinProcessor(
                _mapPinRepositoryMock.Object,
                _telemetryRepositoryMock.Object,
                new NullLogger<HotEotMapPinProcessor>());

            _mapPinRepositoryMock
                .Setup(r => r.GetAllByAddressIdAsync(It.IsAny<int>()))
                .ReturnsAsync(new List<MapPin>());
        }

        [TestMethod]
        public async Task ProcessAsync_ReturnsNewMapPinSignal_WhenNoExistingAddressMatchExists()
        {
            var telemetry = CreateTelemetry(SourceEnum.HOT, addressId: 12345);

            var result = await _processor.ProcessAsync(telemetry);

            Assert.IsNull(result.MapPin);
            Assert.IsTrue(result.IsNewMapPin);
            Assert.IsNull(result.DiscardReason);
        }

        [TestMethod]
        public async Task ProcessAsync_ReturnsExistingMapPin_WithoutAddingDuplicateAddress_WhenSourceAlreadyPresent()
        {
            var existingMapPin = CreateMapPin([
                CreateAddress(12345, null, SourceEnum.HOT, _utcNow.AddMinutes(-5))
            ]);
            var telemetry = CreateTelemetry(SourceEnum.HOT, addressId: 12345);

            _mapPinRepositoryMock
                .Setup(r => r.GetAllByAddressIdAsync(12345))
                .ReturnsAsync([existingMapPin]);

            var result = await _processor.ProcessAsync(telemetry);

            Assert.AreSame(existingMapPin, result.MapPin);
            Assert.IsFalse(result.IsNewMapPin);
            Assert.IsNull(result.DiscardReason);
            Assert.AreEqual(1, existingMapPin.Addresses.Count);
        }

        [TestMethod]
        public async Task ProcessAsync_RefreshesLastUpdate_WhenSourceAlreadyPresent()
        {
            var staleTimestamp = _utcNow.AddMinutes(-5);
            var existingMapPin = CreateMapPin([
                CreateAddress(12345, null, SourceEnum.HOT, staleTimestamp)
            ]);
            var telemetry = CreateTelemetry(SourceEnum.HOT, addressId: 12345);

            _mapPinRepositoryMock
                .Setup(r => r.GetAllByAddressIdAsync(12345))
                .ReturnsAsync([existingMapPin]);

            await _processor.ProcessAsync(telemetry);

            var existingAddress = existingMapPin.Addresses.Single();
            Assert.AreEqual(staleTimestamp, existingAddress.CreatedAt);
            Assert.AreEqual(_utcNow, existingAddress.LastUpdate);
        }

        [TestMethod]
        public async Task ProcessAsync_AddsAddress_WhenExistingMapPinHasDifferentSource()
        {
            var existingMapPin = CreateMapPin([
                CreateAddress(12345, null, SourceEnum.HOT, _utcNow.AddMinutes(-5))
            ]);
            var telemetry = CreateTelemetry(SourceEnum.EOT, addressId: 12345);

            _mapPinRepositoryMock
                .Setup(r => r.GetAllByAddressIdAsync(12345))
                .ReturnsAsync([existingMapPin]);

            var result = await _processor.ProcessAsync(telemetry);

            Assert.AreSame(existingMapPin, result.MapPin);
            Assert.IsFalse(result.IsNewMapPin);
            Assert.IsNull(result.DiscardReason);
            Assert.AreEqual(2, existingMapPin.Addresses.Count);

            var addedAddress = existingMapPin.Addresses.Single(a => a.Source == SourceEnum.EOT);
            Assert.AreEqual(12345, addedAddress.AddressID);
            Assert.IsNull(addedAddress.DpuTrainID);
            Assert.AreEqual(_utcNow, addedAddress.CreatedAt);
            Assert.AreEqual(_utcNow, addedAddress.LastUpdate);
        }

        [TestMethod]
        public async Task ProcessAsync_ChoosesReachableMapPin_OverNewerUnreachableOne()
        {
            // Fond du Lac pin updated a minute ago; the southbound pin at Allenton ten minutes ago.
            var fondDuLacPin = CreatePinAt(FondDuLac(), _utcNow.AddMinutes(-1), id: 1);
            var southboundPin = CreatePinAt(Allenton(), _utcNow.AddMinutes(-10), id: 2);
            var telemetry = CreateTelemetryAt(RugbyJunction(), SourceEnum.HOT);

            _mapPinRepositoryMock
                .Setup(r => r.GetAllByAddressIdAsync(20679))
                .ReturnsAsync([fondDuLacPin, southboundPin]);

            var result = await _processor.ProcessAsync(telemetry);

            Assert.AreSame(southboundPin, result.MapPin);
            Assert.IsFalse(result.IsNewMapPin);
            Assert.IsNull(result.DiscardReason);
        }

        [TestMethod]
        public async Task ProcessAsync_PrefersNewestReachableMapPin_OverStalePinAtSameBeacon()
        {
            var stalePinAtRugby = CreatePinAt(RugbyJunction(), _utcNow.AddDays(-1), id: 1);
            var arrivingPin = CreatePinAt(Allenton(), _utcNow.AddMinutes(-10), id: 2);
            var telemetry = CreateTelemetryAt(RugbyJunction(), SourceEnum.HOT);

            _mapPinRepositoryMock
                .Setup(r => r.GetAllByAddressIdAsync(20679))
                .ReturnsAsync([arrivingPin, stalePinAtRugby]);

            var result = await _processor.ProcessAsync(telemetry);

            Assert.AreSame(arrivingPin, result.MapPin);
        }

        [TestMethod]
        public async Task ProcessAsync_DiscardsAsPendingSecondTrain_WhenNoMapPinIsReachable()
        {
            var fondDuLacPin = CreatePinAt(FondDuLac(), _utcNow.AddMinutes(-1), id: 1);
            var telemetry = CreateTelemetryAt(Allenton(), SourceEnum.EOT);

            _mapPinRepositoryMock
                .Setup(r => r.GetAllByAddressIdAsync(20679))
                .ReturnsAsync([fondDuLacPin]);

            var result = await _processor.ProcessAsync(telemetry);

            Assert.AreEqual(HotEotMapPinProcessor.PENDING_SECOND_TRAIN_REASON, result.DiscardReason);
            Assert.IsNull(result.MapPin);
            Assert.IsNull(result.SeedTelemetry);
            Assert.AreEqual(1, fondDuLacPin.Addresses.Count, "The unreachable pin must not take the address.");
        }

        [TestMethod]
        public async Task ProcessAsync_ConfirmsSecondTrain_WhenPendingReadingIsConsistent()
        {
            var fondDuLacPin = CreatePinAt(FondDuLac(), _utcNow.AddMinutes(-1), id: 1);
            var pending = CreateTelemetryAt(Allenton(), SourceEnum.EOT, _utcNow.AddMinutes(-1), id: 501);
            var telemetry = CreateTelemetryAt(Allenton(), SourceEnum.EOT, id: 502);

            _mapPinRepositoryMock
                .Setup(r => r.GetAllByAddressIdAsync(20679))
                .ReturnsAsync([fondDuLacPin]);
            _telemetryRepositoryMock
                .Setup(r => r.GetRecentsDiscardedForAddressAsync(20679, HotEotMapPinProcessor.PENDING_SECOND_TRAIN_REASON, _utcNow.AddMinutes(-30)))
                .ReturnsAsync([pending]);

            var result = await _processor.ProcessAsync(telemetry);

            Assert.IsTrue(result.IsNewMapPin);
            Assert.IsNull(result.DiscardReason);
            Assert.AreSame(pending, result.SeedTelemetry);
        }

        [TestMethod]
        public async Task ProcessAsync_StaysPending_WhenPendingReadingIsInconsistent()
        {
            var fondDuLacPin = CreatePinAt(FondDuLac(), _utcNow.AddMinutes(-1), id: 1);
            var pendingFarAway = CreateTelemetryAt(FarNorth(), SourceEnum.EOT, _utcNow.AddMinutes(-1), id: 501);
            var telemetry = CreateTelemetryAt(Allenton(), SourceEnum.EOT, id: 502);

            _mapPinRepositoryMock
                .Setup(r => r.GetAllByAddressIdAsync(20679))
                .ReturnsAsync([fondDuLacPin]);
            _telemetryRepositoryMock
                .Setup(r => r.GetRecentsDiscardedForAddressAsync(20679, HotEotMapPinProcessor.PENDING_SECOND_TRAIN_REASON, It.IsAny<DateTime>()))
                .ReturnsAsync([pendingFarAway]);

            var result = await _processor.ProcessAsync(telemetry);

            Assert.AreEqual(HotEotMapPinProcessor.PENDING_SECOND_TRAIN_REASON, result.DiscardReason);
            Assert.IsNull(result.SeedTelemetry);
        }

        [TestMethod]
        public void SupportedSources_ContainsHotAndEot()
        {
            CollectionAssert.AreEqual(new[] { SourceEnum.HOT, SourceEnum.EOT }, _processor.SupportedSources);
        }

        private Telemetry CreateTelemetry(string source, int addressId)
        {
            return new Telemetry
            {
                BeaconID = 10,
                Beacon = new Beacon
                {
                    ID = 10,
                    Name = "Beacon-10"
                },
                AddressID = addressId,
                Source = source,
                CreatedAt = _utcNow,
                LastUpdate = _utcNow
            };
        }

        // One subdivision with mileposts roughly matching the real distances, north to south.
        private static Subdivision Subdivision() => new() { ID = 101, Name = "Test", RailroadID = 1 };

        private static BeaconRailroad FarNorth() => BeaconRailroadAt(23, 400.0, 46.5);
        private static BeaconRailroad FondDuLac() => BeaconRailroadAt(20, 150.0, 43.77);
        private static BeaconRailroad Allenton() => BeaconRailroadAt(21, 110.0, 43.42);
        private static BeaconRailroad RugbyJunction() => BeaconRailroadAt(22, 100.0, 43.28);

        private static BeaconRailroad BeaconRailroadAt(int beaconId, double milepost, double latitude) => new()
        {
            BeaconID = beaconId,
            SubdivisionID = 101,
            Subdivision = Subdivision(),
            Milepost = milepost,
            Latitude = latitude,
            Longitude = -88.3
        };

        private Telemetry CreateTelemetryAt(BeaconRailroad beaconRailroad, string source, DateTime? createdAt = null, int id = 0)
        {
            return new Telemetry
            {
                ID = id,
                BeaconID = beaconRailroad.BeaconID,
                Beacon = new Beacon
                {
                    ID = beaconRailroad.BeaconID,
                    BeaconRailroads = [beaconRailroad]
                },
                AddressID = 20679,
                Source = source,
                CreatedAt = createdAt ?? _utcNow,
                LastUpdate = createdAt ?? _utcNow
            };
        }

        private static MapPin CreatePinAt(BeaconRailroad beaconRailroad, DateTime lastUpdate, int id)
        {
            return new MapPin
            {
                ID = id,
                BeaconID = beaconRailroad.BeaconID,
                SubdivisionId = beaconRailroad.SubdivisionID,
                BeaconRailroad = beaconRailroad,
                LastUpdate = lastUpdate,
                Addresses = [CreateAddress(20679, null, SourceEnum.HOT, lastUpdate)]
            };
        }

        private static MapPin CreateMapPin(IEnumerable<Address> addresses)
        {
            return new MapPin
            {
                ID = 1,
                BeaconID = 10,
                SubdivisionId = 101,
                Addresses = addresses.ToList()
            };
        }

        private static Address CreateAddress(int addressId, int? trainId, string source, DateTime timestamp)
        {
            return new Address
            {
                AddressID = addressId,
                DpuTrainID = trainId,
                Source = source,
                CreatedAt = timestamp,
                LastUpdate = timestamp
            };
        }
    }
}