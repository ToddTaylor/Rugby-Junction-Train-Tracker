using AutoMapper;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Web.Server.Entities;
using Web.Server.Hubs;
using Web.Server.Mappers;

namespace Web.Server.Services.Tests
{
    [TestClass]
    public class TelemetryServiceTests
    {
        private TelemetryService? _telemetryService;

        private Mock<IBeaconRepository> _mockBeaconRepository;
        private Mock<ITelemetryRepository> _mockTelemetryRepository;
        private Mock<IHubContext<NotificationHub>> _mockHubContext;
        private Mock<IHubClients> _mockHubClients;
        private Mock<IClientProxy> _mockClientProxy;
        private IMapper _mapper;

        [TestInitialize]
        public void Initialize()
        {
            _mockBeaconRepository = new Mock<IBeaconRepository>();
            _mockTelemetryRepository = new Mock<ITelemetryRepository>();
            _mockHubContext = new Mock<IHubContext<NotificationHub>>();
            _mockHubClients = new Mock<IHubClients>();
            _mockClientProxy = new Mock<IClientProxy>();

            var config = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<AutoMapperProfile>();
            });
            _mapper = config.CreateMapper();

            _telemetryService = new TelemetryService(
                _mockHubContext.Object,
                _mockTelemetryRepository.Object,
                _mockBeaconRepository.Object,
                _mapper);
        }

        [TestMethod]
        public async Task GetTelemetries_ReturnsAllTelemetries()
        {
            // Arrange
            var telemetries = new List<Telemetry>
            {
                new Telemetry { ID = 1, AddressID = 100, Source = "HOT", Timestamp = DateTime.UtcNow },
                new Telemetry { ID = 2, AddressID = 200, Source = "EOT", Timestamp = DateTime.UtcNow }
            };

            _mockTelemetryRepository.Setup(repo => repo.GetAllAsync())
                .ReturnsAsync(telemetries);

            // Act
            var result = await _telemetryService.GetTelemetries();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Count());
            _mockTelemetryRepository.Verify(repo => repo.GetAllAsync(), Times.Once);
        }

        [TestMethod]
        public void CreateTelemetry_AddsTelemetryAndUpdatesBeacon()
        {
            // Arrange
            var telemetry = new Telemetry
            {
                ID = 1,
                AddressID = 100,
                Source = "HOT",
                Timestamp = DateTime.UtcNow,
                Beacon = new Beacon
                {
                    ID = 1,
                    Latitude = 10.0,
                    Longitude = 20.0,
                    Timestamp = DateTime.UtcNow
                }
            };

            var existingTelemetry = new List<Telemetry>()
            {
                new Telemetry
                {
                    ID = 1,
                    AddressID = 100,
                    Source = "HOT",
                    Timestamp = DateTime.UtcNow,
                    Beacon = new Beacon
                    {
                        ID = 1,
                        Latitude = 10.0,
                        Longitude = 20.0,
                        Timestamp = DateTime.UtcNow,
                        Railroads = new List<Railroad> {
                            new Railroad {
                                ID = 1,
                                Name = "CN",
                                Subdivision = "Waukesha"
                            }
                        }
                    },
                }
            };

            var existingBeacon = new Beacon
            {
                ID = 1,
                Latitude = 10.0,
                Longitude = 20.0,
                Timestamp = DateTime.UtcNow
            };

            var expectedMapAlert = new MapAlert
            {
                AddressID = 100,
                Latitude = 10.0,
                Longitude = 20.0,
                Source = "HOT",
                Timestamp = DateTime.UtcNow,
                Direction = "W"
            };

            var expectedObjects = new object[]
            {
                expectedMapAlert
            };

            _mockTelemetryRepository.Setup(repo => repo.GetAllAsync())
                .ReturnsAsync(existingTelemetry);
            _mockTelemetryRepository.Setup(repo => repo.AddAsync(It.IsAny<Telemetry>()))
                .ReturnsAsync(telemetry);
            _mockBeaconRepository.Setup(repo => repo.GetByIdAsync(telemetry.Beacon.ID))
                .ReturnsAsync(existingBeacon);
            _mockBeaconRepository.Setup(repo => repo.UpdateAsync(It.IsAny<Beacon>()))
                .ReturnsAsync(existingBeacon);
            _mockHubContext.Setup(h => h.Clients).Returns(_mockHubClients.Object);
            _mockHubClients.Setup(h => h.All).Returns(_mockClientProxy.Object);
            _mockClientProxy.Setup(proxy => proxy.SendCoreAsync("MapAlert", expectedObjects, default))
                .Returns(Task.CompletedTask);

            // Act
            _telemetryService.CreateTelemetry(telemetry);

            // Assert
            _mockTelemetryRepository.Verify(repo => repo.AddAsync(It.Is<Telemetry>(t => t == telemetry)), Times.Once);
            _mockBeaconRepository.Verify(repo => repo.GetByIdAsync(telemetry.Beacon.ID), Times.Once);
            _mockBeaconRepository.Verify(repo => repo.UpdateAsync(It.Is<Beacon>(b => b.ID == telemetry.Beacon.ID)), Times.Once);
            //_mockClientProxy.Verify(proxy => proxy.SendCoreAsync(
            //    "MapAlert",
            //    It.Is<object[]>(args => args.Length == 1 && args[0] is MapAlert),
            //    default), Times.Once);

            _mockClientProxy.Verify(proxy => proxy.SendCoreAsync(
                "MapAlert",
                It.Is<object[]>(args => args[0] == expectedMapAlert),
                default), Times.Once);
        }

        [TestMethod]
        public void CreateTelemetry_ThrowsException_WhenBeaconNotFound()
        {
            // Arrange
            var telemetry = new Telemetry
            {
                ID = 1,
                AddressID = 100,
                Source = "HOT",
                Timestamp = DateTime.UtcNow,
                Beacon = new Beacon { ID = 1, Latitude = 10.0, Longitude = 20.0, Timestamp = DateTime.UtcNow }
            };

            _mockBeaconRepository.Setup(repo => repo.GetByIdAsync(telemetry.Beacon.ID))
                .ReturnsAsync((Beacon?)null);

            // Act & Assert
            Assert.ThrowsException<InvalidOperationException>(() => _telemetryService.CreateTelemetry(telemetry));
            _mockBeaconRepository.Verify(repo => repo.GetByIdAsync(telemetry.Beacon.ID), Times.Once);
            _mockTelemetryRepository.Verify(repo => repo.AddAsync(It.IsAny<Telemetry>()), Times.Never);
        }
    }
}