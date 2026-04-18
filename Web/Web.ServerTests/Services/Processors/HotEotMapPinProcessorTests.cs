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

        private HotEotMapPinProcessor _processor = null!;
        private DateTime _utcNow;

        [TestInitialize]
        public void Setup()
        {
            _utcNow = new DateTime(2026, 3, 27, 1, 0, 0, DateTimeKind.Utc);
            _processor = new HotEotMapPinProcessor(
                _mapPinRepositoryMock.Object,
                new NullLogger<HotEotMapPinProcessor>());

            _mapPinRepositoryMock
                .Setup(r => r.GetByAddressIdAsync(It.IsAny<int>()))
                .ReturnsAsync((MapPin?)null);
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
                .Setup(r => r.GetByAddressIdAsync(12345))
                .ReturnsAsync(existingMapPin);

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
                .Setup(r => r.GetByAddressIdAsync(12345))
                .ReturnsAsync(existingMapPin);

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
                .Setup(r => r.GetByAddressIdAsync(12345))
                .ReturnsAsync(existingMapPin);

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