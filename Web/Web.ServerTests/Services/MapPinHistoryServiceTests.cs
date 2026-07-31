using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Moq;
using Web.Server.Entities;
using Web.Server.Providers;
using Web.Server.Repositories;
using Web.Server.Services;

namespace Web.ServerTests.Services
{
    [ExcludeFromCodeCoverage]
    [TestClass]
    public class MapPinHistoryServiceTests
    {
        private readonly Mock<IMapPinHistoryRepository> _repositoryMock = new();
        private readonly Mock<IBeaconRailroadService> _beaconRailroadServiceMock = new();
        private readonly Mock<ITimeProvider> _timeProviderMock = new();
        private readonly Mock<IConfiguration> _configurationMock = new();

        private MapPinHistoryService _service;

        [TestInitialize]
        public void Setup()
        {
            _timeProviderMock.Setup(tp => tp.UtcNow).Returns(DateTime.UtcNow);
            _configurationMock.Setup(c => c.GetSection("ApplicationSettings:HistoryTimeThresholdMinutes").Value)
                .Returns("360");
            _configurationMock.Setup(c => c.GetSection("ApplicationSettings:StationaryDirectionNullThresholdHours").Value)
                .Returns("6");

            _service = new MapPinHistoryService(
                _repositoryMock.Object,
                _beaconRailroadServiceMock.Object,
                _timeProviderMock.Object,
                _configurationMock.Object);
        }

        private static string AddressesJson(int addressID, string source = "HOT")
        {
            return JsonSerializer.Serialize(new[]
            {
                new { AddressID = addressID, Source = source, DpuTrainID = (int?)null, CreatedAt = DateTime.UtcNow, LastUpdate = DateTime.UtcNow }
            });
        }

        private static Subdivision MakeSubdivision(string? localTrainAddressIDs)
        {
            return new Subdivision
            {
                ID = 1,
                RailroadID = 1,
                Railroad = new Railroad { ID = 1, Name = "CN" },
                Name = "Waukesha",
                LocalTrainAddressIDs = localTrainAddressIDs,
            };
        }

        private static BeaconRailroad MakeBeaconRailroad(Subdivision subdivision)
        {
            return new BeaconRailroad
            {
                BeaconID = 1,
                SubdivisionID = subdivision.ID,
                Subdivision = subdivision,
                Latitude = 43.0,
                Longitude = -88.0,
                Milepost = 117.2,
            };
        }

        [TestMethod]
        public async Task GetHistoryByBeaconIdAsync_RecomputesIsLocal_TrueWhenAddressNowInSubdivisionLocalList()
        {
            // The stored snapshot says false (as it was when the record was last written by telemetry),
            // but the subdivision's local-train list has since been updated to include this address.
            var history = new MapPinHistory
            {
                ID = 1,
                BeaconID = 1,
                SubdivisionId = 1,
                IsLocal = false,
                AddressesJson = AddressesJson(29353),
            };

            var subdivision = MakeSubdivision("29353");
            _repositoryMock.Setup(r => r.GetByBeaconIdAsync(1, null, null))
                .ReturnsAsync(new List<MapPinHistory> { history });
            _beaconRailroadServiceMock.Setup(s => s.GetByIdAsync(1, 1))
                .ReturnsAsync(MakeBeaconRailroad(subdivision));

            var result = (await _service.GetHistoryByBeaconIdAsync(1)).ToList();

            Assert.AreEqual(1, result.Count);
            Assert.IsTrue(result[0].IsLocal);
        }

        [TestMethod]
        public async Task GetHistoryByBeaconIdAsync_RecomputesIsLocal_FalseWhenAddressRemovedFromSubdivisionLocalList()
        {
            // The stored snapshot says true, but the subdivision's local-train list no longer includes it.
            var history = new MapPinHistory
            {
                ID = 2,
                BeaconID = 1,
                SubdivisionId = 1,
                IsLocal = true,
                AddressesJson = AddressesJson(29353),
            };

            var subdivision = MakeSubdivision("");
            _repositoryMock.Setup(r => r.GetByBeaconIdAsync(1, null, null))
                .ReturnsAsync(new List<MapPinHistory> { history });
            _beaconRailroadServiceMock.Setup(s => s.GetByIdAsync(1, 1))
                .ReturnsAsync(MakeBeaconRailroad(subdivision));

            var result = (await _service.GetHistoryByBeaconIdAsync(1)).ToList();

            Assert.IsFalse(result[0].IsLocal);
        }

        [TestMethod]
        public async Task GetHistoryByBeaconIdAsync_KeepsStoredIsLocal_WhenBeaconRailroadCannotBeResolved()
        {
            var history = new MapPinHistory
            {
                ID = 3,
                BeaconID = 1,
                SubdivisionId = 1,
                IsLocal = true,
                AddressesJson = AddressesJson(29353),
            };

            _repositoryMock.Setup(r => r.GetByBeaconIdAsync(1, null, null))
                .ReturnsAsync(new List<MapPinHistory> { history });
            _beaconRailroadServiceMock.Setup(s => s.GetByIdAsync(1, 1))
                .ReturnsAsync((BeaconRailroad?)null);

            var result = (await _service.GetHistoryByBeaconIdAsync(1)).ToList();

            Assert.IsTrue(result[0].IsLocal);
        }

        [TestMethod]
        public async Task GetHistoryByBeaconIdAsync_KeepsStoredIsLocal_WhenAddressesJsonIsUnparseable()
        {
            var history = new MapPinHistory
            {
                ID = 4,
                BeaconID = 1,
                SubdivisionId = 1,
                IsLocal = true,
                AddressesJson = "not valid json",
            };

            var subdivision = MakeSubdivision("");
            _repositoryMock.Setup(r => r.GetByBeaconIdAsync(1, null, null))
                .ReturnsAsync(new List<MapPinHistory> { history });
            _beaconRailroadServiceMock.Setup(s => s.GetByIdAsync(1, 1))
                .ReturnsAsync(MakeBeaconRailroad(subdivision));

            var result = (await _service.GetHistoryByBeaconIdAsync(1)).ToList();

            Assert.IsTrue(result[0].IsLocal);
        }
    }
}
