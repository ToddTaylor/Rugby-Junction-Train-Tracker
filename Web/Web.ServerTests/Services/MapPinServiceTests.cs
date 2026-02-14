using AutoMapper;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Diagnostics.CodeAnalysis;
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
        private readonly Mock<ILogger<MapPinService>> _loggerMock = new();

        private MapPinService _service;
        private IMapper _mapper;
        private IMapPinRuleEngine _mapPinRuleEngine;
        private ITelemetryRuleEngine _telemetryRuleEngine;

        [TestInitialize]
        public void Setup()
        {
            _timeProviderMock.Setup(tp => tp.UtcNow).Returns(DateTime.UtcNow);

            var config = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<AutoMapperProfile>();
            });
            _mapper = config.CreateMapper();

            _configurationMock.Setup(c => c.GetSection("ApplicationSettings:StationaryDirectionNullThresholdHours").Value)
                .Returns("6");

            // Initialize real rule engines with actual rules (same as production)
            var mapPinRules = new List<IMapPinRule>
            {
                new TrainSpeedSanityCheckRule(),
                new TrackageRightsRule(_trackageRightRepositoryMock.Object)
            };
            _mapPinRuleEngine = new MapPinRuleEngine(mapPinRules);

            var telemetryRules = new List<ITelemetryRule>
            {
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
                _loggerMock.Object,
                _configurationMock.Object);
        }

        [TestMethod]
        public async Task GetMapPinByIdAsync_ReturnsMapPin_WhenFound()
        {
            // Arrange
            var mapPin = new MapPin { ID = 1, BeaconID = 1, SubdivisionId = 1 };
            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(1, 2)).ReturnsAsync(mapPin);

            // Act
            var result = await _service.GetMapPinByIdAsync(1, 2);

            // Assert
            Assert.AreEqual(mapPin, result);
        }

        [TestMethod]
        public async Task GetMapPinByIdAsync_ReturnsNull_WhenNotFound()
        {
            // Arrange
            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(1, 2)).ReturnsAsync((MapPin?)null);

            // Act
            var result = await _service.GetMapPinByIdAsync(1, 2);

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

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID, telemetry.TrainID))
                .ReturnsAsync((MapPin?)null);
            _mapPinRepositoryMock.Setup(r => r.GetByTimeThreshold(telemetry.BeaconID, WSORRugbyJunctionBeacon.Subdivision.RailroadID, 5))
                .ReturnsAsync((MapPin?)null);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(expectedMapPinBeforeInsert))
                .ReturnsAsync(expectedMapPinAfterInsert);

            _clientProxyMock.Setup(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate, expectedMapPinObjects, default))
                    .Returns(Task.CompletedTask);
            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(expectedMapPinBeforeInsert), Times.Once);

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

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID, telemetry.TrainID))
                .ReturnsAsync((MapPin?)null);
            _mapPinRepositoryMock.Setup(r => r.GetByTimeThreshold(telemetry.BeaconID, WSORRugbyJunctionBeacon.SubdivisionID, 5))
                .ReturnsAsync((MapPin?)null);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(expectedMapPinBeforeInsert))
                .ReturnsAsync(expectedMapPinAfterInsert);

            _clientProxyMock.Setup(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate, expectedMapPinObjects, default))
                    .Returns(Task.CompletedTask);
            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(expectedMapPinBeforeInsert), Times.Once);

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
                LastUpdate = telemetry.LastUpdate,
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
                        LastUpdate = _timeProviderMock.Object.UtcNow
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

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID, telemetry.TrainID))
                .ReturnsAsync(fromMapPin);
            _beaconRailroadServiceMock.Setup(b => b.GetByIdAsync(telemetry.BeaconID, fromMapPin.SubdivisionId))
                .ReturnsAsync(CNRugbyJunctionBeacon);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(toMapPinBeforeUpdate))
                .ReturnsAsync(toMapPinAfterUpdate);

            _clientProxyMock.Setup(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate, toMapPinObjects, default))
                    .Returns(Task.CompletedTask);
            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(toMapPinBeforeUpdate), Times.Once);

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

            var subdivisionTrackageRights = new List<SubdivisionTrackageRight>
            {
                new SubdivisionTrackageRight
                {
                    ID = 1,
                    FromSubdivisionID = WSORHartfordBeacon.Subdivision.ID,
                    ToSubdivisionID = CNRugbyJunctionBeacon.Subdivision.ID
                }
            };

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID, telemetry.TrainID))
                .ReturnsAsync(previousMapPin);
            _mapPinRepositoryMock.Setup(r => r.GetByTimeThreshold(telemetry.BeaconID, CNRugbyJunctionBeacon.SubdivisionID, 5))
                .ReturnsAsync((MapPin?)null);
            _trackageRightRepositoryMock.Setup(r => r.GetByFromSubdivisionAsync(WSORHartfordBeacon.SubdivisionID))
                .ReturnsAsync(subdivisionTrackageRights);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(expectedMapPinBeforeInsert))
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
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(expectedMapPinBeforeInsert), Times.Once);

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

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID, telemetry.TrainID))
                .ReturnsAsync(fromMapPin); // Simulate previous map pin exists.
            _mapPinRepositoryMock.Setup(r => r.GetByTimeThreshold(telemetry.BeaconID, CNSussexBeacon.SubdivisionID, 5))
                .ReturnsAsync((MapPin?)null);
            _beaconRailroadServiceMock.Setup(s => s.GetByIdAsync(telemetry.BeaconID, fromMapPin.SubdivisionId))
                .ReturnsAsync(toBeaconRailroads[0]);
            _beaconRailroadServiceMock.Setup(s => s.GetByIdAsync(fromMapPin.BeaconID, fromMapPin.SubdivisionId))
                .ReturnsAsync(fromBeaconRailroads[0]);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(It.IsAny<MapPin>()))
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
            )), Times.Once);

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
                Moving = telemetry.Moving,
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

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID, telemetry.TrainID))
                .ReturnsAsync(fromMapPin); // Simulate previous map pin exists.
            _mapPinRepositoryMock.Setup(r => r.GetByTimeThreshold(telemetry.BeaconID, CNSussexBeacon.SubdivisionID, 5))
                .ReturnsAsync((MapPin?)null);
            _beaconRailroadServiceMock.Setup(s => s.GetByIdAsync(telemetry.BeaconID, fromMapPin.SubdivisionId))
                .ReturnsAsync(toBeaconRailroads[0]);
            _beaconRailroadServiceMock.Setup(s => s.GetByIdAsync(fromMapPin.BeaconID, fromMapPin.SubdivisionId))
                .ReturnsAsync(fromBeaconRailroads[0]);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(toMapPinBeforeUpdate))
                .ReturnsAsync(toMapPinAfterUpdate);

            _clientProxyMock.Setup(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate, expectedMapPinObjects, default))
                    .Returns(Task.CompletedTask);
            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(toMapPinBeforeUpdate), Times.Once);

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
                CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(1),
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

            var toMapPinAfterUpdate = toMapPinBeforeUpdate.Clone();

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

            var subdivisionTrackageRights = new List<SubdivisionTrackageRight>
            {
                new SubdivisionTrackageRight
                {
                    ID = 1,
                    FromSubdivisionID = WSORRugbyJunctionBeacon.Subdivision.ID,
                    ToSubdivisionID = CNSussexBeacon.Subdivision.ID
                }
            };

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID, telemetry.TrainID))
                .ReturnsAsync(fromMapPin);
            _beaconRailroadServiceMock.Setup(s => s.GetByIdAsync(fromMapPin.BeaconID, fromMapPin.SubdivisionId))
                .ReturnsAsync(fromBeaconRailroads[1]); // WSOR is second in array.
            _trackageRightRepositoryMock.Setup(r => r.GetByFromSubdivisionAsync(WSORRugbyJunctionBeacon.SubdivisionID))
                .ReturnsAsync(subdivisionTrackageRights);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(toMapPinBeforeUpdate))
                .ReturnsAsync(toMapPinAfterUpdate);

            _clientProxyMock.Setup(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate, expectedMapPinObjects, default))
                    .Returns(Task.CompletedTask);
            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(toMapPinBeforeUpdate), Times.Once);

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
                Moving = telemetry.Moving,
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

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID, telemetry.TrainID))
                .ReturnsAsync(fromMapPin); // Simulate previous map pin exists.
            _mapPinRepositoryMock.Setup(r => r.GetByTimeThreshold(telemetry.BeaconID, CNOshkoshBeaconRailroad.SubdivisionID, 5))
                .ReturnsAsync((MapPin?)null);
            _beaconRailroadServiceMock.Setup(s => s.GetByIdAsync(telemetry.BeaconID, fromMapPin.SubdivisionId))
                .ReturnsAsync(toBeaconRailroads[0]);
            _beaconRailroadServiceMock.Setup(s => s.GetByIdAsync(fromMapPin.BeaconID, fromMapPin.SubdivisionId))
                .ReturnsAsync(fromBeaconRailroads[0]);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(toMapPinBeforeUpdate))
                .ReturnsAsync(toMapPinAfterUpdate);

            _clientProxyMock.Setup(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate, expectedMapPinObjects, default))
                    .Returns(Task.CompletedTask);
            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(toMapPinBeforeUpdate), Times.Once);

            _clientProxyMock?.Verify(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate,
                It.Is<object[]>(args => args[0].Equals(expectedMapPinObjects[0])),
                default), Times.Once);
        }

        /// <summary>
        /// If a train emitting HOT telemetry from a single track and later detected emitting DPU telemetry by the
        /// same single-track beacon, the existing map pin should be updated with the additional source
        /// for the DPU.
        /// </summary>
        [TestMethod]
        public async Task UpsertMapPin_MergeHOTWithDPUViaTimeThreshold()
        {
            // Arrange
            var CNSussexBeacon = TestData.CN_Sussex_WI(_timeProviderMock.Object.UtcNow);

            var fromAddressID = 92342;
            var fromDirection = "N";
            var fromSource = SourceEnum.HOT;

            var toAddressID = 23424;
            var toDirection = "N";
            var toSource = SourceEnum.DPU;

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
                Moving = true,
                CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(1),
                LastUpdate = _timeProviderMock.Object.UtcNow.AddMinutes(2)
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
                ID = 234,
                BeaconID = CNSussexBeacon.BeaconID,
                SubdivisionId = CNSussexBeacon.Subdivision.ID,
                Direction = toDirection,
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
                    },
                    new Address
                    {
                        AddressID = toAddressID,
                        Source = toSource,
                        CreatedAt = _timeProviderMock.Object.UtcNow,
                        LastUpdate = _timeProviderMock.Object.UtcNow,
                    }
                ],
            };

            var toMapPinAfterUpdate = toMapPinBeforeUpdate;

            var toMapPinObjects = new object[]
            {
                new MapPinDTO
                {
                    ID = toMapPinBeforeUpdate.ID,
                    Direction = toDirection,
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

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID, telemetry.TrainID))
                .ReturnsAsync((MapPin?)null);
            _mapPinRepositoryMock.Setup(r => r.GetByTimeThreshold(telemetry.BeaconID, CNSussexBeacon.SubdivisionID, MapPinService.TIME_THRESHOLD_MINUTES))
                .ReturnsAsync(fromMapPin);
            _beaconRailroadServiceMock.Setup(s => s.GetByIdAsync(fromMapPin.BeaconID, fromMapPin.SubdivisionId))
                .ReturnsAsync(beaconRailroads[0]);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(toMapPinBeforeUpdate))
                .ReturnsAsync(toMapPinAfterUpdate);

            _clientProxyMock.Setup(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate, toMapPinObjects, default))
                    .Returns(Task.CompletedTask);
            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(toMapPinBeforeUpdate), Times.Once);

            _clientProxyMock?.Verify(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate,
                It.Is<object[]>(args => args[0].Equals(toMapPinObjects[0])),
                default), Times.Once);
        }

        /// <summary>
        /// Test ensures that two HOT addresses can be combined at a single-track beacon.
        /// </summary>
        [TestMethod]
        public async Task UpsertMapPin_MergeHOTWithHOTViaTimeThreshold()
        {
            // Arrange
            var CNSussexBeacon = TestData.CN_Sussex_WI(_timeProviderMock.Object.UtcNow);

            var fromAddressID = 92342;
            var toAddressID = 23424;

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
                Direction = null,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow,
                BeaconRailroad = CNSussexBeacon,
                Moving = telemetry.Moving,
                Addresses =
                    [
                        new Address
                        {
                            AddressID = fromAddressID,
                            Source = SourceEnum.HOT,
                            CreatedAt = _timeProviderMock.Object.UtcNow,
                            LastUpdate = _timeProviderMock.Object.UtcNow
                        }
                    ],
            };

            var toMapPinBeforeUpdate = new MapPin
            {
                ID = 234,
                BeaconID = CNSussexBeacon.BeaconID,
                SubdivisionId = CNSussexBeacon.Subdivision.ID,
                Direction = null,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow,
                BeaconRailroad = CNSussexBeacon,
                Moving = telemetry.Moving,
                Addresses =
                [
                    new Address
                    {
                        AddressID = fromAddressID,
                        Source = SourceEnum.HOT,
                        CreatedAt = _timeProviderMock.Object.UtcNow,
                        LastUpdate = _timeProviderMock.Object.UtcNow
                    },
                    new Address
                    {
                        AddressID = toAddressID,
                        Source = SourceEnum.HOT,
                        CreatedAt = _timeProviderMock.Object.UtcNow,
                        LastUpdate = _timeProviderMock.Object.UtcNow,
                    }
                ],
            };

            var toMapPinAfterUpdate = toMapPinBeforeUpdate;

            var toMapPinObjects = new object[]
            {
                new MapPinDTO
                {
                    ID = toMapPinBeforeUpdate.ID,
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
                            AddressID = fromAddressID,
                            Source = SourceEnum.HOT
                        },
                        new AddressDTO
                        {
                            AddressID = toAddressID,
                            Source = SourceEnum.HOT
                        }
                    ],
                }
            };

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID, telemetry.TrainID))
                .ReturnsAsync((MapPin?)null);
            _mapPinRepositoryMock.Setup(r => r.GetByTimeThreshold(telemetry.BeaconID, CNSussexBeacon.SubdivisionID, MapPinService.TIME_THRESHOLD_MINUTES))
                .ReturnsAsync(fromMapPin);
            _beaconRailroadServiceMock.Setup(s => s.GetByIdAsync(fromMapPin.BeaconID, fromMapPin.SubdivisionId))
                .ReturnsAsync(beaconRailroads[0]);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(toMapPinBeforeUpdate))
                .ReturnsAsync(toMapPinAfterUpdate);

            _clientProxyMock.Setup(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate, toMapPinObjects, default))
                    .Returns(Task.CompletedTask);
            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(toMapPinBeforeUpdate), Times.Once);

            _clientProxyMock?.Verify(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate,
                It.Is<object[]>(args => args[0].Equals(toMapPinObjects[0])),
                default), Times.Once);
        }

        /// <summary>
        /// Test ensures that a EOT address is added to a HOT address at a single-track beacon.
        /// </summary>
        [TestMethod]
        public async Task UpsertMapPin_MergeEOTWithHOTViaTimeThreshold()
        {
            // Arrange
            var CNSussexBeacon = TestData.CN_Sussex_WI(_timeProviderMock.Object.UtcNow);

            var fromAddressID = 92342;
            var toAddressID = 23424;

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
                Source = SourceEnum.EOT,
                Moving = true,
                CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(1),
                LastUpdate = _timeProviderMock.Object.UtcNow.AddMinutes(2)
            };

            var fromMapPin = new MapPin
            {
                ID = 234,
                BeaconID = CNSussexBeacon.BeaconID,
                SubdivisionId = CNSussexBeacon.Subdivision.ID,
                Direction = null,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow,
                BeaconRailroad = CNSussexBeacon,
                Moving = telemetry.Moving,
                Addresses =
                    [
                        new Address
                        {
                            AddressID = fromAddressID,
                            Source = SourceEnum.HOT,
                            CreatedAt = _timeProviderMock.Object.UtcNow,
                            LastUpdate = _timeProviderMock.Object.UtcNow
                        }
                    ],
            };

            var toMapPinBeforeUpdate = new MapPin
            {
                ID = 234,
                BeaconID = CNSussexBeacon.BeaconID,
                SubdivisionId = CNSussexBeacon.Subdivision.ID,
                Direction = null,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = _timeProviderMock.Object.UtcNow,
                BeaconRailroad = CNSussexBeacon,
                Moving = telemetry.Moving,
                Addresses =
                [
                    new Address
                    {
                        AddressID = fromAddressID,
                        Source = SourceEnum.HOT,
                        CreatedAt = _timeProviderMock.Object.UtcNow,
                        LastUpdate = _timeProviderMock.Object.UtcNow
                    },
                    new Address
                    {
                        AddressID = toAddressID,
                        Source = SourceEnum.EOT,
                        CreatedAt = _timeProviderMock.Object.UtcNow,
                        LastUpdate = _timeProviderMock.Object.UtcNow
                    }
                ],
            };

            var toMapPinAfterUpdate = toMapPinBeforeUpdate;

            var toMapPinObjects = new object[]
            {
                new MapPinDTO
                {
                    ID = toMapPinBeforeUpdate.ID,
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
                            AddressID = fromAddressID,
                            Source = SourceEnum.HOT
                        },
                        new AddressDTO
                        {
                            AddressID = toAddressID,
                            Source = SourceEnum.EOT
                        }
                    ],
                }
            };

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID, telemetry.TrainID))
                .ReturnsAsync((MapPin?)null);
            _mapPinRepositoryMock.Setup(r => r.GetByTimeThreshold(telemetry.BeaconID, CNSussexBeacon.SubdivisionID, MapPinService.TIME_THRESHOLD_MINUTES))
                .ReturnsAsync(fromMapPin);
            _beaconRailroadServiceMock.Setup(s => s.GetByIdAsync(fromMapPin.BeaconID, fromMapPin.SubdivisionId))
                .ReturnsAsync(beaconRailroads[0]);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(toMapPinBeforeUpdate))
                .ReturnsAsync(toMapPinAfterUpdate);

            _clientProxyMock.Setup(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate, toMapPinObjects, default))
                    .Returns(Task.CompletedTask);
            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(toMapPinBeforeUpdate), Times.Once);

            _clientProxyMock?.Verify(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate,
                It.Is<object[]>(args => args[0].Equals(toMapPinObjects[0])),
                default), Times.Once);
        }

        /// <summary>
        /// Test ensures that a EOT address is not added to an existing EOT address at a single-track beacon.
        /// </summary>
        [TestMethod]
        public async Task UpsertMapPin_DoNotMergeEOTWithEOTViaTimeThreshold()
        {
            // Arrange
            var CNSussexBeacon = TestData.CN_Sussex_WI(_timeProviderMock.Object.UtcNow);

            var fromAddressID = 92342;
            var toAddressID = 23424;

            var telemetry = new Telemetry
            {
                BeaconID = CNSussexBeacon.BeaconID,
                Beacon = new Beacon
                {
                    ID = CNSussexBeacon.BeaconID,
                    Name = CNSussexBeacon.Beacon.Name,
                    BeaconRailroads =
                    [
                        CNSussexBeacon
                    ]
                },
                AddressID = toAddressID,
                Source = SourceEnum.EOT,
                Moving = true,
                CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(1),
                LastUpdate = _timeProviderMock.Object.UtcNow.AddMinutes(2)
            };

            var fromMapPin = new MapPin
            {
                ID = 234,
                BeaconID = CNSussexBeacon.BeaconID,
                SubdivisionId = CNSussexBeacon.Subdivision.ID,
                Direction = null,
                CreatedAt = _timeProviderMock.Object.UtcNow,
                LastUpdate = telemetry.LastUpdate,
                BeaconRailroad = CNSussexBeacon,
                Moving = telemetry.Moving,
                Addresses =
                    [
                        new Address
                        {
                            AddressID = fromAddressID,
                            Source = SourceEnum.EOT,
                            CreatedAt = _timeProviderMock.Object.UtcNow,
                            LastUpdate = _timeProviderMock.Object.UtcNow
                        }
                    ],
            };

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID, telemetry.TrainID))
                .ReturnsAsync((MapPin?)null);
            _mapPinRepositoryMock.Setup(r => r.GetByTimeThreshold(telemetry.BeaconID, CNSussexBeacon.SubdivisionID, MapPinService.TIME_THRESHOLD_MINUTES))
                .ReturnsAsync(fromMapPin);

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert
            _telemetryRepositoryMock.Verify(r => r.UpdateAsync(telemetry), Times.Once());
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(It.IsAny<MapPin>()), Times.Never);
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

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID, telemetry.TrainID))
                .ReturnsAsync((MapPin?)null);
            _mapPinRepositoryMock.Setup(r => r.GetByTrainIdAsync(telemetry.TrainID.Value, MapPinService.TIME_THRESHOLD_DPU_MINUTES))
                .ReturnsAsync((MapPin?)null);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(expectedMapPinBeforeUpdate))
                .ReturnsAsync(expectedMapPinAfterUpdate);

            _clientProxyMock.Setup(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate, expectedMapPinObjects, default))
                    .Returns(Task.CompletedTask);
            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(expectedMapPinBeforeUpdate), Times.Once);

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
                CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(2),
                LastUpdate = _timeProviderMock.Object.UtcNow.AddMinutes(2),
                BeaconRailroad = CNRugbyJunctionBeacon,
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
                    CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(2),
                    LastUpdate = _timeProviderMock.Object.UtcNow.AddMinutes(2),
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

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID, telemetry.TrainID))
                .ReturnsAsync((MapPin?)null);
            _mapPinRepositoryMock.Setup(r => r.GetByTimeThreshold(telemetry.BeaconID, CNSussexBeacon.Subdivision.RailroadID, 5))
                .ReturnsAsync(fromMapPin);
            _beaconRailroadServiceMock.Setup(s => s.GetByIdAsync(fromMapPin.BeaconID, fromMapPin.SubdivisionId))
                .ReturnsAsync(beaconRailroads[0]);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(toMapPinBeforeUpdate))
                .ReturnsAsync(toMapPinAfterUpdate);

            _clientProxyMock.Setup(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate, toMapPinObjects, default))
                    .Returns(Task.CompletedTask);
            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(toMapPinBeforeUpdate), Times.Once);

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

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID, telemetry.TrainID))
                .ReturnsAsync((MapPin?)null);
            _mapPinRepositoryMock.Setup(r => r.GetByTimeThreshold(telemetry.BeaconID, CNRugbyJunctionBeacon.SubdivisionID, 5))
                .ReturnsAsync(previousMapPin);
            _beaconRailroadServiceMock.Setup(s => s.GetByIdAsync(previousMapPin.BeaconID, previousMapPin.SubdivisionId))
                .ReturnsAsync(beaconRailroads[0]);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(expectedMapPinBeforeInsert))
                .ReturnsAsync(expectedMapPinAfterInsert);

            _clientProxyMock.Setup(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate, expectedMapPinObjects, default))
                    .Returns(Task.CompletedTask);
            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(expectedMapPinBeforeInsert), Times.Once);

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

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID, telemetry.TrainID))
                .ReturnsAsync((MapPin?)null);
            _mapPinRepositoryMock.Setup(r => r.GetByTimeThreshold(telemetry.BeaconID, CNRugbyJunctionBeacon.SubdivisionID, 5))
                .ReturnsAsync(previousMapPin);
            _beaconRailroadServiceMock.Setup(s => s.GetByIdAsync(previousMapPin.BeaconID, previousMapPin.SubdivisionId))
                .ReturnsAsync(beaconRailroads[0]);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(expectedMapPinBeforeInsert))
                .ReturnsAsync(expectedMapPinAfterInsert);

            _clientProxyMock.Setup(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate, expectedMapPinObjects, default))
                    .Returns(Task.CompletedTask);
            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(expectedMapPinBeforeInsert), Times.Once);

            _clientProxyMock?.Verify(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate,
                It.Is<object[]>(args => args[0].Equals(expectedMapPinObjects[0])),
                default), Times.Once);
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

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID, telemetry.TrainID))
                .ReturnsAsync(previousMapPin);
            _beaconRailroadServiceMock.Setup(b => b.GetByIdAsync(telemetry.BeaconID, beaconRailroad.SubdivisionID))
                .ReturnsAsync(beaconRailroad);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(It.IsAny<MapPin>()))
                .ReturnsAsync((MapPin mp) => mp);
            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);
            _clientProxyMock.Setup(proxy => proxy.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), default))
                .Returns(Task.CompletedTask);

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert - should have 2 addresses: original HOT + new DPU
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(It.Is<MapPin>(mp =>
                mp.Addresses.Count == 2 &&
                mp.Addresses.Any(a => a.Source == SourceEnum.HOT && a.AddressID == 12345) &&
                mp.Addresses.Any(a => a.Source == SourceEnum.DPU && a.AddressID == 54321)
            )), Times.Once);
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

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID, telemetry.TrainID))
                .ReturnsAsync(previousMapPin);
            _beaconRailroadServiceMock.Setup(b => b.GetByIdAsync(telemetry.BeaconID, beaconRailroad.SubdivisionID))
                .ReturnsAsync(beaconRailroad);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(It.IsAny<MapPin>()))
                .ReturnsAsync((MapPin mp) => mp);
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
            )), Times.Once);
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

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID, telemetry.TrainID))
                .ReturnsAsync(previousMapPin);
            _beaconRailroadServiceMock.Setup(b => b.GetByIdAsync(telemetry.BeaconID, beaconRailroad.SubdivisionID))
                .ReturnsAsync(beaconRailroad);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(It.IsAny<MapPin>()))
                .ReturnsAsync((MapPin mp) => mp);
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
            )), Times.Once);
        }

        /// <summary>
        /// Test ensures that when telemetry has no Moving value (null), map pin Moving property is nulled. 
        /// Make no assumptions about whether the train is moving or not.
        /// </summary>
        [TestMethod]
        public async Task UpsertMapPin_MovingNull_NotUpdated()
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

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID, telemetry.TrainID))
                .ReturnsAsync(previousMapPin);
            _beaconRailroadServiceMock.Setup(b => b.GetByIdAsync(telemetry.BeaconID, beaconRailroad.SubdivisionID))
                .ReturnsAsync(beaconRailroad);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(It.IsAny<MapPin>()))
                .ReturnsAsync((MapPin mp) => mp);
            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);
            _clientProxyMock.Setup(proxy => proxy.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), default))
                .Returns(Task.CompletedTask);

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert - Moving should still be true (not updated to null)
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(It.Is<MapPin>(mp =>
                mp.Moving == null
            )), Times.Once);
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
                    },
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

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID, telemetry.TrainID))
                .ReturnsAsync((MapPin?)null);
            _mapPinRepositoryMock.Setup(r => r.GetByTrainIdAsync(telemetry.TrainID.Value, MapPinService.TIME_THRESHOLD_DPU_MINUTES))
                .ReturnsAsync(fromMapPin);
            _beaconRailroadServiceMock.Setup(b => b.GetByIdAsync(telemetry.BeaconID, fromMapPin.SubdivisionId))
                .ReturnsAsync(CNRugbyJunctionBeacon);
            _beaconRailroadServiceMock.Setup(b => b.GetByIdAsync(fromMapPin.BeaconID, fromMapPin.SubdivisionId))
                .ReturnsAsync(CNSussexBeacon);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(toMapPinBeforeUpdate))
                .ReturnsAsync(toMapPinAfterUpdate);

            _clientProxyMock.Setup(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate, toMapPinObjects, default))
                    .Returns(Task.CompletedTask);
            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(toMapPinBeforeUpdate), Times.Once);

            _clientProxyMock?.Verify(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate,
                It.Is<object[]>(args => args[0].Equals(toMapPinObjects[0])),
                default), Times.Once);
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

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID, telemetry.TrainID))
                .ReturnsAsync((MapPin?)null);
            _mapPinRepositoryMock.Setup(r => r.GetByTrainIdAsync(telemetry.TrainID.Value, MapPinService.TIME_THRESHOLD_DPU_MINUTES))
                .ReturnsAsync(wrongRailroadMapPin);

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert
            _telemetryRepositoryMock.Verify(r => r.UpdateAsync(telemetry), Times.Once);

            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(It.IsAny<MapPin>()), Times.Never);

            _clientProxyMock?.Verify(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate,
                It.IsAny<object[]>(),
                default), Times.Never);
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
                CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(-2),
                LastUpdate = _timeProviderMock.Object.UtcNow.AddMinutes(-2),
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
                CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(-2),
                LastUpdate = _timeProviderMock.Object.UtcNow,
                BeaconRailroad = CNRugbyJunctionBeacon,
                Moving = telemetry.Moving,
                CreatedRailroadID = CNSussexBeacon.Subdivision.RailroadID,
                Addresses =
                [
                    new Address
                    {
                        AddressID = fromAddressID,
                        DpuTrainID = fromTrainID,
                        Source = SourceEnum.DPU,
                        CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(-2),
                        LastUpdate = _timeProviderMock.Object.UtcNow,
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
                    CreatedAt = _timeProviderMock.Object.UtcNow.AddMinutes(-2),
                    LastUpdate = _timeProviderMock.Object.UtcNow,
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

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID, telemetry.TrainID))
                .ReturnsAsync((MapPin?)null);
            _mapPinRepositoryMock.Setup(r => r.GetByTrainIdAsync(telemetry.TrainID.Value, MapPinService.TIME_THRESHOLD_DPU_MINUTES))
                .ReturnsAsync(fromMapPin);
            _beaconRailroadServiceMock.Setup(b => b.GetByIdAsync(telemetry.BeaconID, fromMapPin.SubdivisionId))
                .ReturnsAsync(CNRugbyJunctionBeacon);
            _beaconRailroadServiceMock.Setup(b => b.GetByIdAsync(fromMapPin.BeaconID, fromMapPin.SubdivisionId))
                .ReturnsAsync(CNSussexBeacon);
            _mapPinRepositoryMock.Setup(r => r.UpsertAsync(toMapPinBeforeUpdate))
                .ReturnsAsync(toMapPinAfterUpdate);

            _clientProxyMock.Setup(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate, toMapPinObjects, default))
                    .Returns(Task.CompletedTask);
            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);

            // Act
            await _service.UpsertMapPin(telemetry);

            // Assert
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(toMapPinBeforeUpdate), Times.Once);

            _clientProxyMock?.Verify(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate,
                It.Is<object[]>(args => args[0].Equals(toMapPinObjects[0])),
                default), Times.Once);
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

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID, telemetry.TrainID))
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

            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(It.IsAny<MapPin>()), Times.Never);
            _clientProxyMock?.Verify(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate,
                It.IsAny<object[]>(),
                default),
                Times.Never);
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