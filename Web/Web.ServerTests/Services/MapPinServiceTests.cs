using MapsterMapper;
using Mapster;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Web.Server.DTOs;
using Web.Server.Entities;
using Web.Server.Enums;
using Web.Server.Hubs;
using Web.Server.Mappers;
using Web.Server.Providers;
using Web.Server.Repositories;
using Web.Server.Services;
using Web.Server.Services.Rules;

namespace Web.ServerTests.Services
{
    [ExcludeFromCodeCoverage]
    [TestClass]
    public class MapPinServiceTests
    {
        private readonly Mock<IBeaconRailroadService> _beaconRailroadServiceMock = new();
        private readonly Mock<IMapPinHistoryService> _mapPinHistoryServiceMock = new();
        private readonly Mock<IHubClients> _hubClientsMock = new();
        private readonly Mock<IHubContext<NotificationHub>> _hubContextMock = new();
        private readonly Mock<IMapPinRepository> _mapPinRepositoryMock = new();
        private readonly Mock<ITimeProvider> _timeProviderMock = new();
        private readonly Mock<IClientProxy> _clientProxyMock = new();
        private readonly Mock<IConfiguration> _configurationMock = new();
        private readonly Mock<ITelemetryRepository> _telemetryRepositoryMock = new();
        private readonly Mock<ISubdivisionTrackageRightRepository> _trackageRightRepositoryMock = new();
        private readonly Mock<IUserTrackedPinRepository> _userTrackedPinRepositoryMock = new();
        private readonly Mock<ILogger<MapPinService>> _loggerMock = new();

        private MapPinService _service;
        private IMapper _mapper;
        private IMapPinRuleEngine _mapPinRuleEngine;
        private ITelemetryRuleEngine _telemetryRuleEngine;

        [TestInitialize]
        public void Setup()
        {
            _timeProviderMock.Setup(tp => tp.UtcNow).Returns(DateTime.UtcNow);

            // Default: no duplicate map pins at any beacon (no merging)
            _mapPinRepositoryMock.Setup(r => r.GetAllByBeaconAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(new List<MapPin>());

            // Default: no exact DPU address+train match
            _mapPinRepositoryMock.Setup(r => r.GetByAddressAndTrainIdAsync(It.IsAny<int>(), It.IsAny<int>(), MapPinService.TIME_THRESHOLD_DPU_EXACT_MINUTES))
                .ReturnsAsync((MapPin?)null);

            // Default: no tracked pins to migrate during merges
            _userTrackedPinRepositoryMock.Setup(r => r.UpdateMapPinIdAsync(It.IsAny<int>(), It.IsAny<int>()))
                .Returns(Task.CompletedTask);

            var config = new TypeAdapterConfig();
            config.Scan(typeof(MapsterProfile).Assembly);
            var provider = new ServiceCollection().BuildServiceProvider();
            _mapper = new ServiceMapper(provider, config);

            _configurationMock.Setup(c => c.GetSection("ApplicationSettings:StationaryDirectionNullThresholdHours").Value)
                .Returns("6");

            // Initialize real rule engines with actual rules (same as production)
            var mapPinRules = new List<IMapPinRule>
            {
                new TrackageRightsRule(_trackageRightRepositoryMock.Object)
            };
            _mapPinRuleEngine = new MapPinRuleEngine(mapPinRules);

            var telemetryRules = new List<ITelemetryRule>
            {
                new TrainSpeedSanityCheckRule(_telemetryRepositoryMock.Object),
                new DpuAntiPingPongRule(_telemetryRepositoryMock.Object),
                new EotHotAntiPingPongRule(_telemetryRepositoryMock.Object)
            };
            _telemetryRuleEngine = new TelemetryRuleEngine(telemetryRules);

            _service = new MapPinService(
                _beaconRailroadServiceMock.Object,
                _mapPinHistoryServiceMock.Object,
                _mapPinRepositoryMock.Object,
                _hubContextMock.Object,
                _mapper,
                _timeProviderMock.Object,
                _telemetryRepositoryMock.Object,
                _mapPinRuleEngine,
                _telemetryRuleEngine,
                _trackageRightRepositoryMock.Object,
                _userTrackedPinRepositoryMock.Object,
                _loggerMock.Object,
                _configurationMock.Object,
                new Dictionary<string, Web.Server.Services.Processors.IMapPinProcessor>
                {
                    // Use real processors with mocked dependencies
                    { SourceEnum.DPU, new Web.Server.Services.Processors.DpuMapPinProcessor(_mapPinRepositoryMock.Object, new Microsoft.Extensions.Logging.Abstractions.NullLogger<Web.Server.Services.Processors.DpuMapPinProcessor>()) },
                    { SourceEnum.HOT, new Web.Server.Services.Processors.HotEotMapPinProcessor(_mapPinRepositoryMock.Object, new Microsoft.Extensions.Logging.Abstractions.NullLogger<Web.Server.Services.Processors.HotEotMapPinProcessor>()) },
                    { SourceEnum.EOT, new Web.Server.Services.Processors.HotEotMapPinProcessor(_mapPinRepositoryMock.Object, new Microsoft.Extensions.Logging.Abstractions.NullLogger<Web.Server.Services.Processors.HotEotMapPinProcessor>()) }
                });
        }

        [TestMethod]
        public async Task GetMapPinByIdAsync_ReturnsMapPin_WhenFound()
        {
            // Arrange
            var mapPin = new MapPin { ID = 1, BeaconID = 1, SubdivisionId = 1 };
            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(1)).ReturnsAsync(mapPin);

            // Act
            var result = await _service.GetMapPinByIdAsync(1);

            // Assert
            Assert.AreEqual(mapPin, result);
        }

        [TestMethod]
        public async Task GetMapPinByIdAsync_ReturnsNull_WhenNotFound()
        {
            // Arrange
            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(1)).ReturnsAsync((MapPin?)null);

            // Act
            var result = await _service.GetMapPinByIdAsync(1);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetMapPinsAsync_ReturnsAllMapPins()
        {
            // Arrange
            var mapPins = new List<MapPin> { new MapPin { ID = 1, BeaconID = 1, SubdivisionId = 1 } };
            _mapPinRepositoryMock.Setup(r => r.GetAllAsync(null)).ReturnsAsync(mapPins);

            // Act
            var result = await _service.GetMapPinsAsync(null);

            // Assert
            Assert.AreEqual(mapPins, result);
        }

        [TestMethod]
        public void CalculateDirection_WhenConstrainedDirectionIsEmpty_FallsBackToAllDirections()
        {
            // Arrange
            var fromBeaconRailroad = new BeaconRailroad
            {
                Latitude = 43.0,
                Longitude = -88.0,
                Direction = Direction.All
            };

            var toBeaconRailroad = new BeaconRailroad
            {
                Latitude = 44.0,
                Longitude = -88.0,
                Direction = Direction.EastWest
            };

            var calculateDirectionMethod = typeof(MapPinService)
                .GetMethod("CalculateDirection", BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(calculateDirectionMethod);

            // Act
            var direction = calculateDirectionMethod!.Invoke(null, [fromBeaconRailroad, toBeaconRailroad]) as string;

            // Assert
            Assert.AreEqual("N", direction);
        }

        [TestMethod]
        public async Task UpsertMapPin_CreateMapPin_MultiRailroad()
        {
            // Arrange
            var CNRugbyJunctionBeacon = TestData.CN_RugbyJunction_WI(_timeProviderMock.Object.UtcNow);
            var WSORRugbyJunctionBeacon = TestData.WSOR_RugbyJunction_WI(_timeProviderMock.Object.UtcNow);

            // Hack: First railroad listed will be chosen because this is a multi-railroad beacon
            // and the only other option is to not show the map pin at all.
            var beaconRailroads = new List<BeaconRailroad>
            {
                WSORRugbyJunctionBeacon, // Hack: First railroad will be chosen.
                CNRugbyJunctionBeacon
            };

            var telemetry = new Telemetry
            {
                BeaconID = CNRugbyJunctionBeacon.BeaconID,
                Beacon = new Beacon
                {
                    ID = CNRugbyJunctionBeacon.BeaconID,
                    Name = CNRugbyJunctionBeacon.Beacon.Name,
                    BeaconRailroads = beaconRailroads
                },
                AddressID = 23424,
                Source = SourceEnum.HOT,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow,
                Moving = true
            };

            var expectedMapPinBeforeInsert = new MapPin
            {
                ID = 0, // New map pin, ID will be generated by the database.
                BeaconID = WSORRugbyJunctionBeacon.BeaconID,
                SubdivisionId = WSORRugbyJunctionBeacon.Subdivision.ID,
                CreatedRailroadID = WSORRugbyJunctionBeacon.Subdivision.RailroadID,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow,
                BeaconRailroad = WSORRugbyJunctionBeacon,
                Moving = telemetry.Moving,
                Addresses =
                [
                    new Address
                    {
                        AddressID = telemetry.AddressID,
                        Source = telemetry.Source,
                        CreatedAt = _timeProviderMock.Object.UtcNow,
                        LastUpdate = _timeProviderMock.Object.UtcNow
                    }
                ],
            };

            var expectedMapPinAfterInsert = new MapPin
            {
                ID = 234, // ID returned.
                BeaconID = WSORRugbyJunctionBeacon.BeaconID,
                SubdivisionId = WSORRugbyJunctionBeacon.Subdivision.ID,
                CreatedRailroadID = WSORRugbyJunctionBeacon.Subdivision.RailroadID,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow,
                BeaconRailroad = WSORRugbyJunctionBeacon,
                Moving = telemetry.Moving,
                Addresses =
                [
                    new Address
                    {
                        AddressID = telemetry.AddressID,
                        Source = telemetry.Source,
                        CreatedAt = _timeProviderMock.Object.UtcNow,
                        LastUpdate = _timeProviderMock.Object.UtcNow
                    }
                ],
            };

            // This map pin is a guess based on the first railroad for the multi-railroad beacon. Due to a
            // hack in the code to temporarily provide a beacon rather than nothing, the map pin will
            // show the first railroad in the list, which in this test case is WSOR even though the telemetry
            // beacon ID came from the CN.
            var expectedMapPinObjects = new object[]
            {
                new MapPinDTO
                {
                    ID = 234,
                    Direction = null,
                    BeaconID = WSORRugbyJunctionBeacon.BeaconID,
                    BeaconName = WSORRugbyJunctionBeacon.Beacon.Name,
                    Railroad = WSORRugbyJunctionBeacon.Subdivision.Railroad.Name,
                    Subdivision = WSORRugbyJunctionBeacon.Subdivision.Name,
                    SubdivisionID = WSORRugbyJunctionBeacon.Subdivision.ID,
                    Latitude = WSORRugbyJunctionBeacon.Latitude,
                    Longitude = WSORRugbyJunctionBeacon.Longitude,
                    Milepost = WSORRugbyJunctionBeacon.Milepost,
                    Moving = telemetry.Moving,
                    CreatedAt = _timeProviderMock.Object.UtcNow,
                    LastUpdate = _timeProviderMock.Object.UtcNow,
                    Addresses =
                    [
                        new AddressDTO
                        {
                            AddressID = telemetry.AddressID,
                            Source = telemetry.Source
                        }
                    ],
                }
            };

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID))
                .ReturnsAsync((MapPin?)null);
            _mapPinRepositoryMock.Setup(r => r.GetByTimeThreshold(telemetry.BeaconID, WSORRugbyJunctionBeacon.Subdivision.RailroadID, 5))
                .ReturnsAsync((MapPin?)null);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(expectedMapPinBeforeInsert, telemetry.LastUpdate))
                .ReturnsAsync(expectedMapPinAfterInsert);

            _clientProxyMock.Setup(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate, expectedMapPinObjects, default))
                    .Returns(Task.CompletedTask);
            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(expectedMapPinBeforeInsert, telemetry.LastUpdate), Times.Once);

            _clientProxyMock?.Verify(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate,
                It.Is<object[]>(args => args[0].Equals(expectedMapPinObjects[0])),
                default), Times.Once);
        }

        [TestMethod]
        public async Task UpsertMapPin_CreateMapPin_SingleRailroad()
        {
            // Arrange
            var WSORRugbyJunctionBeacon = TestData.WSOR_RugbyJunction_WI(_timeProviderMock.Object.UtcNow);

            var beaconRailroads = new List<BeaconRailroad>
            {
                WSORRugbyJunctionBeacon
            };

            var telemetry = new Telemetry
            {
                BeaconID = WSORRugbyJunctionBeacon.BeaconID,
                Beacon = new Beacon
                {
                    ID = WSORRugbyJunctionBeacon.BeaconID,
                    Name = WSORRugbyJunctionBeacon.Beacon.Name,
                    BeaconRailroads = beaconRailroads
                },
                AddressID = 23424,
                Source = SourceEnum.HOT,
                Moving = true,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow
            };

            var expectedMapPinBeforeInsert = new MapPin
            {
                ID = 0, // New map pin, ID will be generated by the database.
                BeaconID = WSORRugbyJunctionBeacon.BeaconID,
                SubdivisionId = WSORRugbyJunctionBeacon.Subdivision.ID,
                CreatedRailroadID = WSORRugbyJunctionBeacon.Subdivision.RailroadID,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow,
                BeaconRailroad = WSORRugbyJunctionBeacon,
                Moving = telemetry.Moving,
                Addresses =
                [
                    new Address
                    {
                        AddressID = telemetry.AddressID,
                        Source = telemetry.Source,
                        CreatedAt = _timeProviderMock.Object.UtcNow,
                        LastUpdate = _timeProviderMock.Object.UtcNow
                    }
                ],
            };

            var expectedMapPinAfterInsert = expectedMapPinBeforeInsert.Clone();
            expectedMapPinAfterInsert.ID = 234;

            var expectedMapPinObjects = new object[]
            {
                new MapPinDTO
                {
                    ID = expectedMapPinAfterInsert.ID,
                    Direction = null,
                    BeaconID = WSORRugbyJunctionBeacon.BeaconID,
                    BeaconName = WSORRugbyJunctionBeacon.Beacon.Name,
                    Railroad = WSORRugbyJunctionBeacon.Subdivision.Railroad.Name,
                    Subdivision = WSORRugbyJunctionBeacon.Subdivision.Name,
                    SubdivisionID = WSORRugbyJunctionBeacon.Subdivision.ID,
                    Latitude = WSORRugbyJunctionBeacon.Latitude,
                    Longitude = WSORRugbyJunctionBeacon.Longitude,
                    Milepost = WSORRugbyJunctionBeacon.Milepost,
                    Moving = telemetry.Moving,
                    CreatedAt = _timeProviderMock.Object.UtcNow,
                    LastUpdate = _timeProviderMock.Object.UtcNow,
                    Addresses =
                    [
                        new AddressDTO
                        {
                            AddressID = telemetry.AddressID,
                            Source = telemetry.Source
                        }
                    ],
                }
            };

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID))
                .ReturnsAsync((MapPin?)null);
            _mapPinRepositoryMock.Setup(r => r.GetByTimeThreshold(telemetry.BeaconID, WSORRugbyJunctionBeacon.SubdivisionID, 5))
                .ReturnsAsync((MapPin?)null);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(expectedMapPinBeforeInsert, telemetry.LastUpdate))
                .ReturnsAsync(expectedMapPinAfterInsert);

            _clientProxyMock.Setup(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate, expectedMapPinObjects, default))
                    .Returns(Task.CompletedTask);
            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(expectedMapPinBeforeInsert, telemetry.LastUpdate), Times.Once);

            _clientProxyMock?.Verify(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate,
                It.Is<object[]>(args => args[0].Equals(expectedMapPinObjects[0])),
                default), Times.Once);
        }

        [TestMethod]
        public async Task UpsertMapPin_AddHOTToExistingEOTMapPin()
        {
            // Arrange
            var CNRugbyJunctionBeacon = TestData.CN_RugbyJunction_WI(_timeProviderMock.Object.UtcNow);

            var telemetry = new Telemetry
            {
                BeaconID = CNRugbyJunctionBeacon.BeaconID,
                Beacon = new Beacon
                {
                    ID = CNRugbyJunctionBeacon.BeaconID,
                    Name = CNRugbyJunctionBeacon.Beacon.Name,
                    BeaconRailroads =
                    [
                        CNRugbyJunctionBeacon
                    ]
                },
                AddressID = 12345,
                TrainID = null,
                Source = SourceEnum.HOT,
                Moving = true,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow,
            };

            var fromMapPin = new MapPin
            {
                ID = 234,
                BeaconID = CNRugbyJunctionBeacon.BeaconID,
                SubdivisionId = CNRugbyJunctionBeacon.Subdivision.ID,
                Direction = "N",
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow,
                BeaconRailroad = CNRugbyJunctionBeacon,
                CreatedRailroadID = CNRugbyJunctionBeacon.Subdivision.RailroadID,
                Moving = telemetry.Moving,
                Addresses =
                    [
                        new Address
                        {
                            AddressID = 12345,
                            Source = SourceEnum.EOT,
                            CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(-2),
                            LastUpdate = _timeProviderMock.Object.UtcNow.AddMinutes(-2)
                        }
                    ],
            };

            var toMapPinBeforeUpdate = new MapPin
            {
                ID = 234,
                BeaconID = CNRugbyJunctionBeacon.BeaconID,
                SubdivisionId = CNRugbyJunctionBeacon.Subdivision.ID,
                Direction = "N",
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow,
                BeaconRailroad = CNRugbyJunctionBeacon,
                Moving = telemetry.Moving,
                CreatedRailroadID = CNRugbyJunctionBeacon.Subdivision.RailroadID,
                Addresses =
                [
                    new Address
                    {
                        AddressID = 12345,
                        Source = SourceEnum.EOT,
                        CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(-2),
                        LastUpdate = _timeProviderMock.Object.UtcNow.AddMinutes(-2)
                    },
                    new Address
                    {
                        AddressID = 12345,
                        Source = SourceEnum.HOT,
                        CreatedAt = _timeProviderMock.Object.UtcNow,
                        LastUpdate = _timeProviderMock.Object.UtcNow
                    }
                ],
            };

            var toMapPinAfterUpdate = toMapPinBeforeUpdate.Clone();
            toMapPinAfterUpdate.ID = 936; // ID returned after insert.

            var toMapPinObjects = new object[]
            {
                new MapPinDTO
                {
                    ID = toMapPinAfterUpdate.ID,
                    Direction = "N",
                    BeaconID = telemetry.BeaconID,
                    BeaconName = CNRugbyJunctionBeacon.Beacon.Name,
                    Railroad = CNRugbyJunctionBeacon.Subdivision.Railroad.Name,
                    Subdivision = CNRugbyJunctionBeacon.Subdivision.Name,
                    SubdivisionID = CNRugbyJunctionBeacon.Subdivision.ID,
                    Latitude = CNRugbyJunctionBeacon.Latitude,
                    Longitude = CNRugbyJunctionBeacon.Longitude,
                    Milepost = CNRugbyJunctionBeacon.Milepost,
                    Moving = telemetry.Moving,
                    CreatedAt = _timeProviderMock.Object.UtcNow,
                    LastUpdate = _timeProviderMock.Object.UtcNow,
                    Addresses =
                    [
                        new AddressDTO
                        {
                            AddressID = 12345,
                            Source = SourceEnum.EOT
                        },
                        new AddressDTO
                        {
                            AddressID = 12345,
                            Source = SourceEnum.HOT
                        }
                    ],
                }
             };

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID))
                .ReturnsAsync(fromMapPin);
            _beaconRailroadServiceMock.Setup(b => b.GetByIdAsync(telemetry.BeaconID, fromMapPin.SubdivisionId))
                .ReturnsAsync(CNRugbyJunctionBeacon);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(toMapPinBeforeUpdate, telemetry.LastUpdate))
                .ReturnsAsync(toMapPinAfterUpdate);

            _clientProxyMock.Setup(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate, toMapPinObjects, default))
                    .Returns(Task.CompletedTask);
            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(toMapPinBeforeUpdate, telemetry.LastUpdate), Times.Once);

            _clientProxyMock?.Verify(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate,
                It.Is<object[]>(args => args[0].Equals(toMapPinObjects[0])),
                default), Times.Once);
        }

        [TestMethod]
        public async Task UpsertMapPin_CreateMapPin_SingleRailroad_SwitchRailroads()
        {
            // Arrange
            var WSORHartfordBeacon = TestData.WSOR_Hartford_WI(_timeProviderMock.Object.UtcNow);
            var CNRugbyJunctionBeacon = TestData.CN_RugbyJunction_WI(_timeProviderMock.Object.UtcNow);

            // Train just got on CN from WSOR
            var beaconRailroads = new List<BeaconRailroad>
            {
                CNRugbyJunctionBeacon
            };

            var telemetry = new Telemetry
            {
                BeaconID = CNRugbyJunctionBeacon.BeaconID,
                Beacon = new Beacon
                {
                    ID = CNRugbyJunctionBeacon.BeaconID,
                    Name = CNRugbyJunctionBeacon.Beacon.Name,
                    BeaconRailroads = beaconRailroads
                },
                AddressID = 23424,
                Source = SourceEnum.HOT,
                Moving = true,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow
            };

            var previousMapPin = new MapPin
            {
                ID = 29032,
                BeaconID = WSORHartfordBeacon.BeaconID,
                SubdivisionId = WSORHartfordBeacon.Subdivision.ID,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow,
                BeaconRailroad = WSORHartfordBeacon,
                Moving = telemetry.Moving,
                CreatedRailroadID = WSORHartfordBeacon.Subdivision.RailroadID,
                Addresses =
                [
                    new Address
                    {
                        AddressID = telemetry.AddressID,
                        Source = telemetry.Source,
                        CreatedAt = _timeProviderMock.Object.UtcNow,
                        LastUpdate = _timeProviderMock.Object.UtcNow
                    }
                ],
            };

            var expectedMapPinBeforeInsert = new MapPin
            {
                ID = previousMapPin.ID, // Reuse previous map pin ID to avoid duplicates on map.
                BeaconID = CNRugbyJunctionBeacon.BeaconID,
                SubdivisionId = CNRugbyJunctionBeacon.Subdivision.ID,
                Direction = "S",
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow,
                BeaconRailroad = CNRugbyJunctionBeacon,
                Moving = null,
                CreatedRailroadID = WSORHartfordBeacon.Subdivision.RailroadID,
                Addresses =
                [
                    new Address
                    {
                        AddressID = telemetry.AddressID,
                        Source = telemetry.Source,
                        CreatedAt = _timeProviderMock.Object.UtcNow,
                        LastUpdate = _timeProviderMock.Object.UtcNow
                    }
                ],
            };

            var expectedMapPinAfterInsert = expectedMapPinBeforeInsert.Clone();
            expectedMapPinAfterInsert.ID = 234;

            var expectedMapPinObjects = new object[]
            {
                new MapPinDTO
                {
                    ID = expectedMapPinAfterInsert.ID,
                    Direction = "S",
                    BeaconID = telemetry.BeaconID,
                    BeaconName = CNRugbyJunctionBeacon.Beacon.Name,
                    Railroad = CNRugbyJunctionBeacon.Subdivision.Railroad.Name,
                    Subdivision = CNRugbyJunctionBeacon.Subdivision.Name,
                    SubdivisionID = CNRugbyJunctionBeacon.Subdivision.ID,
                    Latitude = CNRugbyJunctionBeacon.Latitude,
                    Longitude = CNRugbyJunctionBeacon.Longitude,
                    Milepost = CNRugbyJunctionBeacon.Milepost,
                    Moving = null,
                    CreatedAt = _timeProviderMock.Object.UtcNow,
                    LastUpdate = _timeProviderMock.Object.UtcNow,
                    Addresses =
                    [
                        new AddressDTO
                        {
                            AddressID = telemetry.AddressID,
                            Source = telemetry.Source
                        }
                    ],
                }
            };

            var subdivisionTrackageRights = new List<SubdivisionTrackageRight>
            {
                new SubdivisionTrackageRight
                {
                    ID = 1,
                    FromSubdivisionID = WSORHartfordBeacon.Subdivision.ID,
                    ToSubdivisionID = CNRugbyJunctionBeacon.Subdivision.ID
                }
            };

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID))
                .ReturnsAsync(previousMapPin);
            _mapPinRepositoryMock.Setup(r => r.GetByTimeThreshold(telemetry.BeaconID, CNRugbyJunctionBeacon.SubdivisionID, 5))
                .ReturnsAsync((MapPin?)null);
            _trackageRightRepositoryMock.Setup(r => r.GetByFromSubdivisionAsync(WSORHartfordBeacon.SubdivisionID))
                .ReturnsAsync(subdivisionTrackageRights);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(expectedMapPinBeforeInsert, telemetry.LastUpdate))
                .ReturnsAsync(expectedMapPinAfterInsert);

            _beaconRailroadServiceMock.Setup(b => b.GetByIdAsync(telemetry.BeaconID, previousMapPin.SubdivisionId))
                .ReturnsAsync(CNRugbyJunctionBeacon);
            _beaconRailroadServiceMock.Setup(b => b.GetByIdAsync(previousMapPin.BeaconID, previousMapPin.SubdivisionId))
                .ReturnsAsync(WSORHartfordBeacon); // Not the same beacon as new telemetry.

            // No need to mock rule engine - using real implementation that will evaluate actual rules

            _clientProxyMock.Setup(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate, expectedMapPinObjects, default))
                    .Returns(Task.CompletedTask);
            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(expectedMapPinBeforeInsert, telemetry.LastUpdate), Times.Once);

            _clientProxyMock?.Verify(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate,
                It.Is<object[]>(args => args[0].Equals(expectedMapPinObjects[0])),
                default), Times.Once);
        }

        /// <summary>
        /// If a train emitting HOT telemetry from a single track and later detected emitting EOT telemetry by a 
        /// multi-track beacon, multi-railroad, the existing map pin should be updated with the additional source
        /// for the HOT with a calculated direction.
        /// </summary>
        [TestMethod]
        public async Task UpsertMapPin_SingleRailroad_SameAddress_DifferentSource()
        {
            // Arrange
            var CNRugbyJunctionBeacon = TestData.CN_RugbyJunction_WI(_timeProviderMock.Object.UtcNow);
            var CNSussexBeacon = TestData.CN_Sussex_WI(_timeProviderMock.Object.UtcNow);
            var WSORRugbyJunctionBeacon = TestData.WSOR_RugbyJunction_WI(_timeProviderMock.Object.UtcNow);

            var fromDirection = null as string;
            var fromSource = SourceEnum.HOT;

            var toDirection = "N";
            var toSource = SourceEnum.EOT;

            var fromBeaconRailroads = new List<BeaconRailroad>
            {
                CNSussexBeacon
            };

            var toBeaconRailroads = new List<BeaconRailroad>
            {
                CNRugbyJunctionBeacon,
                WSORRugbyJunctionBeacon
            };

            var telemetry = new Telemetry
            {
                BeaconID = CNRugbyJunctionBeacon.BeaconID,
                Beacon = new Beacon
                {
                    ID = CNRugbyJunctionBeacon.BeaconID,
                    Name = CNRugbyJunctionBeacon.Beacon.Name,
                    BeaconRailroads = toBeaconRailroads
                },
                AddressID = 23424,
                Source = toSource,
                Moving = true,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow
            };

            var fromMapPin = new MapPin
            {
                ID = 234,
                BeaconID = CNSussexBeacon.BeaconID,
                SubdivisionId = CNSussexBeacon.Subdivision.ID,
                Direction = fromDirection,
                CreatedAt = _timeProviderMock.Object.UtcNow.AddSeconds(-5),
                LastUpdate = _timeProviderMock.Object.UtcNow.AddSeconds(-5),
                BeaconRailroad = CNSussexBeacon,
                Moving = telemetry.Moving,
                CreatedRailroadID = CNSussexBeacon.Subdivision.RailroadID,
                Addresses =
                    [
                        new Address
                        {
                            AddressID = telemetry.AddressID,
                            Source = fromSource, // Different source from new telemetry (HOT > EOT)
                            CreatedAt = _timeProviderMock.Object.UtcNow.AddSeconds(-5),
                            LastUpdate = _timeProviderMock.Object.UtcNow.AddSeconds(-5)
                        }
                    ],
            };

            var toMapPinBeforeUpdate = new MapPin
            {
                ID = fromMapPin.ID,
                BeaconID = CNRugbyJunctionBeacon.BeaconID,
                SubdivisionId = CNRugbyJunctionBeacon.Subdivision.ID,
                Direction = toDirection,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow,
                BeaconRailroad = CNRugbyJunctionBeacon,
                Moving = telemetry.Moving,
                CreatedRailroadID = CNSussexBeacon.Subdivision.RailroadID,
                Addresses =
                [
                    new Address
                    {
                        AddressID = telemetry.AddressID,
                        Source = fromSource,
                        CreatedAt = _timeProviderMock.Object.UtcNow,
                        LastUpdate = _timeProviderMock.Object.UtcNow
                    },
                    new Address
                    {
                        AddressID = telemetry.AddressID,
                        Source = toSource,
                        CreatedAt = _timeProviderMock.Object.UtcNow,
                        LastUpdate = _timeProviderMock.Object.UtcNow
                    }
                ],
            };

            var toMapPinAfterUpdate = toMapPinBeforeUpdate.Clone();
            toMapPinAfterUpdate.ID = 345; // New ID after insert.

            var toMapPinObjects = new object[]
            {
                new MapPinDTO
                {
                    ID = toMapPinAfterUpdate.ID,
                    Direction = toDirection,
                    BeaconID = telemetry.BeaconID,
                    BeaconName = CNRugbyJunctionBeacon.Beacon.Name,
                    Railroad = CNRugbyJunctionBeacon.Subdivision.Railroad.Name,
                    Subdivision = CNRugbyJunctionBeacon.Subdivision.Name,
                    SubdivisionID = CNRugbyJunctionBeacon.Subdivision.ID,
                    Latitude = CNRugbyJunctionBeacon.Latitude,
                    Longitude = CNRugbyJunctionBeacon.Longitude,
                    Milepost = CNRugbyJunctionBeacon.Milepost,
                    Moving = telemetry.Moving,
                    CreatedAt = _timeProviderMock.Object.UtcNow,
                    LastUpdate = _timeProviderMock.Object.UtcNow,
                    Addresses =
                    [
                        new AddressDTO
                        {
                            AddressID = telemetry.AddressID,
                            Source = fromSource
                        },
                        new AddressDTO
                        {
                            AddressID = telemetry.AddressID,
                            Source = toSource
                        }
                    ],
                }
            };

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID))
                .ReturnsAsync(fromMapPin); // Simulate previous map pin exists.
            _mapPinRepositoryMock.Setup(r => r.GetByTimeThreshold(telemetry.BeaconID, CNSussexBeacon.SubdivisionID, 5))
                .ReturnsAsync((MapPin?)null);
            _beaconRailroadServiceMock.Setup(s => s.GetByIdAsync(telemetry.BeaconID, fromMapPin.SubdivisionId))
                .ReturnsAsync(toBeaconRailroads[0]);
            _beaconRailroadServiceMock.Setup(s => s.GetByIdAsync(fromMapPin.BeaconID, fromMapPin.SubdivisionId))
                .ReturnsAsync(fromBeaconRailroads[0]);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(It.IsAny<MapPin>(), It.IsAny<DateTime>()))
                .ReturnsAsync(toMapPinAfterUpdate);

            _clientProxyMock.Setup(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate, It.IsAny<object[]>(), default))
                    .Returns(Task.CompletedTask);
            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert - only EOT should be added (no automatic HOT pairing)
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(It.Is<MapPin>(mp =>
                mp.Addresses.Count == 2 &&
                mp.Addresses.Any(a => a.Source == fromSource && a.AddressID == telemetry.AddressID) &&
                mp.Addresses.Any(a => a.Source == toSource && a.AddressID == telemetry.AddressID)
            ), telemetry.LastUpdate), Times.Once);

            _clientProxyMock?.Verify(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate,
                It.Is<object[]>(args =>
                    args.Length > 0 &&
                    args[0] != null &&
                    ((MapPinDTO)args[0]).Addresses.Count == 2 &&
                    ((MapPinDTO)args[0]).Addresses.Any(a => a.Source == fromSource) &&
                    ((MapPinDTO)args[0]).Addresses.Any(a => a.Source == toSource)
                ),
                default), Times.Once);
        }

        /// <summary>
        /// If a train emitting HOT telemetry from a single track and later detected emitting HOT telemetry by a 
        /// multi-track beacon, multi-railroad, the existing map pin last update should be updated.
        /// </summary>
        [TestMethod]
        public async Task UpsertMapPin_SingleRailroad_SameAddress_SameSource()
        {
            // Arrange
            var CNRugbyJunctionBeacon = TestData.CN_RugbyJunction_WI(_timeProviderMock.Object.UtcNow);
            var WSORRugbyJunctionBeacon = TestData.WSOR_RugbyJunction_WI(_timeProviderMock.Object.UtcNow);
            var CNSussexBeacon = TestData.CN_Sussex_WI(_timeProviderMock.Object.UtcNow);

            var toDirection = "N";

            var fromBeaconRailroads = new List<BeaconRailroad>
            {
                CNSussexBeacon
            };

            var toBeaconRailroads = new List<BeaconRailroad>
            {
                CNRugbyJunctionBeacon,
                WSORRugbyJunctionBeacon
            };

            var telemetry = new Telemetry
            {
                BeaconID = CNRugbyJunctionBeacon.BeaconID,
                Beacon = new Beacon
                {
                    ID = CNRugbyJunctionBeacon.BeaconID,
                    Name = CNRugbyJunctionBeacon.Beacon.Name,
                    BeaconRailroads = toBeaconRailroads
                },
                AddressID = 23424,
                Source = SourceEnum.HOT,
                Moving = true,
                CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(1),
                LastUpdate = _timeProviderMock.Object.UtcNow.AddMinutes(2)
            };

            var fromMapPin = new MapPin
            {
                ID = 234,
                BeaconID = CNSussexBeacon.BeaconID,
                SubdivisionId = CNSussexBeacon.Subdivision.ID,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow,
                BeaconRailroad = CNSussexBeacon,
                Moving = telemetry.Moving,
                CreatedRailroadID = CNSussexBeacon.Subdivision.RailroadID,
                Addresses =
                [
                    new Address
                    {
                        AddressID = telemetry.AddressID,
                        Source = telemetry.Source, // Same source as new telemetry (HOT > HOT)
                        CreatedAt = _timeProviderMock.Object.UtcNow,
                        LastUpdate = _timeProviderMock.Object.UtcNow
                    }
                ],
            };

            var toMapPinBeforeUpdate = new MapPin
            {
                ID = fromMapPin.ID,
                BeaconID = CNRugbyJunctionBeacon.BeaconID,
                SubdivisionId = CNRugbyJunctionBeacon.Subdivision.ID,
                Direction = toDirection,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow,
                BeaconRailroad = CNRugbyJunctionBeacon,
                Moving = null,
                CreatedRailroadID = CNSussexBeacon.Subdivision.RailroadID,
                Addresses =
                [
                    new Address
                    {
                        AddressID = telemetry.AddressID,
                        Source = telemetry.Source,
                        CreatedAt = _timeProviderMock.Object.UtcNow,
                        LastUpdate = _timeProviderMock.Object.UtcNow
                    }
                ],
            };

            var toMapPinAfterUpdate = toMapPinBeforeUpdate;

            var expectedMapPinObjects = new object[]
            {
                new MapPinDTO
                {
                    ID = toMapPinAfterUpdate.ID,
                    Direction = toDirection,
                    BeaconID = telemetry.BeaconID,
                    BeaconName = CNRugbyJunctionBeacon.Beacon.Name,
                    Railroad = CNRugbyJunctionBeacon.Subdivision.Railroad.Name,
                    Subdivision = CNRugbyJunctionBeacon.Subdivision.Name,
                    SubdivisionID = CNRugbyJunctionBeacon.Subdivision.ID,
                    Latitude = CNRugbyJunctionBeacon.Latitude,
                    Longitude = CNRugbyJunctionBeacon.Longitude,
                    Milepost = CNRugbyJunctionBeacon.Milepost,
                    Moving = null,
                    CreatedAt = _timeProviderMock.Object.UtcNow,
                    LastUpdate = _timeProviderMock.Object.UtcNow,
                    Addresses =
                    [
                        new AddressDTO
                        {
                            AddressID = telemetry.AddressID,
                            Source = telemetry.Source
                        }
                    ],
                }
            };

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID))
                .ReturnsAsync(fromMapPin); // Simulate previous map pin exists.
            _mapPinRepositoryMock.Setup(r => r.GetByTimeThreshold(telemetry.BeaconID, CNSussexBeacon.SubdivisionID, 5))
                .ReturnsAsync((MapPin?)null);
            _beaconRailroadServiceMock.Setup(s => s.GetByIdAsync(telemetry.BeaconID, fromMapPin.SubdivisionId))
                .ReturnsAsync(toBeaconRailroads[0]);
            _beaconRailroadServiceMock.Setup(s => s.GetByIdAsync(fromMapPin.BeaconID, fromMapPin.SubdivisionId))
                .ReturnsAsync(fromBeaconRailroads[0]);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(toMapPinBeforeUpdate, telemetry.LastUpdate))
                .ReturnsAsync(toMapPinAfterUpdate);

            _clientProxyMock.Setup(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate, expectedMapPinObjects, default))
                    .Returns(Task.CompletedTask);
            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(toMapPinBeforeUpdate, telemetry.LastUpdate), Times.Once);

            _clientProxyMock?.Verify(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate,
                It.Is<object[]>(args => args[0].Equals(expectedMapPinObjects[0])),
                default), Times.Once);
        }

        [TestMethod]
        public async Task UpsertMapPin_MultipleToSingleRailroad_DifferentRailroad()
        {
            // Arrange
            var CNRugbyJunctionBeacon = TestData.CN_RugbyJunction_WI(_timeProviderMock.Object.UtcNow);
            var WSORRugbyJunctionBeacon = TestData.WSOR_RugbyJunction_WI(_timeProviderMock.Object.UtcNow);
            var CNSussexBeacon = TestData.CN_Sussex_WI(_timeProviderMock.Object.UtcNow);
            var UPSussexBeacon = TestData.UP_Sussex_WI(_timeProviderMock.Object.UtcNow);

            var toDirection = "S";

            var fromBeaconRailroads = new List<BeaconRailroad>
            {
                CNRugbyJunctionBeacon,
                WSORRugbyJunctionBeacon
            };

            var toBeaconRailroads = new List<BeaconRailroad>
            {
                CNSussexBeacon,
                UPSussexBeacon
            };

            var telemetry = new Telemetry
            {
                BeaconID = CNSussexBeacon.BeaconID,
                Beacon = new Beacon
                {
                    ID = CNSussexBeacon.BeaconID,
                    Name = CNSussexBeacon.Beacon.Name,
                    BeaconRailroads = toBeaconRailroads
                },
                AddressID = 23424,
                Source = SourceEnum.HOT,
                Moving = true,
                CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(2),
                LastUpdate = _timeProviderMock.Object.UtcNow.AddMinutes(2)
            };

            var fromMapPin = new MapPin
            {
                ID = 234,
                BeaconID = WSORRugbyJunctionBeacon.BeaconID,
                SubdivisionId = WSORRugbyJunctionBeacon.Subdivision.ID,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow,
                BeaconRailroad = WSORRugbyJunctionBeacon,
                Moving = telemetry.Moving,
                CreatedRailroadID = WSORRugbyJunctionBeacon.Subdivision.RailroadID,
                Addresses =
                [
                    new Address
                    {
                        AddressID = telemetry.AddressID,
                        Source = telemetry.Source,
                        CreatedAt = _timeProviderMock.Object.UtcNow,
                        LastUpdate = _timeProviderMock.Object.UtcNow
                    }
                ],
            };

            var toMapPinBeforeUpdate = new MapPin
            {
                ID = fromMapPin.ID,
                BeaconID = CNSussexBeacon.BeaconID,
                SubdivisionId = CNSussexBeacon.Subdivision.ID,
                Direction = toDirection,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow,
                BeaconRailroad = CNSussexBeacon,
                Moving = null,
                CreatedRailroadID = WSORRugbyJunctionBeacon.Subdivision.RailroadID,
                Addresses =
                [
                    new Address
                    {
                        AddressID = telemetry.AddressID,
                        Source = telemetry.Source,
                        CreatedAt = _timeProviderMock.Object.UtcNow,
                        LastUpdate = _timeProviderMock.Object.UtcNow
                    }
                ],
            };

            var toMapPinAfterUpdate = toMapPinBeforeUpdate.Clone();
            toMapPinAfterUpdate.LastUpdate = telemetry.LastUpdate;
            toMapPinAfterUpdate.Addresses.ToList()[0].LastUpdate = telemetry.LastUpdate;

            var expectedMapPinObjects = new object[]
            {
                new MapPinDTO
                {
                    ID = toMapPinAfterUpdate.ID,
                    Direction = toDirection,
                    BeaconID = telemetry.BeaconID,
                    BeaconName = CNSussexBeacon.Beacon.Name,
                    Railroad = CNSussexBeacon.Subdivision.Railroad.Name,
                    Subdivision = CNSussexBeacon.Subdivision.Name,
                    SubdivisionID = CNSussexBeacon.Subdivision.ID,
                    Latitude = CNSussexBeacon.Latitude,
                    Longitude = CNSussexBeacon.Longitude,
                    Milepost = CNSussexBeacon.Milepost,
                    Moving = null,
                    CreatedAt = _timeProviderMock.Object.UtcNow,
                    LastUpdate = telemetry.LastUpdate,
                    Addresses =
                    [
                        new AddressDTO
                        {
                            AddressID = telemetry.AddressID,
                            Source = telemetry.Source
                        }
                    ],
                }
            };

            var subdivisionTrackageRights = new List<SubdivisionTrackageRight>
            {
                new SubdivisionTrackageRight
                {
                    ID = 1,
                    FromSubdivisionID = WSORRugbyJunctionBeacon.Subdivision.ID,
                    ToSubdivisionID = CNSussexBeacon.Subdivision.ID
                }
            };

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID))
                .ReturnsAsync(fromMapPin);
            _beaconRailroadServiceMock.Setup(s => s.GetByIdAsync(fromMapPin.BeaconID, fromMapPin.SubdivisionId))
                .ReturnsAsync(fromBeaconRailroads[1]); // WSOR is second in array.
            _trackageRightRepositoryMock.Setup(r => r.GetByFromSubdivisionAsync(WSORRugbyJunctionBeacon.SubdivisionID))
                .ReturnsAsync(subdivisionTrackageRights);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(toMapPinBeforeUpdate, telemetry.LastUpdate))
                .ReturnsAsync(toMapPinAfterUpdate);

            _clientProxyMock.Setup(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate, expectedMapPinObjects, default))
                    .Returns(Task.CompletedTask);
            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(toMapPinBeforeUpdate, telemetry.LastUpdate), Times.Once);

            _clientProxyMock?.Verify(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate,
                It.Is<object[]>(args => args[0].Equals(expectedMapPinObjects[0])),
                default), Times.Once);
        }

        [TestMethod]
        public async Task UpsertMapPin_SingleRailroadDifferentSubdivision_SameAddress_SameSource()
        {
            // Arrange
            var CNOshkoshBeaconRailroad = TestData.CN_Oshkosh_WI(_timeProviderMock.Object.UtcNow);
            var CNRugbyJunctionBeaconRailroad = TestData.CN_RugbyJunction_WI(_timeProviderMock.Object.UtcNow);
            var WSORRugbyJunctionBeaconRailroad = TestData.WSOR_RugbyJunction_WI(_timeProviderMock.Object.UtcNow);

            var toDirection = "S";

            var fromBeaconRailroads = new List<BeaconRailroad>
            {
                CNOshkoshBeaconRailroad
            };

            var toBeaconRailroads = new List<BeaconRailroad>
            {
                CNRugbyJunctionBeaconRailroad,
                WSORRugbyJunctionBeaconRailroad
            };

            var telemetry = new Telemetry
            {
                BeaconID = CNRugbyJunctionBeaconRailroad.BeaconID,
                Beacon = new Beacon
                {
                    ID = CNRugbyJunctionBeaconRailroad.BeaconID,
                    Name = CNRugbyJunctionBeaconRailroad.Beacon.Name,
                    BeaconRailroads = toBeaconRailroads
                },
                AddressID = 23424,
                Source = SourceEnum.HOT,
                Moving = true,
                CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(1),
                LastUpdate = _timeProviderMock.Object.UtcNow.AddMinutes(2)
            };

            var fromMapPin = new MapPin
            {
                ID = 234,
                BeaconID = CNOshkoshBeaconRailroad.BeaconID,
                SubdivisionId = CNOshkoshBeaconRailroad.Subdivision.ID,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow,
                BeaconRailroad = CNOshkoshBeaconRailroad,
                Moving = telemetry.Moving,
                CreatedRailroadID = CNOshkoshBeaconRailroad.Subdivision.RailroadID,
                Addresses =
                [
                    new Address
                    {
                        AddressID = telemetry.AddressID,
                        Source = telemetry.Source, // Same source as new telemetry (HOT > HOT)
                        CreatedAt = _timeProviderMock.Object.UtcNow,
                        LastUpdate = _timeProviderMock.Object.UtcNow
                    }
                ],
            };

            var toMapPinBeforeUpdate = new MapPin
            {
                ID = fromMapPin.ID,
                BeaconID = CNRugbyJunctionBeaconRailroad.BeaconID,
                SubdivisionId = CNRugbyJunctionBeaconRailroad.Subdivision.ID,
                Direction = toDirection,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow,
                BeaconRailroad = CNRugbyJunctionBeaconRailroad,
                Moving = null,
                CreatedRailroadID = CNOshkoshBeaconRailroad.Subdivision.RailroadID,
                Addresses =
                [
                    new Address
                    {
                        AddressID = telemetry.AddressID,
                        Source = telemetry.Source,
                        CreatedAt = _timeProviderMock.Object.UtcNow,
                        LastUpdate = _timeProviderMock.Object.UtcNow
                    }
                ],
            };

            var toMapPinAfterUpdate = toMapPinBeforeUpdate;

            var expectedMapPinObjects = new object[]
            {
                new MapPinDTO
                {
                    ID = toMapPinAfterUpdate.ID,
                    Direction = toDirection,
                    BeaconID = telemetry.BeaconID,
                    BeaconName = CNRugbyJunctionBeaconRailroad.Beacon.Name,
                    Railroad = CNRugbyJunctionBeaconRailroad.Subdivision.Railroad.Name,
                    Subdivision = CNRugbyJunctionBeaconRailroad.Subdivision.Name,
                    SubdivisionID = CNRugbyJunctionBeaconRailroad.Subdivision.ID,
                    Latitude = CNRugbyJunctionBeaconRailroad.Latitude,
                    Longitude = CNRugbyJunctionBeaconRailroad.Longitude,
                    Milepost = CNRugbyJunctionBeaconRailroad.Milepost,
                    Moving = null,
                    CreatedAt = _timeProviderMock.Object.UtcNow,
                    LastUpdate = _timeProviderMock.Object.UtcNow,
                    Addresses =
                    [
                        new AddressDTO
                        {
                            AddressID = telemetry.AddressID,
                            Source = telemetry.Source
                        }
                    ],
                }
            };

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID))
                .ReturnsAsync(fromMapPin); // Simulate previous map pin exists.
            _mapPinRepositoryMock.Setup(r => r.GetByTimeThreshold(telemetry.BeaconID, CNOshkoshBeaconRailroad.SubdivisionID, 5))
                .ReturnsAsync((MapPin?)null);
            _beaconRailroadServiceMock.Setup(s => s.GetByIdAsync(telemetry.BeaconID, fromMapPin.SubdivisionId))
                .ReturnsAsync(toBeaconRailroads[0]);
            _beaconRailroadServiceMock.Setup(s => s.GetByIdAsync(fromMapPin.BeaconID, fromMapPin.SubdivisionId))
                .ReturnsAsync(fromBeaconRailroads[0]);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(toMapPinBeforeUpdate, telemetry.LastUpdate))
                .ReturnsAsync(toMapPinAfterUpdate);

            _clientProxyMock.Setup(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate, expectedMapPinObjects, default))
                    .Returns(Task.CompletedTask);
            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(toMapPinBeforeUpdate, telemetry.LastUpdate), Times.Once);

            _clientProxyMock?.Verify(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate,
                It.Is<object[]>(args => args[0].Equals(expectedMapPinObjects[0])),
                default), Times.Once);
        }

        /// <summary>
        /// Tests ensures that a DPU telemetry that has no previous matching map pin at a multi-track beacon
        /// creates a new map pin with no direction.
        /// </summary>
        [TestMethod]
        public async Task UpsertMapPin_CreateNewPin_SingleRailroad_MultiTrack()
        {
            // Arrange
            var CNSussexBeacon = TestData.CN_Sussex_WI(_timeProviderMock.Object.UtcNow);
            CNSussexBeacon.MultipleTracks = true; // Enable multi-track for this test.

            var toSource = SourceEnum.DPU;
            var toAddressID = 23424;
            var toTrainID = 123;

            var beaconRailroads = new List<BeaconRailroad>
            {
                CNSussexBeacon
            };

            var telemetry = new Telemetry
            {
                BeaconID = CNSussexBeacon.BeaconID,
                Beacon = new Beacon
                {
                    ID = CNSussexBeacon.BeaconID,
                    Name = CNSussexBeacon.Beacon.Name,
                    BeaconRailroads = beaconRailroads
                },
                AddressID = toAddressID,
                Source = toSource,
                TrainID = toTrainID,
                Moving = true,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow
            };

            var expectedMapPinBeforeUpdate = new MapPin
            {
                ID = 0, // New map pin won't have an yet ID
                BeaconID = CNSussexBeacon.BeaconID,
                SubdivisionId = CNSussexBeacon.Subdivision.ID,
                CreatedRailroadID = CNSussexBeacon.Subdivision.RailroadID,
                Direction = null, // Can't calculate direction
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow,
                BeaconRailroad = CNSussexBeacon,
                Moving = telemetry.Moving,
                Addresses =
                [
                    new Address
                    {
                        AddressID = toAddressID,
                        DpuTrainID = toTrainID,
                        Source = toSource,
                        CreatedAt = _timeProviderMock.Object.UtcNow,
                        LastUpdate = _timeProviderMock.Object.UtcNow
                    }
                ],
            };

            var expectedMapPinAfterUpdate = expectedMapPinBeforeUpdate.Clone();
            expectedMapPinAfterUpdate.ID = 234; // ID returned after insert.

            var expectedMapPinObjects = new object[]
            {
                new MapPinDTO
                {
                    ID = 234,
                    Direction = null,
                    BeaconID = telemetry.BeaconID,
                    BeaconName = CNSussexBeacon.Beacon.Name,
                    Railroad = CNSussexBeacon.Subdivision.Railroad.Name,
                    Subdivision = CNSussexBeacon.Subdivision.Name,
                    SubdivisionID = CNSussexBeacon.Subdivision.ID,
                    Latitude = CNSussexBeacon.Latitude,
                    Longitude = CNSussexBeacon.Longitude,
                    Milepost = CNSussexBeacon.Milepost,
                    Moving = telemetry.Moving,
                    CreatedAt = _timeProviderMock.Object.UtcNow,
                    LastUpdate = _timeProviderMock.Object.UtcNow,
                    Addresses =
                    [
                        new AddressDTO
                        {
                            AddressID = toAddressID,
                            Source = toSource
                        }
                    ],
                }
            };

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID))
                .ReturnsAsync((MapPin?)null);
            _mapPinRepositoryMock.Setup(r => r.GetByTrainIdAsync(telemetry.TrainID.Value, MapPinService.TIME_THRESHOLD_DPU_MINUTES))
                .ReturnsAsync((MapPin?)null);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(expectedMapPinBeforeUpdate, telemetry.LastUpdate))
                .ReturnsAsync(expectedMapPinAfterUpdate);

            _clientProxyMock.Setup(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate, expectedMapPinObjects, default))
                    .Returns(Task.CompletedTask);
            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(expectedMapPinBeforeUpdate, telemetry.LastUpdate), Times.Once);

            _clientProxyMock?.Verify(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate,
                It.Is<object[]>(args => args[0].Equals(expectedMapPinObjects[0])),
                default), Times.Once);
        }

        /// <summary>
        /// Summary:
        /// If a train emitting HOT telemetry from a single track and later detected emitting DPU telemetry by a 
        /// multi-track beacon, a new map pin should be created for the DPU address with no direction.
        /// 
        /// Due to the multi-track nature of the beacon, there is no way to determine which train the DPU is on
        /// and therefore no direction can be calculated.
        /// 
        /// If:
        /// - Both beacons are on the same railroad
        /// - Railroad is DPU capable 
        /// - From beacon is single track
        /// - To beacon railroad is multi track
        /// - To beacon detects telemetry from DPU source
        /// - From beacon map pin exists within time threshold
        /// Then:
        /// - Create new DPU map pin for to beacon with no direction
        /// </summary>
        [TestMethod]
        public async Task UpsertMapPin_SingleRailroad_SingleToMultiTrack_DifferentAddress_DpuSource()
        {
            // Arrange
            var CNSussexBeacon = TestData.CN_Sussex_WI(_timeProviderMock.Object.UtcNow);
            var fromDirection = null as String;
            var fromAddressID = 92342;
            var fromSource = SourceEnum.HOT;

            var CNRugbyJunctionBeacon = TestData.CN_RugbyJunction_WI(_timeProviderMock.Object.UtcNow);
            var toDirection = null as String;
            var toAddressID = 23424;
            var toTrainID = 123;
            var toSource = SourceEnum.DPU;

            var beaconRailroads = new List<BeaconRailroad>
            {
                CNRugbyJunctionBeacon,
                CNSussexBeacon
            };

            var telemetry = new Telemetry
            {
                BeaconID = CNRugbyJunctionBeacon.BeaconID,
                Beacon = new Beacon
                {
                    ID = CNRugbyJunctionBeacon.BeaconID,
                    Name = CNRugbyJunctionBeacon.Beacon.Name,
                    BeaconRailroads = beaconRailroads
                },
                AddressID = toAddressID,
                TrainID = toTrainID,
                Source = toSource,
                Moving = true,
                CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(2),
                LastUpdate = _timeProviderMock.Object.UtcNow.AddMinutes(2),
            };

            var fromMapPin = new MapPin
            {
                ID = 234,
                BeaconID = CNSussexBeacon.BeaconID,
                SubdivisionId = CNSussexBeacon.Subdivision.ID,
                Direction = fromDirection,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow,
                BeaconRailroad = CNSussexBeacon,
                Moving = telemetry.Moving,
                Addresses =
                    [
                        new Address
                        {
                            AddressID = fromAddressID,
                            Source = fromSource,
                            CreatedAt = _timeProviderMock.Object.UtcNow,
                            LastUpdate = _timeProviderMock.Object.UtcNow
                        }
                    ],
            };

            var toMapPinBeforeUpdate = new MapPin
            {
                ID = 0, // New map pin won't have an yet ID
                BeaconID = CNRugbyJunctionBeacon.BeaconID,
                SubdivisionId = CNRugbyJunctionBeacon.Subdivision.ID,
                CreatedRailroadID = CNRugbyJunctionBeacon.Subdivision.RailroadID,
                Direction = null, // Can't calculate direction for DPU
                CreatedAt = telemetry.CreatedAt,
                LastUpdate = telemetry.LastUpdate,
                BeaconRailroad = CNRugbyJunctionBeacon,
                Moving = telemetry.Moving,
                Addresses =
                [
                    new Address
                    {
                        AddressID = toAddressID,
                        DpuTrainID = toTrainID,
                        Source = toSource,
                        CreatedAt = telemetry.CreatedAt,
                        LastUpdate = telemetry.LastUpdate
                    }
                ],
            };

            var toMapPinAfterUpdate = toMapPinBeforeUpdate.Clone();
            toMapPinAfterUpdate.ID = 936; // ID returned after insert.

            var toMapPinObjects = new object[]
            {
                new MapPinDTO
                {
                    ID = toMapPinAfterUpdate.ID,
                    Direction = toDirection,
                    BeaconID = telemetry.BeaconID,
                    BeaconName = CNRugbyJunctionBeacon.Beacon.Name,
                    Railroad = CNRugbyJunctionBeacon.Subdivision.Railroad.Name,
                    Subdivision = CNRugbyJunctionBeacon.Subdivision.Name,
                    SubdivisionID = CNRugbyJunctionBeacon.Subdivision.ID,
                    Latitude = CNRugbyJunctionBeacon.Latitude,
                    Longitude = CNRugbyJunctionBeacon.Longitude,
                    Milepost = CNRugbyJunctionBeacon.Milepost,
                    Moving = telemetry.Moving,
                    CreatedAt = telemetry.CreatedAt,
                    LastUpdate = telemetry.LastUpdate,
                    Addresses =
                    [
                        new AddressDTO
                        {
                            AddressID = toAddressID,
                            Source = toSource
                        }
                    ],
                }
             };

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID))
                .ReturnsAsync((MapPin?)null);
            _mapPinRepositoryMock.Setup(r => r.GetByTimeThreshold(telemetry.BeaconID, CNSussexBeacon.Subdivision.RailroadID, 5))
                .ReturnsAsync(fromMapPin);
            _beaconRailroadServiceMock.Setup(s => s.GetByIdAsync(fromMapPin.BeaconID, fromMapPin.SubdivisionId))
                .ReturnsAsync(beaconRailroads[0]);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(toMapPinBeforeUpdate, telemetry.LastUpdate))
                .ReturnsAsync(toMapPinAfterUpdate);

            _clientProxyMock.Setup(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate, toMapPinObjects, default))
                    .Returns(Task.CompletedTask);
            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(toMapPinBeforeUpdate, telemetry.LastUpdate), Times.Once);

            _clientProxyMock?.Verify(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate,
                It.Is<object[]>(args => args[0].Equals(toMapPinObjects[0])),
                default), Times.Once);
        }

        [TestMethod]
        public async Task UpsertMapPin_UpdateMapPin_SingleDpuCapableRailroad_TimeThreshold_PreviousHOTandEOT()
        {
            // Arrange
            var CNRugbyJunctionBeacon = TestData.CN_RugbyJunction_WI(_timeProviderMock.Object.UtcNow);
            var CNSussexBeacon = TestData.CN_Sussex_WI(_timeProviderMock.Object.UtcNow);
            var WSORRugbyJunctionBeacon = TestData.WSOR_RugbyJunction_WI(_timeProviderMock.Object.UtcNow);

            var calculatedDirection = "N";

            var toDpuSource = SourceEnum.DPU;
            var toAddressID = 46969;
            var toTrainID = 123;

            var fromHotSource1 = SourceEnum.HOT;
            var fromEotSource2 = SourceEnum.EOT;
            var fromAddressID1 = 32591;
            var fromAddressID2 = 32591;

            var beaconRailroads = new List<BeaconRailroad>
            {
                CNRugbyJunctionBeacon,
                WSORRugbyJunctionBeacon
            };

            var telemetry = new Telemetry
            {
                BeaconID = CNRugbyJunctionBeacon.BeaconID,
                Beacon = new Beacon
                {
                    ID = CNRugbyJunctionBeacon.BeaconID,
                    Name = CNRugbyJunctionBeacon.Beacon.Name,
                    BeaconRailroads = beaconRailroads
                },
                AddressID = toAddressID,
                TrainID = toTrainID,
                Source = toDpuSource,
                Moving = true,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow
            };

            var previousMapPin = new MapPin
            {
                ID = 0,
                BeaconID = CNSussexBeacon.BeaconID,
                SubdivisionId = CNSussexBeacon.Subdivision.ID,
                Direction = calculatedDirection,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow,
                BeaconRailroad = CNSussexBeacon,
                Moving = null,
                Addresses =
                    [
                        new Address
                        {
                            AddressID = fromAddressID1,
                            Source = fromHotSource1,
                            CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(-2),
                            LastUpdate = _timeProviderMock.Object.UtcNow.AddMinutes(-2)
                        },
                        new Address
                        {
                            AddressID = fromAddressID2,
                            Source = fromEotSource2,
                            CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(-1),
                            LastUpdate = _timeProviderMock.Object.UtcNow.AddMinutes(-1)
                        }
                    ],
            };

            var expectedMapPinBeforeInsert = new MapPin
            {
                ID = 0, // ID will be set by InsertAsync.
                BeaconID = CNRugbyJunctionBeacon.BeaconID,
                SubdivisionId = CNRugbyJunctionBeacon.Subdivision.ID,
                CreatedRailroadID = CNRugbyJunctionBeacon.Subdivision.RailroadID,
                Direction = null, // Can't predict direction because DPU group logic doesn't exist yet.
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow,
                BeaconRailroad = CNRugbyJunctionBeacon,
                Moving = telemetry.Moving,
                Addresses =
                [
                    new Address
                    {
                        AddressID = toAddressID,
                        DpuTrainID = toTrainID,
                        Source = toDpuSource,
                        CreatedAt = _timeProviderMock.Object.UtcNow,
                        LastUpdate = _timeProviderMock.Object.UtcNow
                    }
                ],
            };

            var expectedMapPinAfterInsert = expectedMapPinBeforeInsert;

            var expectedMapPinObjects = new object[]
            {
                new MapPinDTO
                {
                    ID = expectedMapPinBeforeInsert.ID,
                    Direction = null, // Can't predict direction because DPU group logic doesn't exist yet.
                    BeaconID = telemetry.BeaconID,
                    BeaconName = CNRugbyJunctionBeacon.Beacon.Name,
                    Railroad = CNRugbyJunctionBeacon.Subdivision.Railroad.Name,
                    Subdivision = CNRugbyJunctionBeacon.Subdivision.Name,
                    SubdivisionID = CNRugbyJunctionBeacon.Subdivision.ID,
                    Latitude = CNRugbyJunctionBeacon.Latitude,
                    Longitude = CNRugbyJunctionBeacon.Longitude,
                    Milepost = CNRugbyJunctionBeacon.Milepost,
                    Moving = telemetry.Moving,
                    CreatedAt = _timeProviderMock.Object.UtcNow,
                    LastUpdate = _timeProviderMock.Object.UtcNow,
                    Addresses =
                    [
                        new AddressDTO
                        {
                            AddressID = toAddressID,
                            Source = toDpuSource
                        },
                    ],
                }
            };

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID))
                .ReturnsAsync((MapPin?)null);
            _mapPinRepositoryMock.Setup(r => r.GetByTimeThreshold(telemetry.BeaconID, CNRugbyJunctionBeacon.SubdivisionID, 5))
                .ReturnsAsync(previousMapPin);
            _beaconRailroadServiceMock.Setup(s => s.GetByIdAsync(previousMapPin.BeaconID, previousMapPin.SubdivisionId))
                .ReturnsAsync(beaconRailroads[0]);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(expectedMapPinBeforeInsert, telemetry.LastUpdate))
                .ReturnsAsync(expectedMapPinAfterInsert);

            _clientProxyMock.Setup(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate, expectedMapPinObjects, default))
                    .Returns(Task.CompletedTask);
            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(expectedMapPinBeforeInsert, telemetry.LastUpdate), Times.Once);

            _clientProxyMock?.Verify(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate,
                It.Is<object[]>(args => args[0].Equals(expectedMapPinObjects[0])),
                default), Times.Once);
        }

        [TestMethod]
        public async Task UpsertMapPin_UpdateMapPin_SingleDpuCapableRailroad_TimeThreshold_PreviousDPU()
        {
            // Arrange
            var CNRugbyJunctionBeacon = TestData.CN_RugbyJunction_WI(_timeProviderMock.Object.UtcNow);
            var CNSussexBeacon = TestData.CN_Sussex_WI(_timeProviderMock.Object.UtcNow);
            var WSORRugbyJunctionBeacon = TestData.WSOR_RugbyJunction_WI(_timeProviderMock.Object.UtcNow);

            var calculatedDirection = "N";

            var newDpuSource = SourceEnum.HOT;
            var previousHotSource1 = SourceEnum.DPU;

            var newAddressID = 46969;
            var previousAddressID1 = 32591;

            var beaconRailroads = new List<BeaconRailroad>
            {
                CNRugbyJunctionBeacon,
                WSORRugbyJunctionBeacon
            };

            var telemetry = new Telemetry
            {
                BeaconID = CNRugbyJunctionBeacon.BeaconID,
                Beacon = new Beacon
                {
                    ID = CNRugbyJunctionBeacon.BeaconID,
                    Name = CNRugbyJunctionBeacon.Beacon.Name,
                    BeaconRailroads = beaconRailroads
                },
                AddressID = newAddressID,
                Source = newDpuSource,
                Moving = true,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow
            };

            var previousMapPin = new MapPin
            {
                ID = 0,
                BeaconID = CNSussexBeacon.BeaconID,
                SubdivisionId = CNSussexBeacon.Subdivision.ID,
                Direction = calculatedDirection,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow,
                BeaconRailroad = CNSussexBeacon,
                Moving = null,
                Addresses =
                    [
                        new Address
                        {
                            AddressID = previousAddressID1,
                            Source = previousHotSource1,
                            CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(-2),
                            LastUpdate = _timeProviderMock.Object.UtcNow.AddMinutes(-2)
                        }
                    ],
            };

            var expectedMapPinBeforeInsert = new MapPin
            {
                ID = 0, // ID will be set by InsertAsync.
                BeaconID = CNRugbyJunctionBeacon.BeaconID,
                SubdivisionId = CNRugbyJunctionBeacon.Subdivision.ID,
                CreatedRailroadID = CNRugbyJunctionBeacon.Subdivision.RailroadID,
                Direction = null, // Can't predict direction because DPU group logic doesn't exist yet.
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow,
                BeaconRailroad = CNRugbyJunctionBeacon,
                Moving = telemetry.Moving,
                Addresses =
                [
                    new Address
                    {
                        AddressID = newAddressID,
                        Source = newDpuSource,
                        CreatedAt = _timeProviderMock.Object.UtcNow,
                        LastUpdate = _timeProviderMock.Object.UtcNow
                    }
                ],
            };

            var expectedMapPinAfterInsert = expectedMapPinBeforeInsert;

            var expectedMapPinObjects = new object[]
            {
                new MapPinDTO
                {
                    ID = expectedMapPinBeforeInsert.ID,
                    Direction = null, // Can't predict direction because DPU group logic doesn't exist yet.
                    BeaconID = telemetry.BeaconID,
                    BeaconName = CNRugbyJunctionBeacon.Beacon.Name,
                    Railroad = CNRugbyJunctionBeacon.Subdivision.Railroad.Name,
                    Subdivision = CNRugbyJunctionBeacon.Subdivision.Name,
                    SubdivisionID = CNRugbyJunctionBeacon.Subdivision.ID,
                    Latitude = CNRugbyJunctionBeacon.Latitude,
                    Longitude = CNRugbyJunctionBeacon.Longitude,
                    Milepost = CNRugbyJunctionBeacon.Milepost,
                    Moving = telemetry.Moving,
                    CreatedAt = _timeProviderMock.Object.UtcNow,
                    LastUpdate = _timeProviderMock.Object.UtcNow,
                    Addresses =
                    [
                        new AddressDTO
                        {
                            AddressID = newAddressID,
                            Source = newDpuSource
                        },
                    ],
                }
            };

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID))
                .ReturnsAsync((MapPin?)null);
            _mapPinRepositoryMock.Setup(r => r.GetByTimeThreshold(telemetry.BeaconID, CNRugbyJunctionBeacon.SubdivisionID, 5))
                .ReturnsAsync(previousMapPin);
            _beaconRailroadServiceMock.Setup(s => s.GetByIdAsync(previousMapPin.BeaconID, previousMapPin.SubdivisionId))
                .ReturnsAsync(beaconRailroads[0]);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(expectedMapPinBeforeInsert, telemetry.LastUpdate))
                .ReturnsAsync(expectedMapPinAfterInsert);

            _clientProxyMock.Setup(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate, expectedMapPinObjects, default))
                    .Returns(Task.CompletedTask);
            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(expectedMapPinBeforeInsert, telemetry.LastUpdate), Times.Once);

            _clientProxyMock?.Verify(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate,
                It.Is<object[]>(args => args[0].Equals(expectedMapPinObjects[0])),
                default), Times.Once);
        }

        /// <summary>
        /// Test ensures that if a train with HOT and EOT is between two beacons that the "from" beacon 
        /// telemetry is discarded before applying the DPU anti-ping pong rule.
        /// </summary>
        [TestMethod]
        public async Task UpsertMapPin_DiscardMapPin_HOTEOTAntiPingPong()
        {
            // Arrange
            var CNSussexBeacon = TestData.CN_Sussex_WI(_timeProviderMock.Object.UtcNow);
            var CNWaukeshaBeacon = TestData.CN_Waukesha_WI(_timeProviderMock.Object.UtcNow);
            var addressID = 20917;

            var calculatedDirection = "N";

            var telemetry = new Telemetry
            {
                BeaconID = CNSussexBeacon.BeaconID,
                Beacon = new Beacon
                {
                    ID = CNSussexBeacon.BeaconID,
                    Name = CNSussexBeacon.Beacon.Name,
                    BeaconRailroads = [
                        CNSussexBeacon
                    ]
                },
                AddressID = addressID,
                Source = SourceEnum.EOT,
                Moving = true,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow
            };

            var previousMapPin = new MapPin
            {
                ID = 0,
                BeaconID = CNSussexBeacon.BeaconID,
                SubdivisionId = CNSussexBeacon.Subdivision.ID,
                Direction = calculatedDirection,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow,
                BeaconRailroad = CNSussexBeacon,
                Moving = null,
                Addresses =
                [
                    new Address
                    {
                        AddressID = addressID,
                        Source = SourceEnum.HOT,
                        CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(-2),
                        LastUpdate = _timeProviderMock.Object.UtcNow.AddMinutes(-2)
                    }
                ]
            };

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID))
                .ReturnsAsync(previousMapPin);
            _beaconRailroadServiceMock.Setup(s => s.GetByIdAsync(previousMapPin.BeaconID, previousMapPin.SubdivisionId))
                .ReturnsAsync(CNSussexBeacon);
            _beaconRailroadServiceMock.Setup(s => s.GetByIdAsync(previousMapPin.BeaconID, previousMapPin.SubdivisionId))
                .ReturnsAsync(CNWaukeshaBeacon);

            var minutesAgo = telemetry.CreatedAt.AddMinutes(-DpuAntiPingPongRule.TIME_WINDOW_MINUTES);
            _telemetryRepositoryMock.Setup(r => r.GetRecentsWithinTimeOffsetAsync(addressID, CNSussexBeacon.Subdivision.RailroadID, minutesAgo))
                .ReturnsAsync(
                [
                    // Sussex tried to steal the train back. This is the new telemetry that should get
                    // deleted before the DPU anti-ping pong rule is applied. 
                    new Telemetry
                    {
                        AddressID = addressID,
                        BeaconID = CNSussexBeacon.BeaconID,
                        Beacon = new Beacon
                        {
                            ID = CNSussexBeacon.BeaconID,
                            Name = CNSussexBeacon.Beacon.Name,
                            BeaconRailroads = [
                                CNSussexBeacon
                            ]
                        },
                        Discarded = false,
                        Source = SourceEnum.EOT,
                        CreatedAt = telemetry.CreatedAt.AddMinutes(-34)
                    },
                    new Telemetry
                    {
                        AddressID = addressID,
                        BeaconID = CNWaukeshaBeacon.BeaconID,
                        Beacon = new Beacon
                        {
                            ID = CNWaukeshaBeacon.BeaconID,
                            Name = CNWaukeshaBeacon.Beacon.Name,
                            BeaconRailroads = [
                                CNWaukeshaBeacon
                            ]
                        },
                        Discarded = false,
                        Source = SourceEnum.HOT,
                        CreatedAt = telemetry.CreatedAt.AddMinutes(-30)
                    },
                    new Telemetry
                    {
                        AddressID = addressID,
                        BeaconID = CNWaukeshaBeacon.BeaconID,
                        Beacon = new Beacon
                        {
                            ID = CNWaukeshaBeacon.BeaconID,
                            Name = CNWaukeshaBeacon.Beacon.Name,
                            BeaconRailroads = [
                                CNWaukeshaBeacon
                            ]
                        },
                        Discarded = false,
                        Source = SourceEnum.HOT,
                        CreatedAt = telemetry.CreatedAt.AddMinutes(-31)
                    },
                    new Telemetry
                    {
                        AddressID = addressID,
                        BeaconID = CNWaukeshaBeacon.BeaconID,
                        Beacon = new Beacon
                        {
                            ID = CNWaukeshaBeacon.BeaconID,
                            Name = CNWaukeshaBeacon.Beacon.Name,
                            BeaconRailroads = [
                                CNWaukeshaBeacon
                            ]
                        },
                        Discarded = false,
                        Source = SourceEnum.HOT,
                        CreatedAt = telemetry.CreatedAt.AddMinutes(-32)
                    },
                    new Telemetry
                    {
                        AddressID = addressID,
                        BeaconID = CNWaukeshaBeacon.BeaconID,
                        Beacon = new Beacon
                        {
                            ID = CNWaukeshaBeacon.BeaconID,
                            Name = CNWaukeshaBeacon.Beacon.Name,
                            BeaconRailroads = [
                                CNWaukeshaBeacon
                            ]
                        },
                        Discarded = false,
                        Source = SourceEnum.HOT,
                        CreatedAt = telemetry.CreatedAt.AddMinutes(-33)
                    },
                    // Sussex was the previous location for the anti-ping pong rule.
                    new Telemetry
                    {
                        AddressID = addressID,
                        BeaconID = CNSussexBeacon.BeaconID,
                        Discarded = false,
                        Source = SourceEnum.EOT,
                        CreatedAt = telemetry.CreatedAt.AddMinutes(-34)
                    },
                    new Telemetry
                    {
                        AddressID = addressID,
                        BeaconID = CNSussexBeacon.BeaconID,
                        Discarded = false,
                        Source = SourceEnum.EOT,
                        CreatedAt = telemetry.CreatedAt.AddMinutes(-35)
                    }
                ]);

            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert
            _telemetryRepositoryMock.Verify(r => r.UpdateAsync(telemetry), Times.Once);

            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(It.IsAny<MapPin>(), It.IsAny<DateTime>()), Times.Never);

            _clientProxyMock?.Verify(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate,
                It.IsAny<object[]>(),
                default), Times.Never);
        }

        /// <summary>
        /// Test that when DPU telemetry arrives with existing DPU address, the map pin is updated correctly.
        /// </summary>
        [TestMethod]
        public async Task UpsertMapPin_DPU_ExistingAddress_OnlyDPUAdded()
        {
            // Arrange
            var beaconRailroad = TestData.CN_RugbyJunction_WI(_timeProviderMock.Object.UtcNow);
            var telemetry = new Telemetry
            {
                BeaconID = beaconRailroad.BeaconID,
                Beacon = new Beacon()
                {
                    ID = beaconRailroad.BeaconID,
                    BeaconRailroads = new List<BeaconRailroad>
                    {
                        beaconRailroad
                    }
                },
                AddressID = 54321,
                TrainID = 9999,
                Source = SourceEnum.DPU,
                Moving = true,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow.AddMinutes(1)
            };

            var previousMapPin = new MapPin
            {
                ID = 100,
                BeaconID = beaconRailroad.BeaconID,
                SubdivisionId = beaconRailroad.Subdivision.ID,
                Direction = "N",
                CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(-10),
                LastUpdate = _timeProviderMock.Object.UtcNow,
                BeaconRailroad = beaconRailroad,
                Moving = true,
                CreatedRailroadID = beaconRailroad.Subdivision.RailroadID,
                Addresses =
                [
                    new Address { AddressID = 12345, Source = SourceEnum.HOT, CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(-10), LastUpdate = _timeProviderMock.Object.UtcNow },
                    new Address { AddressID = telemetry.AddressID, Source = telemetry.Source, DpuTrainID = telemetry.TrainID, CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(-10), LastUpdate = _timeProviderMock.Object.UtcNow }
                ]
            };

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID))
                .ReturnsAsync(previousMapPin);
            _beaconRailroadServiceMock.Setup(b => b.GetByIdAsync(telemetry.BeaconID, beaconRailroad.SubdivisionID))
                .ReturnsAsync(beaconRailroad);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(It.IsAny<MapPin>(), It.IsAny<DateTime>()))
                .ReturnsAsync((MapPin mp, DateTime dt) => mp);
            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);
            _clientProxyMock.Setup(proxy => proxy.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), default))
                .Returns(Task.CompletedTask);

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert - existing DPU match path should still upsert one map pin update.
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(It.IsAny<MapPin>(), It.IsAny<DateTime>()), Times.Once);
        }

        /// <summary>
        /// Test that when telemetry arrives with same address and source (timestamp update only), no new address is added.
        /// </summary>
        [TestMethod]
        public async Task UpsertMapPin_SameAddressAndSource_TimestampUpdateOnly()
        {
            // Arrange
            var beaconRailroad = TestData.CN_RugbyJunction_WI(_timeProviderMock.Object.UtcNow);
            var telemetry = new Telemetry
            {
                BeaconID = beaconRailroad.BeaconID,
                Beacon = new Beacon()
                {
                    ID = beaconRailroad.BeaconID,
                    BeaconRailroads = new List<BeaconRailroad>
                    {
                        beaconRailroad
                    }
                },
                AddressID = 12345,
                Source = SourceEnum.HOT,
                Moving = true,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow.AddMinutes(5)
            };

            var previousMapPin = new MapPin
            {
                ID = 100,
                BeaconID = beaconRailroad.BeaconID,
                SubdivisionId = beaconRailroad.Subdivision.ID,
                Direction = "N",
                CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(-10),
                LastUpdate = _timeProviderMock.Object.UtcNow,
                BeaconRailroad = beaconRailroad,
                Moving = true,
                CreatedRailroadID = beaconRailroad.Subdivision.RailroadID,
                Addresses =
                [
                    new Address {
                        AddressID = 12345,
                        Source = SourceEnum.HOT,
                        CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(-10),
                        LastUpdate = _timeProviderMock.Object.UtcNow
                    }
                ]
            };

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID))
                .ReturnsAsync(previousMapPin);
            _beaconRailroadServiceMock.Setup(b => b.GetByIdAsync(telemetry.BeaconID, beaconRailroad.SubdivisionID))
                .ReturnsAsync(beaconRailroad);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(It.IsAny<MapPin>(), It.IsAny<DateTime>()))
                .ReturnsAsync((MapPin mp, DateTime dt) => mp);
            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);
            _clientProxyMock.Setup(proxy => proxy.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), default))
                .Returns(Task.CompletedTask);

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert - should still have exactly 1 address with updated timestamp
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(It.Is<MapPin>(mp =>
                mp.Addresses.Count == 1 &&
                mp.Addresses.First().AddressID == 12345 &&
                mp.Addresses.First().Source == SourceEnum.HOT &&
                mp.LastUpdate == _timeProviderMock.Object.UtcNow
            ), It.IsAny<DateTime>()), Times.Once);
        }

        /// <summary>
        /// Test that when telemetry arrives at same beacon within threshold time, direction is preserved.
        /// </summary>
        [TestMethod]
        public async Task UpsertMapPin_SameBeacon_WithinThreshold_DirectionPreserved()
        {
            // Arrange
            var beaconRailroad = TestData.CN_RugbyJunction_WI(_timeProviderMock.Object.UtcNow);
            var telemetry = new Telemetry
            {
                BeaconID = beaconRailroad.BeaconID,
                Beacon = new Beacon()
                {
                    ID = beaconRailroad.BeaconID,
                    BeaconRailroads = new List<BeaconRailroad>
                    {
                        beaconRailroad
                    }
                },
                AddressID = 12345,
                Source = SourceEnum.HOT,
                Moving = true,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow.AddMinutes(5) // Only 5 minutes after creation
            };

            var previousMapPin = new MapPin
            {
                ID = 100,
                BeaconID = beaconRailroad.BeaconID,
                SubdivisionId = beaconRailroad.Subdivision.ID,
                Direction = "N", // Should be preserved
                CreatedAt = _timeProviderMock.Object.UtcNow, // Just created
                LastUpdate = _timeProviderMock.Object.UtcNow,
                BeaconRailroad = beaconRailroad,
                Moving = true,
                CreatedRailroadID = beaconRailroad.Subdivision.RailroadID,
                Addresses =
                [
                    new Address { AddressID = 12345, Source = SourceEnum.HOT, CreatedAt = _timeProviderMock.Object.UtcNow, LastUpdate = _timeProviderMock.Object.UtcNow }
                ]
            };

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID))
                .ReturnsAsync(previousMapPin);
            _beaconRailroadServiceMock.Setup(b => b.GetByIdAsync(telemetry.BeaconID, beaconRailroad.SubdivisionID))
                .ReturnsAsync(beaconRailroad);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(It.IsAny<MapPin>(), It.IsAny<DateTime>()))
                .ReturnsAsync((MapPin mp, DateTime dt) => mp);
            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);
            _clientProxyMock.Setup(proxy => proxy.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), default))
                .Returns(Task.CompletedTask);

            // Setup time to be 5 minutes after creation (under 360 minute threshold)
            _timeProviderMock.Setup(tp => tp.UtcNow).Returns(_timeProviderMock.Object.UtcNow.AddMinutes(5));

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert - direction should still be "N"
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(It.Is<MapPin>(mp =>
                mp.Direction == "N"
            ), It.IsAny<DateTime>()), Times.Once);
        }

        /// <summary>
        /// Test ensures that when telemetry has no Moving value (null) but existing MapPin has Moving = true,
        /// the MapPin's Moving value is not overwritten to null.
        /// </summary>
        [TestMethod]
        public async Task UpsertMapPin_CalculateMotion_ExistingMovingNotOverwritten()
        {
            // Arrange
            var beaconRailroad = TestData.CN_RugbyJunction_WI(_timeProviderMock.Object.UtcNow);
            var telemetry = new Telemetry
            {
                BeaconID = beaconRailroad.BeaconID,
                Beacon = new Beacon()
                {
                    ID = beaconRailroad.BeaconID,
                    BeaconRailroads = new List<BeaconRailroad>
                    {
                        beaconRailroad
                    }
                },
                AddressID = 12345,
                Source = SourceEnum.HOT,
                Moving = null, // No moving value
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow.AddMinutes(1)
            };

            var previousMapPin = new MapPin
            {
                ID = 100,
                BeaconID = beaconRailroad.BeaconID,
                SubdivisionId = beaconRailroad.Subdivision.ID,
                Direction = "N",
                CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(-10),
                LastUpdate = _timeProviderMock.Object.UtcNow,
                BeaconRailroad = beaconRailroad,
                Moving = true,
                CreatedRailroadID = beaconRailroad.Subdivision.RailroadID,
                Addresses =
                [
                    new Address {
                        AddressID = 12345,
                        Source = SourceEnum.HOT,
                        CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(-10),
                        LastUpdate = _timeProviderMock.Object.UtcNow
                    }
                ]
            };

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID))
                .ReturnsAsync(previousMapPin);
            _beaconRailroadServiceMock.Setup(b => b.GetByIdAsync(telemetry.BeaconID, beaconRailroad.SubdivisionID))
                .ReturnsAsync(beaconRailroad);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(It.IsAny<MapPin>(), It.IsAny<DateTime>()))
                .ReturnsAsync((MapPin mp, DateTime dt) => mp);
            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);
            _clientProxyMock.Setup(proxy => proxy.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), default))
                .Returns(Task.CompletedTask);

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert - Moving should still be true (not updated to null)
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(It.Is<MapPin>(mp =>
                mp.Moving == true
            ), It.IsAny<DateTime>()), Times.Once);
        }

        [TestMethod]
        public async Task UpsertMapPin_CalculateMotion_BrakePressureApplied()
        {
            // Arrange
            var beaconRailroad = TestData.CN_RugbyJunction_WI(_timeProviderMock.Object.UtcNow);
            var telemetry = new Telemetry
            {
                BeaconID = beaconRailroad.BeaconID,
                Beacon = new Beacon()
                {
                    ID = beaconRailroad.BeaconID,
                    BeaconRailroads = new List<BeaconRailroad>
                    {
                        beaconRailroad
                    }
                },
                AddressID = 12345,
                TrainID = 9999,
                BrakePipePressure = 86, // Above 85 PSI indicates moving.
                Source = SourceEnum.DPU,
                Moving = null,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow.AddMinutes(1)
            };

            var previousMapPin = new MapPin
            {
                ID = 100,
                BeaconID = beaconRailroad.BeaconID,
                SubdivisionId = beaconRailroad.Subdivision.ID,
                Direction = "N",
                CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(-10),
                LastUpdate = _timeProviderMock.Object.UtcNow,
                BeaconRailroad = beaconRailroad,
                Moving = null, // Previously no indication of moving.
                CreatedRailroadID = beaconRailroad.Subdivision.RailroadID,
                Addresses =
                [
                    new Address {
                        AddressID = 12345,
                        Source = SourceEnum.HOT,
                        CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(-10),
                        LastUpdate = _timeProviderMock.Object.UtcNow
                    }
                ]
            };

            _mapPinRepositoryMock.Setup(r => r.GetByTrainIdAsync(telemetry.TrainID!.Value, MapPinService.TIME_THRESHOLD_DPU_MINUTES))
                .ReturnsAsync(previousMapPin);
            _beaconRailroadServiceMock.Setup(b => b.GetByIdAsync(telemetry.BeaconID, beaconRailroad.SubdivisionID))
                .ReturnsAsync(beaconRailroad);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(It.IsAny<MapPin>(), It.IsAny<DateTime>()))
                .ReturnsAsync((MapPin mp, DateTime dt) => mp);
            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);
            _clientProxyMock.Setup(proxy => proxy.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), default))
                .Returns(Task.CompletedTask);

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert - Moving should still be true (not updated to null)
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(It.Is<MapPin>(mp =>
                mp.Moving == true
            ), It.IsAny<DateTime>()), Times.Once);
        }

        [TestMethod]
        public async Task UpsertMapPin_CalculateMotion_MotionIndicatorOn()
        {
            // Arrange
            var beaconRailroad = TestData.CN_RugbyJunction_WI(_timeProviderMock.Object.UtcNow);
            var telemetry = new Telemetry
            {
                BeaconID = beaconRailroad.BeaconID,
                Beacon = new Beacon()
                {
                    ID = beaconRailroad.BeaconID,
                    BeaconRailroads = new List<BeaconRailroad>
                    {
                        beaconRailroad
                    }
                },
                AddressID = 12345,
                BrakePipePressure = 70, // Above 85 PSI indicates moving.
                Source = SourceEnum.EOT,
                Moving = null,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow.AddMinutes(1)
            };

            var previousMapPin = new MapPin
            {
                ID = 100,
                BeaconID = beaconRailroad.BeaconID,
                SubdivisionId = beaconRailroad.Subdivision.ID,
                Direction = "N",
                CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(-10),
                LastUpdate = _timeProviderMock.Object.UtcNow,
                BeaconRailroad = beaconRailroad,
                Moving = null, // Previously no indication of moving.
                CreatedRailroadID = beaconRailroad.Subdivision.RailroadID,
                Addresses =
                [
                    new Address {
                        AddressID = 12345,
                        Source = SourceEnum.HOT,
                        CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(-10),
                        LastUpdate = _timeProviderMock.Object.UtcNow
                    }
                ]
            };

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID))
                .ReturnsAsync(previousMapPin);
            _beaconRailroadServiceMock.Setup(b => b.GetByIdAsync(telemetry.BeaconID, beaconRailroad.SubdivisionID))
                .ReturnsAsync(beaconRailroad);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(It.IsAny<MapPin>(), It.IsAny<DateTime>()))
                .ReturnsAsync((MapPin mp, DateTime dt) => mp);
            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);
            _clientProxyMock.Setup(proxy => proxy.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), default))
                .Returns(Task.CompletedTask);

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert - EOT without telemetry.Moving should yield unknown motion.
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(It.Is<MapPin>(mp =>
                mp.Moving == null
            ), It.IsAny<DateTime>()), Times.Once);
        }

        /// <summary>
        /// A train with an existing matching DPU train ID should be combined with new telemetry 
        /// DPU with a different address ID but same train ID when the beacon is multi-track.
        /// </summary>
        [TestMethod]
        public async Task UpsertMapPin_DPUsMergedOnMultiTrack()
        {
            // Arrange
            var CNSussexBeacon = TestData.CN_Sussex_WI(_timeProviderMock.Object.UtcNow);
            var fromDirection = null as String;
            var fromAddressID = 92342;
            var fromTrainID = 123;
            var fromSource = SourceEnum.DPU;

            var CNRugbyJunctionBeacon = TestData.CN_RugbyJunction_WI(_timeProviderMock.Object.UtcNow);
            var toAddressID = 23424;
            var toTrainID = 123;
            var toSource = SourceEnum.DPU;

            var beaconRailroads = new List<BeaconRailroad>
            {
                CNRugbyJunctionBeacon,
                CNSussexBeacon
            };

            var telemetry = new Telemetry
            {
                BeaconID = CNRugbyJunctionBeacon.BeaconID,
                Beacon = new Beacon
                {
                    ID = CNRugbyJunctionBeacon.BeaconID,
                    Name = CNRugbyJunctionBeacon.Beacon.Name,
                    BeaconRailroads = beaconRailroads
                },
                AddressID = toAddressID,
                TrainID = toTrainID,
                Source = toSource,
                Moving = true,
                CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(2), // Later than previous map pin, but still in time threshold.
                LastUpdate = _timeProviderMock.Object.UtcNow.AddMinutes(2),
            };

            var fromMapPin = new MapPin
            {
                ID = 234,
                BeaconID = CNSussexBeacon.BeaconID,
                SubdivisionId = CNSussexBeacon.Subdivision.ID,
                Direction = fromDirection,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow,
                BeaconRailroad = CNSussexBeacon,
                Moving = telemetry.Moving,
                CreatedRailroadID = CNSussexBeacon.Subdivision.RailroadID,
                Addresses =
                    [
                        new Address
                        {
                            AddressID = fromAddressID,
                            DpuTrainID = fromTrainID,
                            Source = fromSource,
                            CreatedAt = _timeProviderMock.Object.UtcNow,
                            LastUpdate = _timeProviderMock.Object.UtcNow
                        }
                    ],
            };

            var toMapPinBeforeUpdate = new MapPin
            {
                ID = 234,
                BeaconID = CNRugbyJunctionBeacon.BeaconID,
                SubdivisionId = CNRugbyJunctionBeacon.Subdivision.ID,
                Direction = "N",
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow,
                BeaconRailroad = CNRugbyJunctionBeacon,
                Moving = null,
                CreatedRailroadID = CNSussexBeacon.Subdivision.RailroadID,
                Addresses =
                [
                    new Address
                    {
                        AddressID = fromAddressID,
                        DpuTrainID = fromTrainID,
                        Source = fromSource,
                        CreatedAt = _timeProviderMock.Object.UtcNow,
                        LastUpdate = _timeProviderMock.Object.UtcNow
                    },
                    new Address
                    {
                        AddressID = toAddressID,
                        DpuTrainID = toTrainID,
                        Source = toSource,
                        CreatedAt = telemetry.CreatedAt,
                        LastUpdate = telemetry.LastUpdate
                    }
                ],
            };

            var toMapPinAfterUpdate = toMapPinBeforeUpdate.Clone();
            toMapPinAfterUpdate.ID = 936; // ID returned after insert.
            toMapPinAfterUpdate.LastUpdate = telemetry.LastUpdate;
            toMapPinAfterUpdate.Addresses.ToList()[0].LastUpdate = telemetry.LastUpdate;
            toMapPinAfterUpdate.Addresses.ToList()[1].LastUpdate = telemetry.LastUpdate;

            var toMapPinObjects = new object[]
            {
                new MapPinDTO
                {
                    ID = toMapPinAfterUpdate.ID,
                    Direction = "N",
                    BeaconID = telemetry.BeaconID,
                    BeaconName = CNRugbyJunctionBeacon.Beacon.Name,
                    Railroad = CNRugbyJunctionBeacon.Subdivision.Railroad.Name,
                    Subdivision = CNRugbyJunctionBeacon.Subdivision.Name,
                    SubdivisionID = CNRugbyJunctionBeacon.Subdivision.ID,
                    Latitude = CNRugbyJunctionBeacon.Latitude,
                    Longitude = CNRugbyJunctionBeacon.Longitude,
                    Milepost = CNRugbyJunctionBeacon.Milepost,
                    Moving = null,
                    CreatedAt = _timeProviderMock.Object.UtcNow,
                    LastUpdate = telemetry.LastUpdate,
                    Addresses =
                    [
                        new AddressDTO
                        {
                            AddressID = fromAddressID,
                            Source = fromSource
                        },
                        new AddressDTO
                        {
                            AddressID = toAddressID,
                            Source = toSource
                        }
                    ],
                }
             };

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID))
                .ReturnsAsync((MapPin?)null);
            _mapPinRepositoryMock.Setup(r => r.GetByTrainIdAsync(telemetry.TrainID.Value, MapPinService.TIME_THRESHOLD_DPU_MINUTES))
                .ReturnsAsync(fromMapPin);
            _beaconRailroadServiceMock.Setup(b => b.GetByIdAsync(telemetry.BeaconID, fromMapPin.SubdivisionId))
                .ReturnsAsync(CNRugbyJunctionBeacon);
            _beaconRailroadServiceMock.Setup(b => b.GetByIdAsync(fromMapPin.BeaconID, fromMapPin.SubdivisionId))
                .ReturnsAsync(CNSussexBeacon);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(toMapPinBeforeUpdate, It.IsAny<DateTime>()))
                .ReturnsAsync(toMapPinAfterUpdate);

            _clientProxyMock.Setup(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate, toMapPinObjects, default))
                    .Returns(Task.CompletedTask);
            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(toMapPinBeforeUpdate, It.IsAny<DateTime>()), Times.Once);

            _clientProxyMock?.Verify(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate,
                It.Is<object[]>(args => args[0].Equals(toMapPinObjects[0])),
                default), Times.Once);
        }

        /// <summary>
        /// Regression test for DPU flow: if a train with the same DPU train ID is detected at
        /// Sussex and then Rugby 10 minutes later with a second DPU address, the existing map pin
        /// should be updated and direction should be calculated.
        /// </summary>
        [TestMethod]
        public async Task UpsertMapPin_DpuSecondAddress_AfterTenMinutes_CalculatesDirection()
        {
            // Arrange
            var now = _timeProviderMock.Object.UtcNow;
            var CNSussexBeacon = TestData.CN_Sussex_WI(now);
            var CNRugbyJunctionBeacon = TestData.CN_RugbyJunction_WI(now);
            var WSORRugbyJunctionBeacon = TestData.WSOR_RugbyJunction_WI(now);

            var fromAddressID = 90000;
            var toAddressID = 90001;
            var trainID = 123;

            var telemetry = new Telemetry
            {
                BeaconID = CNRugbyJunctionBeacon.BeaconID,
                Beacon = new Beacon
                {
                    ID = CNRugbyJunctionBeacon.BeaconID,
                    Name = CNRugbyJunctionBeacon.Beacon.Name,
                    BeaconRailroads = [CNRugbyJunctionBeacon, WSORRugbyJunctionBeacon]
                },
                AddressID = toAddressID,
                TrainID = trainID,
                Source = SourceEnum.DPU,
                Moving = true,
                CreatedAt = now.AddMinutes(10),
                LastUpdate = now.AddMinutes(10)
            };

            var existingMapPin = new MapPin
            {
                ID = 234,
                BeaconID = CNSussexBeacon.BeaconID,
                SubdivisionId = CNSussexBeacon.Subdivision.ID,
                Direction = null,
                CreatedAt = now,
                LastUpdate = now,
                BeaconRailroad = CNSussexBeacon,
                Moving = true,
                CreatedRailroadID = CNSussexBeacon.Subdivision.RailroadID,
                Addresses =
                [
                    new Address
                    {
                        AddressID = fromAddressID,
                        DpuTrainID = trainID,
                        Source = SourceEnum.DPU,
                        CreatedAt = now,
                        LastUpdate = now
                    }
                ]
            };

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(toAddressID))
                .ReturnsAsync((MapPin?)null);
            _mapPinRepositoryMock.Setup(r => r.GetByTrainIdAsync(trainID, MapPinService.TIME_THRESHOLD_DPU_MINUTES))
                .ReturnsAsync(existingMapPin);
            _beaconRailroadServiceMock.Setup(b => b.GetByIdAsync(existingMapPin.BeaconID, existingMapPin.SubdivisionId))
                .ReturnsAsync(CNSussexBeacon);
            _beaconRailroadServiceMock.Setup(b => b.GetByIdAsync(telemetry.BeaconID, existingMapPin.SubdivisionId))
                .ReturnsAsync(CNRugbyJunctionBeacon);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(It.IsAny<MapPin>(), It.IsAny<DateTime>()))
                .ReturnsAsync((MapPin mp, DateTime _) => mp);

            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);
            _clientProxyMock.Setup(proxy => proxy.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), default))
                .Returns(Task.CompletedTask);

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(
                It.Is<MapPin>(mp =>
                    mp.ID == existingMapPin.ID &&
                    mp.BeaconID == CNRugbyJunctionBeacon.BeaconID &&
                    !string.IsNullOrWhiteSpace(mp.Direction) &&
                    mp.Addresses.Any(a => a.AddressID == fromAddressID && a.DpuTrainID == trainID) &&
                    mp.Addresses.Any(a => a.AddressID == toAddressID && a.DpuTrainID == trainID)),
                telemetry.LastUpdate), Times.Once);
        }

        /// <summary>
        /// Verifies that if no matching DPU address and train ID is found on existing map pins and
        /// a matching train ID is found on a different railroad, the DPU telemetry is ignored.
        /// </summary>
        [TestMethod]
        public async Task UpsertMapPin_DPUFromDifferentRailroadIgnored()
        {
            // Arrange
            var CNRugbyJunctionBeacon = TestData.CN_RugbyJunction_WI(_timeProviderMock.Object.UtcNow);
            var WSORRugbyJunctionBeacon = TestData.WSOR_RugbyJunction_WI(_timeProviderMock.Object.UtcNow);

            var toAddressID = 23424;
            var toTrainID = 123;

            var UPSussexBeacon = TestData.UP_Sussex_WI(_timeProviderMock.Object.UtcNow);

            var telemetry = new Telemetry
            {
                BeaconID = UPSussexBeacon.BeaconID,
                Beacon = new Beacon
                {
                    ID = UPSussexBeacon.BeaconID,
                    Name = UPSussexBeacon.Beacon.Name,
                    BeaconRailroads =
                    [
                        // Current beacon is multi-beacon, multi-track
                        CNRugbyJunctionBeacon,
                        WSORRugbyJunctionBeacon
                    ]
                },
                AddressID = toAddressID,
                TrainID = toTrainID,
                Source = SourceEnum.DPU,
                Moving = true,
                CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(2),
                LastUpdate = _timeProviderMock.Object.UtcNow.AddMinutes(2),
            };

            var wrongRailroadMapPin = new MapPin
            {
                ID = 234,
                BeaconID = UPSussexBeacon.BeaconID,
                SubdivisionId = UPSussexBeacon.Subdivision.ID,
                Direction = "N",
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow,
                BeaconRailroad = UPSussexBeacon,
                Moving = telemetry.Moving,
                Addresses =
                [
                    new Address
                    {
                        AddressID = toAddressID,
                        DpuTrainID = toTrainID, // Same train ID but different railroad.
                        Source = SourceEnum.DPU,
                        CreatedAt = _timeProviderMock.Object.UtcNow,
                        LastUpdate = _timeProviderMock.Object.UtcNow
                    }
                ]
            };

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID))
                .ReturnsAsync((MapPin?)null);
            _mapPinRepositoryMock.Setup(r => r.GetByTrainIdAsync(telemetry.TrainID.Value, MapPinService.TIME_THRESHOLD_DPU_MINUTES))
                .ReturnsAsync(wrongRailroadMapPin);

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert
            _telemetryRepositoryMock.Verify(r => r.UpdateAsync(telemetry), Times.Once);

            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(It.IsAny<MapPin>(), It.IsAny<DateTime>()), Times.Never);

            _clientProxyMock?.Verify(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate,
                It.IsAny<object[]>(),
                default), Times.Never);
        }

        /// <summary>
        /// Verifies that DPU matching first checks for an exact address+train match and does not
        /// fall back to train-only lookup when an exact match is found.
        /// </summary>
        [TestMethod]
        public async Task UpsertMapPin_DpuAddressAndTrainMatch_UsesExactLookupBeforeTrainOnly()
        {
            // Arrange
            var now = _timeProviderMock.Object.UtcNow;

            var fromBeacon = TestData.CN_Sussex_WI(now.AddMinutes(-10));
            var toBeacon = TestData.CN_RugbyJunction_WI(now);

            var telemetry = new Telemetry
            {
                BeaconID = toBeacon.BeaconID,
                Beacon = new Beacon
                {
                    ID = toBeacon.BeaconID,
                    Name = toBeacon.Beacon.Name,
                    BeaconRailroads = [toBeacon]
                },
                AddressID = 55501,
                TrainID = 808,
                Source = SourceEnum.DPU,
                Moving = true,
                CreatedAt = now,
                LastUpdate = now
            };

            var existingExactMapPin = new MapPin
            {
                ID = 313,
                BeaconID = fromBeacon.BeaconID,
                SubdivisionId = fromBeacon.Subdivision.ID,
                Direction = null,
                CreatedAt = now.AddMinutes(-10),
                LastUpdate = now.AddMinutes(-10),
                BeaconRailroad = fromBeacon,
                Moving = true,
                CreatedRailroadID = fromBeacon.Subdivision.RailroadID,
                Addresses =
                [
                    new Address
                    {
                        AddressID = telemetry.AddressID,
                        DpuTrainID = telemetry.TrainID,
                        Source = SourceEnum.DPU,
                        CreatedAt = now.AddMinutes(-10),
                        LastUpdate = now.AddMinutes(-10)
                    }
                ]
            };

            _mapPinRepositoryMock.Setup(r => r.GetByAddressAndTrainIdAsync(telemetry.AddressID, telemetry.TrainID!.Value, MapPinService.TIME_THRESHOLD_DPU_EXACT_MINUTES))
                .ReturnsAsync(existingExactMapPin);
            _beaconRailroadServiceMock.Setup(b => b.GetByIdAsync(existingExactMapPin.BeaconID, existingExactMapPin.SubdivisionId))
                .ReturnsAsync(fromBeacon);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(It.IsAny<MapPin>(), telemetry.LastUpdate))
                .ReturnsAsync((MapPin mp, DateTime _) => mp);

            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);
            _clientProxyMock.Setup(proxy => proxy.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), default))
                .Returns(Task.CompletedTask);

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert
            _mapPinRepositoryMock.Verify(r => r.GetByAddressAndTrainIdAsync(telemetry.AddressID, telemetry.TrainID.Value, MapPinService.TIME_THRESHOLD_DPU_EXACT_MINUTES), Times.Once);
            _mapPinRepositoryMock.Verify(r => r.GetByTrainIdAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(
                It.Is<MapPin>(mp =>
                    mp.ID == existingExactMapPin.ID &&
                    mp.BeaconID == toBeacon.BeaconID),
                telemetry.LastUpdate), Times.Once);
        }

        [TestMethod]
        public async Task UpsertMapPin_DpuNoExactMatch_UsesSplitLookupThresholds()
        {
            // Arrange
            var now = _timeProviderMock.Object.UtcNow;
            var beacon = TestData.CN_RugbyJunction_WI(now);

            var telemetry = new Telemetry
            {
                BeaconID = beacon.BeaconID,
                Beacon = new Beacon
                {
                    ID = beacon.BeaconID,
                    Name = beacon.Beacon.Name,
                    BeaconRailroads = [beacon]
                },
                AddressID = 77701,
                TrainID = 901,
                Source = SourceEnum.DPU,
                CreatedAt = now,
                LastUpdate = now
            };

            _mapPinRepositoryMock.Setup(r => r.GetByAddressAndTrainIdAsync(
                    telemetry.AddressID,
                    telemetry.TrainID!.Value,
                    MapPinService.TIME_THRESHOLD_DPU_EXACT_MINUTES))
                .ReturnsAsync((MapPin?)null);

            _mapPinRepositoryMock.Setup(r => r.GetByTrainIdAsync(
                    telemetry.TrainID!.Value,
                    MapPinService.TIME_THRESHOLD_DPU_TRAIN_ONLY_MINUTES))
                .ReturnsAsync((MapPin?)null);

            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(It.IsAny<MapPin>(), telemetry.LastUpdate))
                .ReturnsAsync((MapPin mp, DateTime _) => mp);

            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);
            _clientProxyMock.Setup(proxy => proxy.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), default))
                .Returns(Task.CompletedTask);

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert
            _mapPinRepositoryMock.Verify(r => r.GetByAddressAndTrainIdAsync(
                telemetry.AddressID,
                telemetry.TrainID.Value,
                MapPinService.TIME_THRESHOLD_DPU_EXACT_MINUTES), Times.Once);

            _mapPinRepositoryMock.Verify(r => r.GetByTrainIdAsync(
                telemetry.TrainID.Value,
                MapPinService.TIME_THRESHOLD_DPU_TRAIN_ONLY_MINUTES), Times.Once);
        }

        /// <summary>
        /// Verifies that an exact DPU match (same AddressID + TrainID) always resolves as Matched and
        /// calculates direction on a beacon change, even when the time delta since LastUpdate would produce
        /// a speed exceeding the threshold used for train-only (CN reuse) detection.
        /// The speed check only makes sense for the train-only path where AddressID differs.
        /// </summary>
        [TestMethod]
        public async Task UpsertMapPin_ExactMatchDpu_BeaconChange_SpeedWouldExceedThreshold_StillMatchedAndDirectionCalculated()
        {
            // Arrange
            var now = _timeProviderMock.Object.UtcNow;

            var fromBeacon = TestData.CN_Sussex_WI(now.AddMinutes(-1));
            var toBeacon = TestData.CN_RugbyJunction_WI(now);

            var telemetry = new Telemetry
            {
                BeaconID = toBeacon.BeaconID,
                Beacon = new Beacon
                {
                    ID = toBeacon.BeaconID,
                    Name = toBeacon.Beacon.Name,
                    BeaconRailroads = [toBeacon]
                },
                AddressID = 55501,
                TrainID = 808,
                Source = SourceEnum.DPU,
                Moving = true,
                CreatedAt = now,
                LastUpdate = now
            };

            // Pin was last updated 1 minute ago at Sussex. Sussex→Rugby Jct is ~8.6 miles;
            // adjusted distance ~1.6 miles in 1 minute produces ~96 MPH — above the 50 MPH
            // threshold that guards against CN train-ID reuse on the train-only path.
            // The exact-match path must skip that check and return Matched directly.
            var existingExactMapPin = new MapPin
            {
                ID = 313,
                BeaconID = fromBeacon.BeaconID,
                SubdivisionId = fromBeacon.Subdivision.ID,
                Direction = null,
                CreatedAt = now.AddMinutes(-1),
                LastUpdate = now.AddMinutes(-1),
                BeaconRailroad = fromBeacon,
                Moving = true,
                CreatedRailroadID = fromBeacon.Subdivision.RailroadID,
                Addresses =
                [
                    new Address
                    {
                        AddressID = telemetry.AddressID,
                        DpuTrainID = telemetry.TrainID,
                        Source = SourceEnum.DPU,
                        CreatedAt = now.AddMinutes(-1),
                        LastUpdate = now.AddMinutes(-1)
                    }
                ]
            };

            _mapPinRepositoryMock.Setup(r => r.GetByAddressAndTrainIdAsync(telemetry.AddressID, telemetry.TrainID!.Value, MapPinService.TIME_THRESHOLD_DPU_EXACT_MINUTES))
                .ReturnsAsync(existingExactMapPin);
            _beaconRailroadServiceMock.Setup(b => b.GetByIdAsync(existingExactMapPin.BeaconID, existingExactMapPin.SubdivisionId))
                .ReturnsAsync(fromBeacon);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(It.IsAny<MapPin>(), telemetry.LastUpdate))
                .ReturnsAsync((MapPin mp, DateTime _) => mp);

            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);
            _clientProxyMock.Setup(proxy => proxy.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), default))
                .Returns(Task.CompletedTask);

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert — pin matched and moved to Rugby Jct; direction calculated (not null)
            _mapPinRepositoryMock.Verify(r => r.GetByAddressAndTrainIdAsync(telemetry.AddressID, telemetry.TrainID.Value, MapPinService.TIME_THRESHOLD_DPU_EXACT_MINUTES), Times.Once);
            _mapPinRepositoryMock.Verify(r => r.GetByTrainIdAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(
                It.Is<MapPin>(mp =>
                    mp.ID == existingExactMapPin.ID &&
                    mp.BeaconID == toBeacon.BeaconID &&
                    !string.IsNullOrWhiteSpace(mp.Direction)),
                telemetry.LastUpdate), Times.Once);
        }

        /// <summary>
        /// Verifies that if a matching DPU train ID is found on an existing map pin from the same railroad,
        /// the DPU telemetry is merged into that map pin.
        /// </summary>
        [TestMethod]
        public async Task UpsertMapPin_DPUsFromSameRailroadMerged()
        {
            // Arrange
            var CNSussexBeacon = TestData.CN_Sussex_WI(_timeProviderMock.Object.UtcNow);
            var fromAddressID = 92342;
            var fromTrainID = 123;

            var CNRugbyJunctionBeacon = TestData.CN_RugbyJunction_WI(_timeProviderMock.Object.UtcNow);
            var toAddressID = 23424;
            var toTrainID = 123;

            var telemetry = new Telemetry
            {
                BeaconID = CNRugbyJunctionBeacon.BeaconID,
                Beacon = new Beacon
                {
                    ID = CNRugbyJunctionBeacon.BeaconID,
                    Name = CNRugbyJunctionBeacon.Beacon.Name,
                    BeaconRailroads = new[]
                    {
                        CNRugbyJunctionBeacon
                    },
                },
                AddressID = toAddressID,
                TrainID = toTrainID,
                Source = SourceEnum.DPU,
                Moving = true,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow
            };

            var fromMapPin = new MapPin
            {
                ID = 234,
                BeaconID = CNSussexBeacon.BeaconID,
                SubdivisionId = CNSussexBeacon.Subdivision.ID,
                CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(-15),
                LastUpdate = _timeProviderMock.Object.UtcNow.AddMinutes(-15),
                BeaconRailroad = CNSussexBeacon,
                Moving = telemetry.Moving,
                CreatedRailroadID = CNSussexBeacon.Subdivision.RailroadID,
                Addresses =
                    [
                        new Address
                        {
                            AddressID = fromAddressID,
                            DpuTrainID = fromTrainID,
                            Source = SourceEnum.DPU,
                            CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(-15),
                            LastUpdate = _timeProviderMock.Object.UtcNow.AddMinutes(-15)
                        }
                    ],
            };

            var toMapPinBeforeUpdate = new MapPin
            {
                ID = 234,
                BeaconID = CNRugbyJunctionBeacon.BeaconID,
                SubdivisionId = CNRugbyJunctionBeacon.Subdivision.ID,
                Direction = "N",
                CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(-15),
                LastUpdate = _timeProviderMock.Object.UtcNow.AddMinutes(-15),
                BeaconRailroad = CNRugbyJunctionBeacon,
                Moving = null,
                CreatedRailroadID = CNSussexBeacon.Subdivision.RailroadID,
                Addresses =
                [
                    new Address
                    {
                        AddressID = fromAddressID,
                        DpuTrainID = fromTrainID,
                        Source = SourceEnum.DPU,
                        CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(-15),
                        LastUpdate = _timeProviderMock.Object.UtcNow.AddMinutes(-15),
                    },
                    new Address
                    {
                        AddressID = toAddressID,
                        DpuTrainID = toTrainID,
                        Source = SourceEnum.DPU,
                        CreatedAt = _timeProviderMock.Object.UtcNow,
                        LastUpdate = _timeProviderMock.Object.UtcNow
                    }
                ],
            };

            var toMapPinAfterUpdate = toMapPinBeforeUpdate.Clone();
            toMapPinAfterUpdate.ID = 936; // ID returned after insert.
            toMapPinAfterUpdate.LastUpdate = telemetry.LastUpdate;
            toMapPinAfterUpdate.Addresses.ToList()[0].LastUpdate = telemetry.LastUpdate;
            toMapPinAfterUpdate.Addresses.ToList()[1].LastUpdate = telemetry.LastUpdate;

            var toMapPinObjects = new object[]
            {
                new MapPinDTO
                {
                    ID = toMapPinAfterUpdate.ID,
                    Direction = "N",
                    BeaconID = telemetry.BeaconID,
                    BeaconName = CNRugbyJunctionBeacon.Beacon.Name,
                    Railroad = CNRugbyJunctionBeacon.Subdivision.Railroad.Name,
                    Subdivision = CNRugbyJunctionBeacon.Subdivision.Name,
                    SubdivisionID = CNRugbyJunctionBeacon.Subdivision.ID,
                    Latitude = CNRugbyJunctionBeacon.Latitude,
                    Longitude = CNRugbyJunctionBeacon.Longitude,
                    Milepost = CNRugbyJunctionBeacon.Milepost,
                    Moving = null,
                    CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(-15),
                    LastUpdate = telemetry.LastUpdate,
                    Addresses =
                    [
                        new AddressDTO
                        {
                            AddressID = fromAddressID,
                            Source = SourceEnum.DPU
                        },
                        new AddressDTO
                        {
                            AddressID = toAddressID,
                            Source = SourceEnum.DPU
                        }
                    ],
                }
             };

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID))
                .ReturnsAsync((MapPin?)null);
            _mapPinRepositoryMock.Setup(r => r.GetByTrainIdAsync(telemetry.TrainID.Value, MapPinService.TIME_THRESHOLD_DPU_MINUTES))
                .ReturnsAsync(fromMapPin);
            _beaconRailroadServiceMock.Setup(b => b.GetByIdAsync(telemetry.BeaconID, fromMapPin.SubdivisionId))
                .ReturnsAsync(CNRugbyJunctionBeacon);
            _beaconRailroadServiceMock.Setup(b => b.GetByIdAsync(fromMapPin.BeaconID, fromMapPin.SubdivisionId))
                .ReturnsAsync(CNSussexBeacon);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(toMapPinBeforeUpdate, It.IsAny<DateTime>()))
                .ReturnsAsync(toMapPinAfterUpdate);

            _clientProxyMock.Setup(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate, toMapPinObjects, default))
                    .Returns(Task.CompletedTask);
            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(toMapPinBeforeUpdate, It.IsAny<DateTime>()), Times.Once);

            _clientProxyMock?.Verify(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate,
                It.Is<object[]>(args => args[0].Equals(toMapPinObjects[0])),
                default), Times.Once);
        }

        /// <summary>
        /// Test ensures that if a matching DPU train ID is found on an existing map pin from 
        /// the same railroad but with a different DPU address ID and there was existing "ping pong"
        /// behavior, the new DPU telemetry is discarded.
        /// 
        /// Example:
        ///
        /// Beacon AddressID TrainID Action
        /// ----------------------------------
        /// Sussex 21348     123     Discarded <-- This is the case this test verifies.
        /// Sussex 47622     123     Discarded
        /// Rugby  47622     123
        /// Rugby  47622     123
        /// Sussex 47622     123
        /// </summary>
        [TestMethod]
        public async Task UpsertMapPin_DPUsNewAddressIdSameTrainIdPingPongShouldBeDiscarded()
        {
            // Arrange
            var CNSussexBeacon = TestData.CN_Sussex_WI(_timeProviderMock.Object.UtcNow);
            var CNRugbyJunctionBeacon = TestData.CN_RugbyJunction_WI(_timeProviderMock.Object.UtcNow);
            var addressID21348 = 21348;
            var addressID47622 = 47622;
            var trainID = 123;

            var telemetry = new Telemetry
            {
                BeaconID = CNSussexBeacon.BeaconID,
                Beacon = new Beacon
                {
                    ID = CNSussexBeacon.BeaconID,
                    Name = CNSussexBeacon.Beacon.Name,
                    BeaconRailroads = new[]
                    {
                        CNSussexBeacon
                    },
                },
                AddressID = addressID21348,
                TrainID = trainID,
                Source = SourceEnum.DPU,
                Moving = true,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow
            };

            var fromMapPin = new MapPin
            {
                ID = 234,
                BeaconID = CNRugbyJunctionBeacon.BeaconID,
                SubdivisionId = CNRugbyJunctionBeacon.Subdivision.ID,
                Direction = "N",
                CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(-16),
                LastUpdate = _timeProviderMock.Object.UtcNow.AddMinutes(-16),
                BeaconRailroad = CNRugbyJunctionBeacon,
                Moving = telemetry.Moving,
                CreatedRailroadID = CNSussexBeacon.Subdivision.RailroadID,
                Addresses =
                [
                    new Address
                    {
                        AddressID = addressID47622,
                        DpuTrainID = trainID,
                        Source = SourceEnum.DPU,
                        CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(-15),
                        LastUpdate = _timeProviderMock.Object.UtcNow.AddMinutes(-15)
                    }
                ],
            };

            var toMapPinBeforeUpdate = new MapPin
            {
                ID = 234,
                BeaconID = CNSussexBeacon.BeaconID,
                SubdivisionId = CNSussexBeacon.Subdivision.ID,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow,
                BeaconRailroad = CNSussexBeacon,
                Moving = telemetry.Moving,
                CreatedRailroadID = CNSussexBeacon.Subdivision.RailroadID,
                Addresses =
                    [
                        new Address
                        {
                            AddressID = addressID21348,
                            DpuTrainID = trainID,
                            Source = SourceEnum.DPU,
                            CreatedAt = _timeProviderMock.Object.UtcNow,
                            LastUpdate = _timeProviderMock.Object.UtcNow
                        }
                    ],
            };

            var toMapPinAfterUpdate = toMapPinBeforeUpdate.Clone();
            toMapPinAfterUpdate.ID = 936; // ID returned after insert.

            var toMapPinObjects = new object[]
            {
                new MapPinDTO
                {
                    ID = toMapPinAfterUpdate.ID,
                    Direction = "N",
                    BeaconID = telemetry.BeaconID,
                    BeaconName = CNRugbyJunctionBeacon.Beacon.Name,
                    Railroad = CNRugbyJunctionBeacon.Subdivision.Railroad.Name,
                    Subdivision = CNRugbyJunctionBeacon.Subdivision.Name,
                    SubdivisionID = CNRugbyJunctionBeacon.Subdivision.ID,
                    Latitude = CNRugbyJunctionBeacon.Latitude,
                    Longitude = CNRugbyJunctionBeacon.Longitude,
                    Milepost = CNRugbyJunctionBeacon.Milepost,
                    Moving = telemetry.Moving,
                    CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(-16),
                    LastUpdate = _timeProviderMock.Object.UtcNow.AddMinutes(-16),
                    Addresses =
                    [
                        new AddressDTO
                        {
                            AddressID = addressID21348,
                            Source = SourceEnum.DPU
                        },
                        new AddressDTO
                        {
                            AddressID = addressID47622,
                            Source = SourceEnum.DPU
                        }
                    ],
                }
             };

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID))
                .ReturnsAsync((MapPin?)null);
            _mapPinRepositoryMock.Setup(r => r.GetByTrainIdAsync(telemetry.TrainID.Value, MapPinService.TIME_THRESHOLD_DPU_MINUTES))
                .ReturnsAsync(fromMapPin);
            _beaconRailroadServiceMock.Setup(b => b.GetByIdAsync(telemetry.BeaconID, fromMapPin.SubdivisionId))
                .ReturnsAsync(CNRugbyJunctionBeacon);
            _beaconRailroadServiceMock.Setup(b => b.GetByIdAsync(fromMapPin.BeaconID, fromMapPin.SubdivisionId))
                .ReturnsAsync(CNRugbyJunctionBeacon);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(toMapPinBeforeUpdate, It.IsAny<DateTime>()))
                .ReturnsAsync(toMapPinAfterUpdate);

            var minutesAgo = telemetry.CreatedAt.AddMinutes(-DpuAntiPingPongRule.TIME_WINDOW_MINUTES);
            _telemetryRepositoryMock.Setup(r => r.GetRecentsForTrainWithinTimeOffsetAsync(trainID, CNSussexBeacon.Subdivision.RailroadID, minutesAgo))
                .ReturnsAsync(
                [
                    new Telemetry
                    {
                        TrainID = trainID,
                        AddressID = addressID21348,
                        BeaconID = CNSussexBeacon.BeaconID,
                        Discarded = false,
                        CreatedAt = telemetry.CreatedAt
                    },
                    new Telemetry
                    {
                        TrainID = trainID,
                        AddressID = addressID47622,
                        BeaconID = CNSussexBeacon.BeaconID,
                        DiscardReason = DpuAntiPingPongRule.DISCARD_REASON,
                        Discarded = true,
                        CreatedAt = telemetry.CreatedAt.AddMinutes(-3)
                    },
                    new Telemetry
                    {
                        TrainID = trainID,
                        AddressID = addressID47622,
                        BeaconID = CNRugbyJunctionBeacon.BeaconID,
                        Discarded = false,
                        CreatedAt = telemetry.CreatedAt.AddMinutes(-5)
                    },
                    new Telemetry
                    {
                        TrainID = trainID,
                        AddressID = addressID47622,
                        BeaconID = CNSussexBeacon.BeaconID,
                        Discarded = false,
                        CreatedAt = telemetry.CreatedAt
                    },
                ]);

            _clientProxyMock.Setup(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate, toMapPinObjects, default))
                    .Returns(Task.CompletedTask);
            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(It.IsAny<MapPin>(), It.IsAny<DateTime>()), Times.Never);

            _clientProxyMock?.Verify(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate,
                It.IsAny<object[]>(),
                default), Times.Never);
        }

        /// <summary>
        /// Test ensures that if a matching DPU train ID is found on an existing map pin from the same railroad 
        /// but with a different DPU address ID and the timestamps are far apart, the new DPU telemetry is 
        /// not merged but instead creates a new map pin.
        /// </summary>
        [TestMethod]
        public async Task UpsertMapPin_DPUsSameTrainIdTooFarApartShouldNotBeCombined()
        {
            // Arrange
            var CNJunctionCity = TestData.CN_JunctionCity_WI(_timeProviderMock.Object.UtcNow);
            var junctionCityAddressID30886 = 30886;
            var junctionCityAddressID42509 = 42509;

            var CNWaukesha = TestData.CN_Waukesha_WI(_timeProviderMock.Object.UtcNow);
            var waukeshaAddressID30629 = 30629;

            var trainID = 123;

            var telemetry = new Telemetry
            {
                BeaconID = CNWaukesha.BeaconID,
                Beacon = new Beacon
                {
                    ID = CNWaukesha.BeaconID,
                    Name = CNWaukesha.Beacon.Name,
                    BeaconRailroads = new[]
                    {
                        CNWaukesha
                    },
                },
                AddressID = waukeshaAddressID30629,
                TrainID = trainID,
                Source = SourceEnum.DPU,
                Moving = true,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow
            };

            var fromMapPin = new MapPin
            {
                ID = 234,
                BeaconID = CNJunctionCity.BeaconID,
                SubdivisionId = CNJunctionCity.Subdivision.ID,
                Direction = "W",
                CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(-30),
                LastUpdate = _timeProviderMock.Object.UtcNow.AddMinutes(-30),
                BeaconRailroad = CNJunctionCity,
                Moving = telemetry.Moving,
                CreatedRailroadID = CNJunctionCity.Subdivision.RailroadID,
                Addresses =
                [
                    new Address
                    {
                        AddressID = junctionCityAddressID30886,
                        DpuTrainID = trainID,
                        Source = SourceEnum.DPU,
                        CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(-30),
                        LastUpdate = _timeProviderMock.Object.UtcNow.AddMinutes(-30)
                    },
                    new Address
                    {
                        AddressID = junctionCityAddressID42509,
                        DpuTrainID = trainID,
                        Source = SourceEnum.DPU,
                        CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(-40),
                        LastUpdate = _timeProviderMock.Object.UtcNow.AddMinutes(-40)
                    }
                ],
            };

            var toMapPinBeforeUpdate = new MapPin
            {
                ID = 0,
                BeaconID = CNWaukesha.BeaconID,
                SubdivisionId = CNWaukesha.Subdivision.ID,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow,
                BeaconRailroad = CNWaukesha,
                Moving = telemetry.Moving,
                CreatedRailroadID = CNWaukesha.Subdivision.RailroadID,
                Addresses =
                    [
                        new Address
                        {
                            AddressID = waukeshaAddressID30629,
                            DpuTrainID = trainID,
                            Source = SourceEnum.DPU,
                            CreatedAt = _timeProviderMock.Object.UtcNow,
                            LastUpdate = _timeProviderMock.Object.UtcNow
                        }
                        // No Junction City addresses should be included.
                    ],
            };

            var toMapPinAfterUpdate = toMapPinBeforeUpdate.Clone();
            toMapPinAfterUpdate.ID = 936; // ID returned after insert.

            var toMapPinObjects = new object[]
            {
                new MapPinDTO
                {
                    ID = toMapPinAfterUpdate.ID,
                    Direction = null,
                    BeaconID = telemetry.BeaconID,
                    BeaconName = CNWaukesha.Beacon.Name,
                    Railroad = CNWaukesha.Subdivision.Railroad.Name,
                    Subdivision = CNWaukesha.Subdivision.Name,
                    SubdivisionID = CNWaukesha.Subdivision.ID,
                    Latitude = CNWaukesha.Latitude,
                    Longitude = CNWaukesha.Longitude,
                    Milepost = CNWaukesha.Milepost,
                    Moving = telemetry.Moving,
                    CreatedAt = _timeProviderMock.Object.UtcNow,
                    LastUpdate = _timeProviderMock.Object.UtcNow,
                    Addresses =
                    [
                        new AddressDTO
                        {
                            AddressID = waukeshaAddressID30629,
                            Source = SourceEnum.DPU
                        }
                        // No Junction City addresses should be included.
                    ],
                }
             };

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID))
                .ReturnsAsync((MapPin?)null);
            _mapPinRepositoryMock.Setup(r => r.GetByTrainIdAsync(telemetry.TrainID.Value, MapPinService.TIME_THRESHOLD_DPU_MINUTES))
                .ReturnsAsync(fromMapPin);
            _beaconRailroadServiceMock.Setup(b => b.GetByIdAsync(telemetry.BeaconID, fromMapPin.SubdivisionId))
                .ReturnsAsync(CNWaukesha);
            _beaconRailroadServiceMock.Setup(b => b.GetByIdAsync(fromMapPin.BeaconID, fromMapPin.SubdivisionId))
                .ReturnsAsync(CNJunctionCity);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(toMapPinBeforeUpdate, It.IsAny<DateTime>()))
                .ReturnsAsync(toMapPinAfterUpdate);

            var minutesAgo = telemetry.CreatedAt.AddMinutes(-DpuAntiPingPongRule.TIME_WINDOW_MINUTES);
            _telemetryRepositoryMock.Setup(r => r.GetRecentsForTrainWithinTimeOffsetAsync(trainID, CNJunctionCity.Subdivision.RailroadID, minutesAgo))
                .ReturnsAsync(
                [
                    new Telemetry
                    {
                        TrainID = trainID,
                        AddressID = junctionCityAddressID30886,
                        BeaconID = CNJunctionCity.BeaconID,
                        Discarded = false,
                        CreatedAt = telemetry.CreatedAt.AddMinutes(-30)
                    },
                    new Telemetry
                    {
                        TrainID = trainID,
                        AddressID = junctionCityAddressID30886,
                        BeaconID = CNJunctionCity.BeaconID,
                        Discarded = false,
                        CreatedAt = telemetry.CreatedAt.AddMinutes(-31)
                    }
                ]);

            _clientProxyMock.Setup(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate, toMapPinObjects, default))
                    .Returns(Task.CompletedTask);
            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(toMapPinBeforeUpdate, It.IsAny<DateTime>()), Times.Once);

            _clientProxyMock?.Verify(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate,
                It.Is<object[]>(args => args[0].Equals(toMapPinObjects[0])),
                default), Times.Once);
        }

        /// <summary>
        /// Train-only DPU fallback should not merge same-beacon telemetry when the previous
        /// train-only candidate is too old; a new logical map pin should be created instead.
        /// </summary>
        [TestMethod]
        public async Task UpsertMapPin_DPUsSameTrainIDLargeTimeGapSameBeacon_CreatesNewMapPin()
        {
            // Arrange
            var CNSussexBeacon = TestData.CN_Sussex_WI(_timeProviderMock.Object.UtcNow);
            var fromAddressID = 31555;
            var toAddressID = 58564;
            var trainID = 243;

            var telemetry = new Telemetry
            {
                BeaconID = CNSussexBeacon.BeaconID,
                Beacon = new Beacon
                {
                    ID = CNSussexBeacon.BeaconID,
                    Name = CNSussexBeacon.Beacon.Name,
                    BeaconRailroads = new[]
                    {
                        CNSussexBeacon
                    },
                },
                AddressID = toAddressID,
                TrainID = trainID,
                Source = SourceEnum.DPU,
                Moving = true,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow
            };

            var fromMapPin = new MapPin
            {
                ID = 234,
                BeaconID = CNSussexBeacon.BeaconID,
                SubdivisionId = CNSussexBeacon.Subdivision.ID,
                CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(-60),
                LastUpdate = _timeProviderMock.Object.UtcNow.AddMinutes(-60),
                BeaconRailroad = CNSussexBeacon,
                Moving = telemetry.Moving,
                CreatedRailroadID = CNSussexBeacon.Subdivision.RailroadID,
                Addresses =
                    [
                        new Address
                        {
                            AddressID = fromAddressID,
                            DpuTrainID = trainID,
                            Source = SourceEnum.DPU,
                            CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(-60),
                            LastUpdate = _timeProviderMock.Object.UtcNow.AddMinutes(-60)
                        }
                    ],
            };

            var createdMapPinBeforeInsert = new MapPin
            {
                ID = 0,
                BeaconID = CNSussexBeacon.BeaconID,
                SubdivisionId = CNSussexBeacon.Subdivision.ID,
                Direction = null,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow,
                BeaconRailroad = CNSussexBeacon,
                Moving = telemetry.Moving,
                CreatedRailroadID = CNSussexBeacon.Subdivision.RailroadID,
                Addresses =
                [
                    new Address
                    {
                        AddressID = toAddressID,
                        DpuTrainID = trainID,
                        Source = SourceEnum.DPU,
                        CreatedAt = telemetry.CreatedAt,
                        LastUpdate = telemetry.LastUpdate
                    }
                ],
            };

            var createdMapPinAfterInsert = createdMapPinBeforeInsert.Clone();
            createdMapPinAfterInsert.ID = 936;

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID))
                .ReturnsAsync((MapPin?)null);
            _mapPinRepositoryMock.Setup(r => r.GetByTrainIdAsync(telemetry.TrainID.Value, MapPinService.TIME_THRESHOLD_DPU_MINUTES))
                .ReturnsAsync(fromMapPin);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(It.IsAny<MapPin>(), It.IsAny<DateTime>()))
                .ReturnsAsync(createdMapPinAfterInsert);

            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);
            _clientProxyMock.Setup(proxy => proxy.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), default))
                .Returns(Task.CompletedTask);

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(
                It.Is<MapPin>(mp =>
                    mp.ID == 0 &&
                    mp.BeaconID == telemetry.BeaconID &&
                    mp.Direction == null &&
                    mp.Addresses.Count == 1 &&
                    mp.Addresses.Any(a => a.AddressID == toAddressID && a.DpuTrainID == trainID) &&
                    !mp.Addresses.Any(a => a.AddressID == fromAddressID)),
                telemetry.LastUpdate), Times.Once);
        }

        /// <summary>
        /// Verifies that if a matching DPU train ID is found on an existing map pin from the same railroad 
        /// but the beacons are far enough apart to trigger a train speed sanity check failure, the new 
        /// telemetry is discarded and map pin is not updated.
        /// </summary>
        [TestMethod]
        [Ignore("Temporarily ignore. Train speed rule triggers but does not discard telemetry due to Neenah antenna overreach issue.")]
        public async Task UpsertMapPin_TrainSpeedSanityCheckFail()
        {
            // Arrange
            var CNOshkoshBeaconRailroad = TestData.CN_Oshkosh_WI(_timeProviderMock.Object.UtcNow.AddMinutes(-30));
            var fromAddressID = 92342;
            var fromTrainID = 123;

            var CNRugbyJunctionBeaconRailroad = TestData.CN_RugbyJunction_WI(_timeProviderMock.Object.UtcNow);
            var toAddressID = 23424;
            var toTrainID = 123;

            var telemetry = new Telemetry
            {
                BeaconID = CNRugbyJunctionBeaconRailroad.BeaconID,
                Beacon = new Beacon
                {
                    ID = CNRugbyJunctionBeaconRailroad.BeaconID,
                    Name = CNRugbyJunctionBeaconRailroad.Beacon.Name,
                    BeaconRailroads = new[]
                    {
                        CNRugbyJunctionBeaconRailroad
                    },
                },
                AddressID = toAddressID,
                TrainID = toTrainID,
                Source = SourceEnum.DPU,
                Moving = true,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow
            };

            var telemetryDiscarded = new Telemetry
            {
                BeaconID = CNRugbyJunctionBeaconRailroad.BeaconID,
                Beacon = new Beacon
                {
                    ID = CNRugbyJunctionBeaconRailroad.BeaconID,
                    Name = CNRugbyJunctionBeaconRailroad.Beacon.Name,
                    BeaconRailroads = new[]
                    {
                        CNRugbyJunctionBeaconRailroad
                    },
                },
                AddressID = toAddressID,
                TrainID = toTrainID,
                Source = SourceEnum.DPU,
                Moving = true,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow
            };

            var fromMapPin = new MapPin
            {
                ID = 234,
                BeaconID = CNOshkoshBeaconRailroad.BeaconID,
                SubdivisionId = CNOshkoshBeaconRailroad.Subdivision.ID,
                CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(-2),
                LastUpdate = _timeProviderMock.Object.UtcNow.AddMinutes(-2),
                BeaconRailroad = CNOshkoshBeaconRailroad,
                Moving = telemetry.Moving,
                CreatedRailroadID = CNOshkoshBeaconRailroad.Subdivision.RailroadID,
                Addresses =
                    [
                        new Address
                        {
                            AddressID = fromAddressID,
                            DpuTrainID = fromTrainID,
                            Source = SourceEnum.DPU,
                            CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(-2),
                            LastUpdate = _timeProviderMock.Object.UtcNow.AddMinutes(-2)
                        }
                    ],
            };

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID))
                .ReturnsAsync((MapPin?)null);
            _mapPinRepositoryMock.Setup(r => r.GetByTrainIdAsync(telemetry.TrainID.Value, MapPinService.TIME_THRESHOLD_DPU_MINUTES))
                .ReturnsAsync(fromMapPin);
            _beaconRailroadServiceMock.Setup(b => b.GetByIdAsync(telemetry.BeaconID, fromMapPin.SubdivisionId))
                .ReturnsAsync(CNRugbyJunctionBeaconRailroad);
            _beaconRailroadServiceMock.Setup(b => b.GetByIdAsync(fromMapPin.BeaconID, fromMapPin.SubdivisionId))
                .ReturnsAsync(CNOshkoshBeaconRailroad);

            // Real rule engine will evaluate if the train speed is realistic
            // The test data should trigger TrainSpeedSanityCheckRule if beacons are far enough apart

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert
            _telemetryRepositoryMock.Verify(
                r => r.UpdateAsync(It.Is<Telemetry>(t =>
                    t.Discarded == true &&
                    t.DiscardReason == TrainSpeedSanityCheckRule.DISCARD_REASON)),
                Times.Once);

            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(It.IsAny<MapPin>(), It.IsAny<DateTime>()), Times.Never);
            _clientProxyMock?.Verify(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate,
                It.IsAny<object[]>(),
                default),
                Times.Never);
        }

        /// <summary>
        /// When a new map pin is upserted at a single-track (non-multi-track) beacon and another map pin
        /// already exists at that same beacon, all addresses should be merged into the new map pin and the
        /// duplicate should be deleted.
        /// </summary>
        [TestMethod]
        public async Task UpsertMapPin_SingleTrackBeacon_MergesExistingMapPinsIntoOne()
        {
            // Arrange
            var wsOrBeacon = TestData.WSOR_RugbyJunction_WI(_timeProviderMock.Object.UtcNow); // MultipleTracks = false

            var telemetry = new Telemetry
            {
                BeaconID = wsOrBeacon.BeaconID,
                Beacon = new Beacon
                {
                    ID = wsOrBeacon.BeaconID,
                    Name = wsOrBeacon.Beacon.Name,
                    BeaconRailroads = [wsOrBeacon]
                },
                AddressID = 11111,
                Source = SourceEnum.HOT,
                Moving = true,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow
            };

            var newMapPinBeforeInsert = new MapPin
            {
                ID = 0,
                BeaconID = wsOrBeacon.BeaconID,
                SubdivisionId = wsOrBeacon.Subdivision.ID,
                CreatedRailroadID = wsOrBeacon.Subdivision.RailroadID,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow,
                BeaconRailroad = wsOrBeacon,
                Moving = telemetry.Moving,
                Addresses =
                [
                    new Address
                    {
                        AddressID = 11111,
                        Source = SourceEnum.HOT,
                        CreatedAt = _timeProviderMock.Object.UtcNow,
                        LastUpdate = _timeProviderMock.Object.UtcNow
                    }
                ]
            };

            var newMapPinAfterInsert = newMapPinBeforeInsert.Clone();
            newMapPinAfterInsert.ID = 456;

            // Pre-existing map pin at the same single-track beacon with a different address
            var existingDuplicateAddress = new Address
            {
                ID = 9,
                AddressID = 22222,
                Source = SourceEnum.EOT,
                CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(-5),
                LastUpdate = _timeProviderMock.Object.UtcNow.AddMinutes(-5)
            };

            var existingDuplicateMapPin = new MapPin
            {
                ID = 123,
                BeaconID = wsOrBeacon.BeaconID,
                SubdivisionId = wsOrBeacon.Subdivision.ID,
                CreatedRailroadID = wsOrBeacon.Subdivision.RailroadID,
                CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(-5),
                LastUpdate = _timeProviderMock.Object.UtcNow.AddMinutes(-5),
                BeaconRailroad = wsOrBeacon,
                Addresses = [existingDuplicateAddress]
            };

            // After merge: the new map pin should contain both addresses
            var mergedMapPin = newMapPinAfterInsert.Clone();
            mergedMapPin.Addresses.Add(existingDuplicateAddress);

            var mergedMapPinAfterUpsert = mergedMapPin.Clone();

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID))
                .ReturnsAsync((MapPin?)null);
            _mapPinRepositoryMock.Setup(r => r.GetByTimeThreshold(wsOrBeacon.BeaconID, wsOrBeacon.SubdivisionID, MapPinService.TIME_THRESHOLD_MINUTES))
                .ReturnsAsync((MapPin?)null);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(newMapPinBeforeInsert, telemetry.LastUpdate))
                .ReturnsAsync(newMapPinAfterInsert);
            _mapPinRepositoryMock.Setup(r => r.GetAllByBeaconAsync(wsOrBeacon.BeaconID, wsOrBeacon.SubdivisionID, It.IsAny<int>()))
                .ReturnsAsync([newMapPinAfterInsert, existingDuplicateMapPin]);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(mergedMapPin, telemetry.LastUpdate))
                .ReturnsAsync(mergedMapPinAfterUpsert);
            _mapPinRepositoryMock.Setup(r => r.DeleteAsync(existingDuplicateMapPin.ID))
                .ReturnsAsync(true);

            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert – duplicate was deleted and merge upsert was called
            _mapPinRepositoryMock.Verify(r => r.DeleteAsync(existingDuplicateMapPin.ID), Times.Once);
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(
                It.Is<MapPin>(mp => mp.ID == newMapPinAfterInsert.ID &&
                                    mp.Addresses.Any(a => a.AddressID == 11111) &&
                                    mp.Addresses.Any(a => a.AddressID == 22222)),
                telemetry.LastUpdate), Times.Once);
        }

        /// <summary>
        /// Two HOT addresses at the same single-track beacon: new HOT arrives, no exact-match and
        /// no GetByTimeThreshold hit, but GetAllByBeaconAsync returns an existing pin.
        /// The new pin should have the existing pin's address absorbed and the old pin deleted.
        /// </summary>
        [TestMethod]
        public async Task UpsertMapPin_MergeHOTWithHOT_ViaBeaconScan()
        {
            // Arrange
            var beacon = TestData.CN_Sussex_WI(_timeProviderMock.Object.UtcNow);

            var existingAddressID = 11111;
            var newAddressID = 22222;

            var telemetry = new Telemetry
            {
                BeaconID = beacon.BeaconID,
                Beacon = new Beacon
                {
                    ID = beacon.BeaconID,
                    Name = beacon.Beacon.Name,
                    BeaconRailroads = [beacon]
                },
                AddressID = newAddressID,
                Source = SourceEnum.HOT,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow
            };

            var existingPin = new MapPin
            {
                ID = 50,
                BeaconID = beacon.BeaconID,
                SubdivisionId = beacon.SubdivisionID,
                BeaconRailroad = beacon,
                CreatedRailroadID = beacon.Subdivision.RailroadID,
                CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(-1),
                LastUpdate = _timeProviderMock.Object.UtcNow.AddMinutes(-1),
                Addresses = [new Address { AddressID = existingAddressID, Source = SourceEnum.HOT }]
            };

            var newPinBeforeInsert = new MapPin
            {
                ID = 0,
                BeaconID = beacon.BeaconID,
                SubdivisionId = beacon.SubdivisionID,
                BeaconRailroad = beacon,
                CreatedRailroadID = beacon.Subdivision.RailroadID,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow,
                Addresses = [new Address { AddressID = newAddressID, Source = SourceEnum.HOT, CreatedAt = _timeProviderMock.Object.UtcNow, LastUpdate = _timeProviderMock.Object.UtcNow }]
            };

            var newPinAfterInsert = newPinBeforeInsert.Clone();
            newPinAfterInsert.ID = 99;

            var mergedPin = newPinAfterInsert.Clone();
            mergedPin.Addresses.Add(existingPin.Addresses.First());

            var mergedPinAfterUpsert = mergedPin.Clone();

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(newAddressID))
                .ReturnsAsync((MapPin?)null);
            _mapPinRepositoryMock.Setup(r => r.GetByTimeThreshold(beacon.BeaconID, beacon.SubdivisionID, MapPinService.TIME_THRESHOLD_MINUTES))
                .ReturnsAsync((MapPin?)null);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(newPinBeforeInsert, telemetry.LastUpdate))
                .ReturnsAsync(newPinAfterInsert);
            _mapPinRepositoryMock.Setup(r => r.GetAllByBeaconAsync(beacon.BeaconID, beacon.SubdivisionID, MapPinService.TIME_THRESHOLD_MINUTES))
                .ReturnsAsync([newPinAfterInsert, existingPin]);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(mergedPin, telemetry.LastUpdate))
                .ReturnsAsync(mergedPinAfterUpsert);
            _mapPinRepositoryMock.Setup(r => r.DeleteAsync(existingPin.ID))
                .ReturnsAsync(true);

            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert – existing pin absorbed and deleted
            _mapPinRepositoryMock.Verify(r => r.DeleteAsync(existingPin.ID), Times.Once);
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(
                It.Is<MapPin>(mp =>
                    mp.ID == newPinAfterInsert.ID &&
                    mp.Addresses.Any(a => a.AddressID == existingAddressID) &&
                    mp.Addresses.Any(a => a.AddressID == newAddressID)),
                telemetry.LastUpdate), Times.Once);
        }

        /// <summary>
        /// EOT arrives at single-track beacon where existing pin has HOT and no EOT.
        /// The EOT should merge into the surviving pin and old pin should be deleted.
        /// </summary>
        [TestMethod]
        public async Task UpsertMapPin_MergeEOTWithHOT_ViaBeaconScan()
        {
            // Arrange
            var beacon = TestData.CN_Sussex_WI(_timeProviderMock.Object.UtcNow);

            var existingAddressID = 11111;
            var newAddressID = 22222;

            var telemetry = new Telemetry
            {
                BeaconID = beacon.BeaconID,
                Beacon = new Beacon
                {
                    ID = beacon.BeaconID,
                    Name = beacon.Beacon.Name,
                    BeaconRailroads = [beacon]
                },
                AddressID = newAddressID,
                Source = SourceEnum.EOT,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow
            };

            var existingPin = new MapPin
            {
                ID = 50,
                BeaconID = beacon.BeaconID,
                SubdivisionId = beacon.SubdivisionID,
                BeaconRailroad = beacon,
                CreatedRailroadID = beacon.Subdivision.RailroadID,
                CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(-1),
                LastUpdate = _timeProviderMock.Object.UtcNow.AddMinutes(-1),
                Addresses = [new Address { AddressID = existingAddressID, Source = SourceEnum.HOT }]
            };

            var newPinBeforeInsert = new MapPin
            {
                ID = 0,
                BeaconID = beacon.BeaconID,
                SubdivisionId = beacon.SubdivisionID,
                BeaconRailroad = beacon,
                CreatedRailroadID = beacon.Subdivision.RailroadID,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow,
                Addresses = [new Address { AddressID = newAddressID, Source = SourceEnum.EOT, CreatedAt = _timeProviderMock.Object.UtcNow, LastUpdate = _timeProviderMock.Object.UtcNow }]
            };

            var newPinAfterInsert = newPinBeforeInsert.Clone();
            newPinAfterInsert.ID = 99;

            var mergedPin = newPinAfterInsert.Clone();
            mergedPin.Addresses.Add(existingPin.Addresses.First());

            var mergedPinAfterUpsert = mergedPin.Clone();

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(newAddressID))
                .ReturnsAsync((MapPin?)null);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(newPinBeforeInsert, telemetry.LastUpdate))
                .ReturnsAsync(newPinAfterInsert);
            _mapPinRepositoryMock.Setup(r => r.GetAllByBeaconAsync(beacon.BeaconID, beacon.SubdivisionID, MapPinService.TIME_THRESHOLD_MINUTES))
                .ReturnsAsync([newPinAfterInsert, existingPin]);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(mergedPin, telemetry.LastUpdate))
                .ReturnsAsync(mergedPinAfterUpsert);
            _mapPinRepositoryMock.Setup(r => r.DeleteAsync(existingPin.ID))
                .ReturnsAsync(true);

            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert
            _mapPinRepositoryMock.Verify(r => r.DeleteAsync(existingPin.ID), Times.Once);
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(
                It.Is<MapPin>(mp =>
                    mp.ID == newPinAfterInsert.ID &&
                    mp.Addresses.Any(a => a.AddressID == existingAddressID && a.Source == SourceEnum.HOT) &&
                    mp.Addresses.Any(a => a.AddressID == newAddressID && a.Source == SourceEnum.EOT)),
                telemetry.LastUpdate), Times.Once);
        }

        /// <summary>
        /// EOT arrives at a single-track beacon where an existing pin already has EOT.
        /// The addresses should still be merged into one surviving pin and the duplicate pin deleted.
        /// </summary>
        [TestMethod]
        public async Task UpsertMapPin_MergeEOTWithEOT_ViaBeaconScan()
        {
            // Arrange
            var beacon = TestData.CN_Sussex_WI(_timeProviderMock.Object.UtcNow);

            var existingAddressID = 11111;
            var newAddressID = 22222;

            var telemetry = new Telemetry
            {
                BeaconID = beacon.BeaconID,
                Beacon = new Beacon
                {
                    ID = beacon.BeaconID,
                    Name = beacon.Beacon.Name,
                    BeaconRailroads = [beacon]
                },
                AddressID = newAddressID,
                Source = SourceEnum.EOT,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow
            };

            var existingPin = new MapPin
            {
                ID = 50,
                BeaconID = beacon.BeaconID,
                SubdivisionId = beacon.SubdivisionID,
                BeaconRailroad = beacon,
                CreatedRailroadID = beacon.Subdivision.RailroadID,
                CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(-1),
                LastUpdate = _timeProviderMock.Object.UtcNow.AddMinutes(-1),
                Addresses = [new Address { AddressID = existingAddressID, Source = SourceEnum.EOT, DpuTrainID = null }]
            };

            var newPinBeforeInsert = new MapPin
            {
                ID = 0,
                BeaconID = beacon.BeaconID,
                SubdivisionId = beacon.SubdivisionID,
                BeaconRailroad = beacon,
                CreatedRailroadID = beacon.Subdivision.RailroadID,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow,
                Addresses = [new Address { AddressID = newAddressID, Source = SourceEnum.EOT, CreatedAt = _timeProviderMock.Object.UtcNow, LastUpdate = _timeProviderMock.Object.UtcNow }]
            };

            var newPinAfterInsert = newPinBeforeInsert.Clone();
            newPinAfterInsert.ID = 99;

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(newAddressID))
                .ReturnsAsync((MapPin?)null);
            _mapPinRepositoryMock.Setup(r => r.GetByTimeThreshold(beacon.BeaconID, beacon.SubdivisionID, MapPinService.TIME_THRESHOLD_MINUTES))
                .ReturnsAsync((MapPin?)null);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(newPinBeforeInsert, telemetry.LastUpdate))
                .ReturnsAsync(newPinAfterInsert);
            _mapPinRepositoryMock.Setup(r => r.GetAllByBeaconAsync(beacon.BeaconID, beacon.SubdivisionID, MapPinService.TIME_THRESHOLD_MINUTES))
                .ReturnsAsync([newPinAfterInsert, existingPin]);
            var mergedPin = newPinAfterInsert.Clone();
            mergedPin.Addresses.Add(existingPin.Addresses.First());

            var mergedPinAfterUpsert = mergedPin.Clone();

            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(mergedPin, telemetry.LastUpdate))
                .ReturnsAsync(mergedPinAfterUpsert);
            _mapPinRepositoryMock.Setup(r => r.DeleteAsync(existingPin.ID))
                .ReturnsAsync(true);

            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert – duplicate old pin deleted and merged upsert performed
            _telemetryRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Telemetry>()), Times.Never);
            _mapPinRepositoryMock.Verify(r => r.DeleteAsync(existingPin.ID), Times.Once);
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(
                It.Is<MapPin>(mp =>
                    mp.ID == newPinAfterInsert.ID &&
                    mp.Addresses.Any(a => a.AddressID == existingAddressID && a.Source == SourceEnum.EOT) &&
                    mp.Addresses.Any(a => a.AddressID == newAddressID && a.Source == SourceEnum.EOT)),
                telemetry.LastUpdate), Times.Once);
        }

        /// <summary>
        /// DPU arrives at single-track beacon; existing pin has HOT but no DPU.
        /// The DPU address should be absorbed into the surviving (new) pin.
        /// </summary>
        [TestMethod]
        public async Task UpsertMapPin_MergeDPUIntoExistingHOT_ViaBeaconScan()
        {
            // Arrange
            var beacon = TestData.CN_Sussex_WI(_timeProviderMock.Object.UtcNow);

            var existingAddressID = 11111;
            var newAddressID = 22222;
            var trainID = 777;

            var telemetry = new Telemetry
            {
                BeaconID = beacon.BeaconID,
                Beacon = new Beacon
                {
                    ID = beacon.BeaconID,
                    Name = beacon.Beacon.Name,
                    BeaconRailroads = [beacon]
                },
                AddressID = newAddressID,
                TrainID = trainID,
                Source = SourceEnum.DPU,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow
            };

            var existingPin = new MapPin
            {
                ID = 50,
                BeaconID = beacon.BeaconID,
                SubdivisionId = beacon.SubdivisionID,
                BeaconRailroad = beacon,
                CreatedRailroadID = beacon.Subdivision.RailroadID,
                CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(-1),
                LastUpdate = _timeProviderMock.Object.UtcNow.AddMinutes(-1),
                Addresses = [new Address { AddressID = existingAddressID, Source = SourceEnum.HOT }]
            };

            var newPinBeforeInsert = new MapPin
            {
                ID = 0,
                BeaconID = beacon.BeaconID,
                SubdivisionId = beacon.SubdivisionID,
                BeaconRailroad = beacon,
                CreatedRailroadID = beacon.Subdivision.RailroadID,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow,
                Addresses = [new Address { AddressID = newAddressID, DpuTrainID = trainID, Source = SourceEnum.DPU, CreatedAt = _timeProviderMock.Object.UtcNow, LastUpdate = _timeProviderMock.Object.UtcNow }]
            };

            var newPinAfterInsert = newPinBeforeInsert.Clone();
            newPinAfterInsert.ID = 99;

            var mergedPin = newPinAfterInsert.Clone();
            mergedPin.Addresses.Add(existingPin.Addresses.First());

            var mergedPinAfterUpsert = mergedPin.Clone();

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(newAddressID))
                .ReturnsAsync((MapPin?)null);
            _mapPinRepositoryMock.Setup(r => r.GetByTrainIdAsync(trainID, MapPinService.TIME_THRESHOLD_DPU_MINUTES))
                .ReturnsAsync((MapPin?)null);
            _mapPinRepositoryMock.Setup(r => r.GetByTimeThreshold(beacon.BeaconID, beacon.SubdivisionID, MapPinService.TIME_THRESHOLD_MINUTES))
                .ReturnsAsync((MapPin?)null);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(newPinBeforeInsert, telemetry.LastUpdate))
                .ReturnsAsync(newPinAfterInsert);
            _mapPinRepositoryMock.Setup(r => r.GetAllByBeaconAsync(beacon.BeaconID, beacon.SubdivisionID, MapPinService.TIME_THRESHOLD_MINUTES))
                .ReturnsAsync([newPinAfterInsert, existingPin]);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(mergedPin, telemetry.LastUpdate))
                .ReturnsAsync(mergedPinAfterUpsert);
            _mapPinRepositoryMock.Setup(r => r.DeleteAsync(existingPin.ID))
                .ReturnsAsync(true);

            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert – existing HOT absorbed into new DPU pin, old pin deleted
            _mapPinRepositoryMock.Verify(r => r.DeleteAsync(existingPin.ID), Times.Once);
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(
                It.Is<MapPin>(mp =>
                    mp.ID == newPinAfterInsert.ID &&
                    mp.Addresses.Any(a => a.AddressID == existingAddressID && a.Source == SourceEnum.HOT) &&
                    mp.Addresses.Any(a => a.AddressID == newAddressID && a.Source == SourceEnum.DPU)),
                telemetry.LastUpdate), Times.Once);
        }


        private static class TestData
        {
            public static Beacon Hartford_WI()
            {
                return new Beacon
                {
                    ID = 4,
                    OwnerID = 1,
                    Name = "Hartford",
                    CreatedAt = DateTime.UtcNow,
                    LastUpdate = DateTime.UtcNow
                };
            }

            public static Beacon JunctionCity_WI()
            {
                return new Beacon
                {
                    ID = 1,
                    OwnerID = 17,
                    Name = "Junction City",
                    CreatedAt = DateTime.UtcNow,
                    LastUpdate = DateTime.UtcNow
                };
            }

            public static Beacon Oshkosh_WI()
            {
                return new Beacon
                {
                    ID = 2,
                    OwnerID = 7,
                    Name = "Oshkosh",
                    CreatedAt = DateTime.UtcNow,
                    LastUpdate = DateTime.UtcNow
                };
            }

            public static Beacon RugbyJunction_WI()
            {
                return new Beacon
                {
                    ID = 1,
                    OwnerID = 1,
                    Name = "Rugby Junction",
                    CreatedAt = DateTime.UtcNow,
                    LastUpdate = DateTime.UtcNow
                };
            }

            public static Beacon Sussex_WI()
            {
                return new Beacon
                {
                    ID = 2,
                    OwnerID = 1,
                    Name = "Sussex",
                    CreatedAt = DateTime.UtcNow,
                    LastUpdate = DateTime.UtcNow
                };
            }

            public static Beacon Waukesha_WI()
            {
                return new Beacon
                {
                    ID = 12,
                    OwnerID = 1,
                    Name = "Waukesha",
                    CreatedAt = DateTime.UtcNow,
                    LastUpdate = DateTime.UtcNow
                };
            }

            public static BeaconRailroad CN_JunctionCity_WI(DateTime currentDateTime)
            {
                return new BeaconRailroad
                {
                    BeaconID = 10,
                    Beacon = TestData.JunctionCity_WI(),
                    SubdivisionID = 3,
                    Subdivision = TestData.CN_Superior(currentDateTime),
                    Direction = Direction.All,
                    Latitude = 44.589494,
                    Longitude = -89.761417,
                    Milepost = 260.0,
                    MultipleTracks = true,
                    CreatedAt = currentDateTime,
                    LastUpdate = currentDateTime
                };
            }

            public static BeaconRailroad CN_Oshkosh_WI(DateTime currentDateTime)
            {
                return new BeaconRailroad
                {
                    BeaconID = 10,
                    Beacon = TestData.Oshkosh_WI(),
                    SubdivisionID = 3,
                    Subdivision = TestData.CN_Neenah(currentDateTime),
                    Direction = Direction.NorthSouth,
                    Latitude = 44.02356,
                    Longitude = -88.531553,
                    Milepost = 174.0,
                    MultipleTracks = false,
                    CreatedAt = currentDateTime,
                    LastUpdate = currentDateTime
                };
            }

            public static BeaconRailroad CN_RugbyJunction_WI(DateTime currentDateTime)
            {
                return new BeaconRailroad
                {
                    BeaconID = 1,
                    Beacon = TestData.RugbyJunction_WI(),
                    SubdivisionID = 1,
                    Subdivision = TestData.CN_Waukesha(currentDateTime),
                    Direction = Direction.NorthSouth,
                    Latitude = 43.280958,
                    Longitude = -88.214682,
                    Milepost = 117.2,
                    MultipleTracks = true,
                    CreatedAt = currentDateTime,
                    LastUpdate = currentDateTime
                };
            }

            public static BeaconRailroad CN_Sussex_WI(DateTime currentDateTime)
            {
                return new BeaconRailroad
                {
                    BeaconID = 2,
                    Beacon = TestData.Sussex_WI(),
                    SubdivisionID = 1,
                    Subdivision = TestData.CN_Waukesha(currentDateTime),
                    Direction = Direction.NorthSouth,
                    Latitude = 43.159517,
                    Longitude = -88.200492,
                    Milepost = 108.6,
                    MultipleTracks = false,
                    CreatedAt = currentDateTime,
                    LastUpdate = currentDateTime
                };
            }

            public static BeaconRailroad CN_Waukesha_WI(DateTime currentDateTime)
            {
                return new BeaconRailroad
                {
                    BeaconID = 12,
                    Beacon = TestData.Waukesha_WI(),
                    SubdivisionID = 1,
                    Subdivision = TestData.CN_Waukesha(currentDateTime),
                    Direction = Direction.NorthSouth,
                    Latitude = 43.009341,
                    Longitude = -88.225405,
                    Milepost = 97.6,
                    MultipleTracks = true,
                    CreatedAt = currentDateTime,
                    LastUpdate = currentDateTime
                };
            }

            public static BeaconRailroad UP_Sussex_WI(DateTime currentDateTime)
            {
                return new BeaconRailroad
                {
                    BeaconID = 2,
                    Beacon = TestData.Sussex_WI(),
                    SubdivisionID = 5,
                    Subdivision = TestData.UP_Adams(currentDateTime),
                    Direction = Direction.EastWest,
                    Latitude = 43.137439,
                    Longitude = -88.209657,
                    Milepost = 306,
                    MultipleTracks = false,
                    CreatedAt = currentDateTime,
                    LastUpdate = currentDateTime
                };
            }

            public static BeaconRailroad WSOR_RugbyJunction_WI(DateTime currentDateTime)
            {
                return new BeaconRailroad
                {
                    BeaconID = 3,
                    Beacon = TestData.RugbyJunction_WI(),
                    SubdivisionID = 2,
                    Subdivision = TestData.WSOR_Milwaukee(currentDateTime),
                    Direction = Direction.NorthwestSoutheast,
                    Latitude = 43.280958,
                    Longitude = -88.213966,
                    Milepost = 112.16,
                    MultipleTracks = false,
                    CreatedAt = currentDateTime,
                    LastUpdate = currentDateTime
                };
            }

            public static BeaconRailroad WSOR_Hartford_WI(DateTime currentDateTime)
            {
                return new BeaconRailroad
                {
                    BeaconID = 4,
                    Beacon = TestData.Hartford_WI(),
                    SubdivisionID = 2,
                    Subdivision = TestData.WSOR_Milwaukee(currentDateTime),
                    Direction = Direction.NorthwestSoutheast,
                    Latitude = 43.319359,
                    Longitude = -88.367589,
                    Milepost = 121,
                    MultipleTracks = false,
                    CreatedAt = currentDateTime,
                    LastUpdate = currentDateTime
                };
            }

            public static Subdivision CN_Neenah(DateTime currentDateTime)
            {
                return
                        new Subdivision
                        {
                            ID = 3,
                            Name = "Neenah",
                            RailroadID = 1,
                            Railroad = new Railroad { ID = 1, Name = "CN" },
                            DpuCapable = true,
                            CreatedAt = currentDateTime,
                            LastUpdate = currentDateTime
                        };
            }

            public static Subdivision CN_Superior(DateTime currentDateTime)
            {
                return
                        new Subdivision
                        {
                            ID = 4,
                            Name = "Superior",
                            RailroadID = 1,
                            Railroad = new Railroad { ID = 1, Name = "CN" },
                            DpuCapable = true,
                            CreatedAt = currentDateTime,
                            LastUpdate = currentDateTime
                        };
            }

            public static Subdivision CN_Waukesha(DateTime currentDateTime)
            {
                return
                        new Subdivision
                        {
                            ID = 1,
                            Name = "Waukesha",
                            RailroadID = 1,
                            Railroad = new Railroad { ID = 1, Name = "CN" },
                            DpuCapable = true,
                            CreatedAt = currentDateTime,
                            LastUpdate = currentDateTime
                        };
            }

            public static Subdivision UP_Adams(DateTime currentDateTime)
            {
                return
                        new Subdivision
                        {
                            ID = 5,
                            Name = "Adams",
                            RailroadID = 4,
                            Railroad = new Railroad { ID = 4, Name = "UP" },
                            DpuCapable = true,
                            CreatedAt = currentDateTime,
                            LastUpdate = currentDateTime
                        };
            }

            public static Subdivision WSOR_Milwaukee(DateTime currentDateTime)
            {
                return new Subdivision
                {
                    ID = 2,
                    Name = "Milwaukee",
                    RailroadID = 2,
                    Railroad = new Railroad { ID = 2, Name = "WSOR" },
                    DpuCapable = false,
                    CreatedAt = currentDateTime,
                    LastUpdate = currentDateTime
                };
            }

            public static Subdivision WSOR_Horicon(DateTime currentDateTime)
            {
                return new Subdivision
                {
                    ID = 5,
                    Name = "Horicon",
                    RailroadID = 2,
                    Railroad = new Railroad { ID = 2, Name = "WSOR" },
                    DpuCapable = false,
                    CreatedAt = currentDateTime,
                    LastUpdate = currentDateTime
                };
            }
        }
    }
}

