using AutoMapper;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Web.Server.DTOs;
using Web.Server.Entities;
using Web.Server.Enums;
using Web.Server.Hubs;
using Web.Server.Providers;
using Web.Server.Repositories;
using Web.Server.Services;

namespace Web.ServerTests.Services
{
    [TestClass]
    public class TelemetryServiceTests
    {
        private Mock<ITelemetryRepository> _telemetryRepositoryMock;
        private Mock<IBeaconService> _beaconServiceMock;
        private Mock<IBeaconRailroadService> _beaconRailroadServiceMock;
        private Mock<IHubContext<NotificationHub>> _hubContextMock;
        private Mock<IMapper> _mapperMock;
        private Mock<IMapPinService> _mapPinServiceMock;
        private Mock<ITimeProvider> _timeProviderMock;
        private TelemetryService _service;

        [TestInitialize]
        public void Setup()
        {
            _telemetryRepositoryMock = new Mock<ITelemetryRepository>();
            _beaconServiceMock = new Mock<IBeaconService>();
            _beaconRailroadServiceMock = new Mock<IBeaconRailroadService>();
            _hubContextMock = new Mock<IHubContext<NotificationHub>>();
            _mapperMock = new Mock<IMapper>();
            _mapPinServiceMock = new Mock<IMapPinService>();
            _timeProviderMock = new Mock<ITimeProvider>();

            // Setup SignalR Clients.All.SendAsync
            var clientsMock = new Mock<IHubClients>();
            var clientProxyMock = new Mock<IClientProxy>();
            clientsMock.Setup(c => c.All).Returns(clientProxyMock.Object);
            _hubContextMock.Setup(h => h.Clients).Returns(clientsMock.Object);

            _service = new TelemetryService(
                _beaconRailroadServiceMock.Object,
                _beaconServiceMock.Object,
                _hubContextMock.Object,
                _mapperMock.Object,
                _mapPinServiceMock.Object,
                _telemetryRepositoryMock.Object
            );
        }

        [TestMethod]
        public async Task GetTelemetriesAsync_ReturnsAllTelemetries()
        {
            // Arrange
            var telemetries = new List<Telemetry> { new Telemetry { BeaconID = 1, AddressID = 1, Source = "HOT", CreatedAt = DateTime.UtcNow } };
            _telemetryRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(telemetries);

            // Act
            var result = await _service.GetTelemetriesAsync();

            // Assert
            Assert.AreEqual(telemetries, result);
        }

        [TestMethod]
        public async Task GetTelemetryByIdAsync_ReturnsTelemetry_WhenFound()
        {
            // Arrange
            var telemetry = new Telemetry { BeaconID = 1, AddressID = 1, Source = "HOT", CreatedAt = DateTime.UtcNow };
            _telemetryRepositoryMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(telemetry);

            // Act
            var result = await _service.GetTelemetryByIdAsync(1);

            // Assert
            Assert.AreEqual(telemetry, result);
        }

        [TestMethod]
        public async Task GetTelemetryByIdAsync_ReturnsNull_WhenNotFound()
        {
            // Arrange
            _telemetryRepositoryMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync((Telemetry?)null);

            // Act
            var result = await _service.GetTelemetryByIdAsync(1);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task CreateTelemetryAsync_Throws_WhenAddressIdInvalid()
        {
            // Arrange
            var telemetry = new Telemetry { BeaconID = 1, AddressID = 0, Source = "HOT", CreatedAt = DateTime.UtcNow };

            // Act & Assert
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => _service.CreateTelemetryAsync(telemetry));
        }

        [TestMethod]
        public async Task CreateTelemetryAsync_Throws_WhenBeaconNotFound()
        {
            // Arrange
            var telemetry = new Telemetry { BeaconID = 1, AddressID = 1, Source = "HOT", CreatedAt = DateTime.UtcNow };
            _beaconServiceMock.Setup(s => s.GetBeaconByIdAsync(1)).ReturnsAsync((Beacon?)null);

            // Act & Assert
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => _service.CreateTelemetryAsync(telemetry));
        }

        [TestMethod]
        public async Task CreateTelemetryAsync_Successful()
        {
            // Arrange
            var beaconRailroad = new BeaconRailroad
            {
                BeaconID = 1,
                Direction = Direction.NorthSouth,
                SubdivisionID = 1,
                Latitude = 0,
                Longitude = 0,
                Milepost = 0,
                MultipleTracks = false
            };
            var beacon = new Beacon
            {
                ID = 1,
                OwnerID = 1,
                Owner = new User
                {
                    ID = 1,
                    FirstName = "Test",
                    LastName = "Owner",
                    Email = "test@example.com"
                },
                BeaconRailroads = new List<BeaconRailroad> { beaconRailroad },
                Telemetries = new List<Telemetry>()
            };
            var telemetry = new Telemetry { BeaconID = 1, AddressID = 1, Source = "HOT", CreatedAt = DateTime.UtcNow };
            var addedTelemetry = new Telemetry { BeaconID = 1, AddressID = 1, Source = "HOT", CreatedAt = DateTime.UtcNow };
            var beaconRailroadDto = new BeaconRailroadDTO();
            var beaconRailroadDtos = new List<BeaconRailroadDTO> { beaconRailroadDto };

            _beaconServiceMock.Setup(s => s.GetBeaconByIdAsync(1)).ReturnsAsync(beacon);
            _telemetryRepositoryMock.Setup(r => r.AddAsync(telemetry)).ReturnsAsync(addedTelemetry);
            _mapPinServiceMock.Setup(m => m.UpsertMapPin(It.IsAny<Telemetry>(), It.IsAny<ICollection<BeaconRailroad>>()))
                .Returns(Task.CompletedTask);
            _beaconRailroadServiceMock.Setup(b => b.UpdateAsync(It.IsAny<ICollection<BeaconRailroad>>()))
                .ReturnsAsync(new List<BeaconRailroad> { beaconRailroad });
            _mapperMock.Setup(m => m.Map<ICollection<BeaconRailroadDTO>>(It.IsAny<ICollection<BeaconRailroad>>()))
                .Returns(beaconRailroadDtos);
            _timeProviderMock.Setup(t => t.UtcNow).Returns(DateTime.UtcNow);

            // Act
            var result = await _service.CreateTelemetryAsync(telemetry);

            // Assert
            Assert.AreEqual(addedTelemetry, result);
            _beaconServiceMock.Verify(s => s.GetBeaconByIdAsync(1), Times.Once);
            _telemetryRepositoryMock.Verify(r => r.AddAsync(telemetry), Times.Once);
            _mapPinServiceMock.Verify(m => m.UpsertMapPin(addedTelemetry, beacon.BeaconRailroads), Times.Once);
        }
    }
}