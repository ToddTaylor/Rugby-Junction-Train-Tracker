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
    public class DpuMapPinProcessorTests
    {
        private readonly Mock<IMapPinRepository> _mapPinRepositoryMock = new();

        private DpuMapPinProcessor _processor = null!;
        private DateTime _utcNow;

        [TestInitialize]
        public void Setup()
        {
            _utcNow = new DateTime(2026, 3, 27, 1, 0, 0, DateTimeKind.Utc);
            _processor = new DpuMapPinProcessor(
                _mapPinRepositoryMock.Object,
                new NullLogger<DpuMapPinProcessor>());

            _mapPinRepositoryMock
                .Setup(r => r.GetByAddressAndTrainIdAsync(It.IsAny<int>(), It.IsAny<int>(), DpuMapPinProcessor.TIME_THRESHOLD_DPU_EXACT_MINUTES))
                .ReturnsAsync((MapPin?)null);

            _mapPinRepositoryMock
                .Setup(r => r.GetByTrainIdAsync(It.IsAny<int>(), DpuMapPinProcessor.TIME_THRESHOLD_DPU_TRAIN_ONLY_MINUTES))
                .ReturnsAsync((MapPin?)null);
        }

        [TestMethod]
        public async Task ProcessAsync_ReturnsDiscard_WhenTrainIdMissing()
        {
            var telemetry = CreateTelemetry(source: SourceEnum.DPU, trainId: null, addressId: 12345, beaconId: 10, railroadId: 1, milepost: 10);

            var result = await _processor.ProcessAsync(telemetry);

            Assert.IsNull(result.MapPin);
            Assert.IsFalse(result.IsNewMapPin);
            Assert.AreEqual("DPU Missing TrainID", result.DiscardReason);
        }

        [TestMethod]
        public async Task ProcessAsync_ReturnsMatchedPin_WhenExactAddressAndTrainMatchExists()
        {
            var existingMapPin = CreateMapPin(beaconId: 10, subdivisionId: 101, railroadId: 1, milepost: 10,
                addresses: [CreateAddress(12345, 777, SourceEnum.DPU, _utcNow.AddMinutes(-5))]);
            var telemetry = CreateTelemetry(source: SourceEnum.DPU, trainId: 777, addressId: 12345, beaconId: 10, railroadId: 1, milepost: 10);

            _mapPinRepositoryMock
                .Setup(r => r.GetByAddressAndTrainIdAsync(12345, 777, DpuMapPinProcessor.TIME_THRESHOLD_DPU_EXACT_MINUTES))
                .ReturnsAsync(existingMapPin);

            var result = await _processor.ProcessAsync(telemetry);

            Assert.AreSame(existingMapPin, result.MapPin);
            Assert.IsFalse(result.IsNewMapPin);
            Assert.IsNull(result.DiscardReason);
            Assert.AreEqual(1, existingMapPin.Addresses.Count);
        }

        [TestMethod]
        public async Task ProcessAsync_RefreshesLastUpdate_WhenExactAddressAndTrainMatchExists()
        {
            var staleTimestamp = _utcNow.AddMinutes(-5);
            var existingMapPin = CreateMapPin(beaconId: 10, subdivisionId: 101, railroadId: 1, milepost: 10,
                addresses: [CreateAddress(12345, 777, SourceEnum.DPU, staleTimestamp)]);
            var telemetry = CreateTelemetry(source: SourceEnum.DPU, trainId: 777, addressId: 12345, beaconId: 10, railroadId: 1, milepost: 10);

            _mapPinRepositoryMock
                .Setup(r => r.GetByAddressAndTrainIdAsync(12345, 777, DpuMapPinProcessor.TIME_THRESHOLD_DPU_EXACT_MINUTES))
                .ReturnsAsync(existingMapPin);

            await _processor.ProcessAsync(telemetry);

            var existingAddress = existingMapPin.Addresses.Single();
            Assert.AreEqual(staleTimestamp, existingAddress.CreatedAt);
            Assert.AreEqual(_utcNow, existingAddress.LastUpdate);
        }

        [TestMethod]
        public async Task ProcessAsync_ReturnsNewMapPinSignal_WhenTrainOnlyMatchIsStaleAtSameBeacon()
        {
            var existingMapPin = CreateMapPin(beaconId: 10, subdivisionId: 101, railroadId: 1, milepost: 10,
                lastUpdate: _utcNow.AddMinutes(-31),
                addresses: [CreateAddress(99999, 777, SourceEnum.DPU, _utcNow.AddMinutes(-31))]);
            var telemetry = CreateTelemetry(source: SourceEnum.DPU, trainId: 777, addressId: 12345, beaconId: 10, railroadId: 1, milepost: 10);

            _mapPinRepositoryMock
                .Setup(r => r.GetByTrainIdAsync(777, DpuMapPinProcessor.TIME_THRESHOLD_DPU_TRAIN_ONLY_MINUTES))
                .ReturnsAsync(existingMapPin);

            var result = await _processor.ProcessAsync(telemetry);

            Assert.IsNull(result.MapPin);
            Assert.IsTrue(result.IsNewMapPin);
            Assert.IsNull(result.DiscardReason);
        }

        [TestMethod]
        public async Task ProcessAsync_ReturnsDiscard_WhenTrainOnlyMatchIsOnDifferentRailroad()
        {
            var existingMapPin = CreateMapPin(beaconId: 99, subdivisionId: 101, railroadId: 1, milepost: 15,
                addresses: [CreateAddress(99999, 777, SourceEnum.DPU, _utcNow.AddMinutes(-5))]);
            var telemetry = CreateTelemetry(source: SourceEnum.DPU, trainId: 777, addressId: 12345, beaconId: 10, railroadId: 2, milepost: 40);

            _mapPinRepositoryMock
                .Setup(r => r.GetByTrainIdAsync(777, DpuMapPinProcessor.TIME_THRESHOLD_DPU_TRAIN_ONLY_MINUTES))
                .ReturnsAsync(existingMapPin);

            var result = await _processor.ProcessAsync(telemetry);

            Assert.IsNull(result.MapPin);
            Assert.IsFalse(result.IsNewMapPin);
            Assert.AreEqual("DPU Invalid Railroad", result.DiscardReason);
        }

        [TestMethod]
        public async Task ProcessAsync_ReturnsNewMapPinSignal_WhenTrainOnlyMatchRequiresImpossibleSpeed()
        {
            var existingMapPin = CreateMapPin(beaconId: 99, subdivisionId: 101, railroadId: 1, milepost: 10,
                lastUpdate: _utcNow.AddHours(-1),
                addresses: [CreateAddress(99999, 777, SourceEnum.DPU, _utcNow.AddHours(-1))]);
            var telemetry = CreateTelemetry(source: SourceEnum.DPU, trainId: 777, addressId: 12345, beaconId: 10, railroadId: 1, milepost: 90);

            _mapPinRepositoryMock
                .Setup(r => r.GetByTrainIdAsync(777, DpuMapPinProcessor.TIME_THRESHOLD_DPU_TRAIN_ONLY_MINUTES))
                .ReturnsAsync(existingMapPin);

            var result = await _processor.ProcessAsync(telemetry);

            Assert.IsNull(result.MapPin);
            Assert.IsTrue(result.IsNewMapPin);
            Assert.IsNull(result.DiscardReason);
        }

        [TestMethod]
        public async Task ProcessAsync_AddsAddressAndReturnsMatchedPin_WhenTrainOnlyMatchIsValid()
        {
            var existingMapPin = CreateMapPin(beaconId: 99, subdivisionId: 101, railroadId: 1, milepost: 10,
                lastUpdate: _utcNow.AddHours(-1),
                addresses: [CreateAddress(99999, 777, SourceEnum.DPU, _utcNow.AddHours(-1))]);
            var telemetry = CreateTelemetry(source: SourceEnum.DPU, trainId: 777, addressId: 12345, beaconId: 10, railroadId: 1, milepost: 40);

            _mapPinRepositoryMock
                .Setup(r => r.GetByTrainIdAsync(777, DpuMapPinProcessor.TIME_THRESHOLD_DPU_TRAIN_ONLY_MINUTES))
                .ReturnsAsync(existingMapPin);

            var result = await _processor.ProcessAsync(telemetry);

            Assert.AreSame(existingMapPin, result.MapPin);
            Assert.IsFalse(result.IsNewMapPin);
            Assert.IsNull(result.DiscardReason);
            Assert.AreEqual(2, existingMapPin.Addresses.Count);

            var addedAddress = existingMapPin.Addresses.Single(a => a.AddressID == 12345);
            Assert.AreEqual(777, addedAddress.DpuTrainID);
            Assert.AreEqual(SourceEnum.DPU, addedAddress.Source);
            Assert.AreEqual(_utcNow, addedAddress.CreatedAt);
            Assert.AreEqual(_utcNow, addedAddress.LastUpdate);
        }

        [TestMethod]
        public void SupportedSources_ContainsOnlyDpu()
        {
            CollectionAssert.AreEqual(new[] { SourceEnum.DPU }, _processor.SupportedSources);
        }

        private Telemetry CreateTelemetry(string source, int? trainId, int addressId, int beaconId, int railroadId, double milepost)
        {
            var subdivision = new Subdivision
            {
                ID = railroadId + 100,
                RailroadID = railroadId,
                Name = $"Subdivision-{railroadId}",
                DpuCapable = true
            };

            var beaconRailroad = new BeaconRailroad
            {
                BeaconID = beaconId,
                SubdivisionID = subdivision.ID,
                Subdivision = subdivision,
                Milepost = milepost
            };

            return new Telemetry
            {
                BeaconID = beaconId,
                Beacon = new Beacon
                {
                    ID = beaconId,
                    Name = $"Beacon-{beaconId}",
                    BeaconRailroads = [beaconRailroad]
                },
                AddressID = addressId,
                TrainID = trainId,
                Source = source,
                CreatedAt = _utcNow,
                LastUpdate = _utcNow
            };
        }

        private static MapPin CreateMapPin(int beaconId, int subdivisionId, int railroadId, double milepost, IEnumerable<Address> addresses, DateTime? lastUpdate = null)
        {
            var subdivision = new Subdivision
            {
                ID = subdivisionId,
                RailroadID = railroadId,
                Name = $"Subdivision-{subdivisionId}",
                DpuCapable = true
            };

            return new MapPin
            {
                ID = 1,
                BeaconID = beaconId,
                SubdivisionId = subdivisionId,
                LastUpdate = lastUpdate ?? DateTime.UtcNow,
                BeaconRailroad = new BeaconRailroad
                {
                    BeaconID = beaconId,
                    SubdivisionID = subdivisionId,
                    Subdivision = subdivision,
                    Milepost = milepost
                },
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