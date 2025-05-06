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

        private Mock<IBeaconRepository>? _mockBeaconRepository;
        private Mock<ITelemetryRepository>? _mockTelemetryRepository;
        private Mock<IHubContext<NotificationHub>>? _mockHubContext;
        private Mock<IHubClients>? _mockHubClients;
        private Mock<IClientProxy>? _mockClientProxy;
        private IMapper? _mapper;

        private DateTime _currentDateTime;

        [TestInitialize]
        public void Initialize()
        {
            _mockBeaconRepository = new Mock<IBeaconRepository>();
            _mockTelemetryRepository = new Mock<ITelemetryRepository>();
            _mockHubContext = new Mock<IHubContext<NotificationHub>>();
            _mockHubClients = new Mock<IHubClients>();
            _mockClientProxy = new Mock<IClientProxy>();

            _currentDateTime = DateTime.UtcNow;

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
        public async Task CreateTelemetry_ThrowsException_WhenBeaconNotFound()
        {
            // Arrange
            var telemetry = new Telemetry
            {
                ID = 1,
                AddressID = 100,
                Source = "HOT",
                Timestamp = DateTime.UtcNow,
                Beacon = new Beacon { ID = 1, Timestamp = DateTime.UtcNow }
            };

            _mockBeaconRepository.Setup(repo => repo.GetByIdAsync(telemetry.Beacon.ID))
                .ReturnsAsync((Beacon?)null);

            // Act
            var ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
            {
                await _telemetryService.CreateTelemetry(telemetry);
            });

            // Assert
            Assert.AreEqual(ex.Message, "Beacon not found.");
            _mockBeaconRepository.Verify(repo => repo.GetByIdAsync(telemetry.Beacon.ID), Times.Once);
            _mockBeaconRepository.Verify(repo => repo.UpdateAsync(It.IsAny<Beacon>()), Times.Never);
        }

        [TestMethod]
        public void CreateTelemetry_AddsTelemetryAndUpdatesBeacon()
        {
            // Arrange
            var telemetry = new Telemetry
            {
                ID = 432,
                AddressID = 90234,
                Source = "HOT",
                Timestamp = _currentDateTime, // TODO: This won't be set in POST
                Beacon = new Beacon
                {
                    ID = 534,
                    Timestamp = _currentDateTime
                }
            };

            var telemetryAdded = new Telemetry
            {
                ID = 432,
                AddressID = 90234,
                Source = "HOT",
                Timestamp = _currentDateTime, // TODO: This won't be set in POST
                Beacon = new Beacon
                {
                    ID = 534,
                    Timestamp = _currentDateTime,
                    BeaconRailroads = new List<BeaconRailroad> {
                        new BeaconRailroad {
                            RailroadID = 12,
                            BeaconID = 1,
                            Latitude = 10.0,
                            Longitude = 20.0
                        },
                        new BeaconRailroad {
                            RailroadID = 13,
                            BeaconID = 1,
                            Latitude = 14.0,
                            Longitude = 32.0
                        }
                    }
                }
            };

            var existingTelemetry = new List<Telemetry>()
            {
                new Telemetry
                {
                    ID = 623,
                    AddressID = 90234,
                    Source = "HOT",
                    Timestamp = _currentDateTime.AddHours(-5),
                    Beacon = new Beacon
                    {
                        ID = 534,
                        OwnerID = 19,
                        Timestamp = _currentDateTime.AddHours(-5),
                        BeaconRailroads = new List<BeaconRailroad> {
                            new BeaconRailroad {
                                RailroadID = 12,
                                BeaconID = 42,
                                Latitude = 10.0,
                                Longitude = 20.0
                            }
                        }
                    },
                }
            };

            var existingBeacon = new Beacon
            {
                ID = 534,
                OwnerID = 19,
                Timestamp = _currentDateTime
            };

            var expectedMapAlert = new MapAlert
            {
                AddressID = 90234,
                Latitude = 10.0,
                Longitude = 20.0,
                Source = "HOT",
                Timestamp = _currentDateTime,
                Direction = "W"
            };

            var expectedObjects = new object[]
            {
                expectedMapAlert
            };

            _mockBeaconRepository.Setup(repo => repo.GetByIdAsync(telemetry.Beacon.ID))
                .ReturnsAsync(existingBeacon);
            _mockBeaconRepository.Setup(repo => repo.UpdateAsync(It.IsAny<Beacon>()))
                .ReturnsAsync(existingBeacon);
            _mockTelemetryRepository.Setup(repo => repo.GetAllAsync())
                .ReturnsAsync(existingTelemetry);
            _mockTelemetryRepository.Setup(repo => repo.AddAsync(It.IsAny<Telemetry>()))
                .ReturnsAsync(telemetryAdded);
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

            _mockClientProxy.Verify(proxy => proxy.SendCoreAsync(
                "MapAlert",
                It.Is<object[]>(args => args[0].Equals(expectedMapAlert)),
                default), Times.Once);
        }
    }
}