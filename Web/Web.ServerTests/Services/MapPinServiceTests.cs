using AutoMapper;
using Microsoft.AspNetCore.SignalR;
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

namespace Web.ServerTests.Services
{
    [ExcludeFromCodeCoverage]
    [TestClass]
    public class MapPinServiceTests
    {
        private readonly Mock<IBeaconRailroadService> _beaconRailroadServiceMock = new();
        private readonly Mock<IHubClients> _hubClientsMock = new();
        private readonly Mock<IHubContext<NotificationHub>> _hubContextMock = new();
        private readonly Mock<IMapPinRepository> _mapPinRepositoryMock = new();
        private readonly Mock<ITimeProvider> _timeProviderMock = new();
        private readonly Mock<IClientProxy> _clientProxyMock = new();

        private MapPinService _service;
        private IMapper _mapper;

        private DateTime _currentDateTime;

        [TestInitialize]
        public void Setup()
        {
            _currentDateTime = DateTime.UtcNow;
            _timeProviderMock.Setup(tp => tp.UtcNow).Returns(_currentDateTime);

            var config = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<AutoMapperProfile>();
            });
            _mapper = config.CreateMapper();

            _service = new MapPinService(
                _beaconRailroadServiceMock.Object,
                _hubContextMock.Object,
                _mapper,
                _mapPinRepositoryMock.Object,
                _timeProviderMock.Object
            );
        }

        [TestMethod]
        public async Task GetMapPinsAsync_ReturnsAllMapPins()
        {
            // Arrange
            var mapPins = new List<MapPin> { new MapPin { ID = 1, BeaconID = 1, RailroadID = 1 } };
            _mapPinRepositoryMock.Setup(r => r.GetAllAsync(null)).ReturnsAsync(mapPins);

            // Act
            var result = await _service.GetMapPinsAsync(null);

            // Assert
            Assert.AreEqual(mapPins, result);
        }

        [TestMethod]
        public async Task GetMapPinByIdAsync_ReturnsMapPin_WhenFound()
        {
            // Arrange
            var mapPin = new MapPin { ID = 1, BeaconID = 1, RailroadID = 1 };
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
        public async Task UpsertMapPin_CreateMapPin_MultiRailroad()
        {
            // Arrange
            var CNRugbyJunctionBeacon = TestData.CN_RugbyJunction_WI(_currentDateTime);
            var WSORRugbyJunctionBeacon = TestData.WSOR_RugbyJunction_WI(_currentDateTime);

            var telemetry = new Telemetry
            {
                BeaconID = CNRugbyJunctionBeacon.BeaconID,
                AddressID = 23424,
                Source = "HOT",
                CreatedAt = _currentDateTime,
                LastUpdate = _currentDateTime,
                Moving = true
            };

            // Hack: First railroad listed will be chosen because this is a mutli-railroad beacon
            // and the only other option is to not show the map pin at all.
            var beaconRailroads = new List<BeaconRailroad>
            {
                WSORRugbyJunctionBeacon, // Hack: First railroad will be chosen.
                CNRugbyJunctionBeacon
            };

            var expectedMapPin = new MapPin
            {
                BeaconID = WSORRugbyJunctionBeacon.BeaconID,
                RailroadID = WSORRugbyJunctionBeacon.RailroadID,
                CreatedAt = _currentDateTime,
                LastUpdate = _currentDateTime,
                BeaconRailroad = WSORRugbyJunctionBeacon,
                Moving = telemetry.Moving,
                Addresses =
                [
                    new Address
                    {
                        AddressID = telemetry.AddressID,
                        Source = telemetry.Source,
                        LastUpdate = _currentDateTime
                    }
                ],
            };

            var expectedMapPinObjects = new object[]
            {
                new MapPinDTO
            {
                Direction = null,
                BeaconID = telemetry.BeaconID,
                RailroadID = WSORRugbyJunctionBeacon.RailroadID,
                Railroad = WSORRugbyJunctionBeacon.Railroad?.Name,
                Subdivision = WSORRugbyJunctionBeacon.Railroad?.Subdivision,
                Latitude = WSORRugbyJunctionBeacon.Latitude,
                Longitude = WSORRugbyJunctionBeacon.Longitude,
                Milepost = WSORRugbyJunctionBeacon.Milepost,
                Moving = telemetry.Moving,
                CreatedAt = _currentDateTime,
                LastUpdate = _currentDateTime,
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
            _mapPinRepositoryMock.Setup(r => r.GetByTimeThreshold(telemetry.BeaconID, WSORRugbyJunctionBeacon.RailroadID, 5))
                .ReturnsAsync((MapPin?)null);

            _clientProxyMock.Setup(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate, expectedMapPinObjects, default))
                    .Returns(Task.CompletedTask);
            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);

            // Act
            await _service.UpsertMapPin(telemetry, beaconRailroads);

            // Assert
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(expectedMapPin), Times.Once);

            _clientProxyMock?.Verify(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate,
                It.Is<object[]>(args => args[0].Equals(expectedMapPinObjects[0])),
                default), Times.Once);
        }

        [TestMethod]
        public async Task UpsertMapPin_CreateMapPin_SingleRailroad()
        {
            // Arrange
            var CNRugbyJunctionBeacon = TestData.CN_RugbyJunction_WI(_currentDateTime);
            var WSORRugbyJunctionBeacon = TestData.WSOR_RugbyJunction_WI(_currentDateTime);

            var telemetry = new Telemetry
            {
                BeaconID = CNRugbyJunctionBeacon.BeaconID,
                AddressID = 23424,
                Source = "HOT",
                Moving = true,
                CreatedAt = _currentDateTime,
                LastUpdate = _currentDateTime
            };

            var beaconRailroads = new List<BeaconRailroad>
            {
                WSORRugbyJunctionBeacon
            };

            var expectedMapPin = new MapPin
            {
                BeaconID = WSORRugbyJunctionBeacon.BeaconID,
                RailroadID = WSORRugbyJunctionBeacon.RailroadID,
                CreatedAt = _currentDateTime,
                LastUpdate = _currentDateTime,
                BeaconRailroad = WSORRugbyJunctionBeacon,
                Moving = telemetry.Moving,
                Addresses =
                [
                    new Address
                    {
                        AddressID = telemetry.AddressID,
                        Source = telemetry.Source,
                        LastUpdate = _currentDateTime
                    }
                ],
            };

            var expectedMapPinObjects = new object[]
            {
                new MapPinDTO
                {
                    Direction = null,
                    BeaconID = telemetry.BeaconID,
                    RailroadID = WSORRugbyJunctionBeacon.RailroadID,
                    Railroad = WSORRugbyJunctionBeacon.Railroad?.Name,
                    Subdivision = WSORRugbyJunctionBeacon.Railroad?.Subdivision,
                    Latitude = WSORRugbyJunctionBeacon.Latitude,
                    Longitude = WSORRugbyJunctionBeacon.Longitude,
                    Milepost = WSORRugbyJunctionBeacon.Milepost,
                    Moving = telemetry.Moving,
                    CreatedAt = _currentDateTime,
                    LastUpdate = _currentDateTime,
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
            _mapPinRepositoryMock.Setup(r => r.GetByTimeThreshold(telemetry.BeaconID, WSORRugbyJunctionBeacon.RailroadID, 5))
                .ReturnsAsync((MapPin?)null);

            _clientProxyMock.Setup(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate, expectedMapPinObjects, default))
                    .Returns(Task.CompletedTask);
            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);

            // Act
            await _service.UpsertMapPin(telemetry, beaconRailroads);

            // Assert
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(expectedMapPin), Times.Once);

            _clientProxyMock?.Verify(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate,
                It.Is<object[]>(args => args[0].Equals(expectedMapPinObjects[0])),
                default), Times.Once);
        }

        [TestMethod]
        public async Task UpsertMapPin_UpdateMapPin_SingleRailroad_SameAddress_DifferentSource()
        {
            // Arrange
            var CNRugbyJunctionBeacon = TestData.CN_RugbyJunction_WI(_currentDateTime);
            var WSORRugbyJunctionBeacon = TestData.WSOR_RugbyJunction_WI(_currentDateTime);
            var CNSussexBeacon = TestData.CN_Sussex_WI(_currentDateTime);

            var calculatedDirection = "N";

            var newSource = "EOT";
            var previousSource = "HOT";

            var telemetry = new Telemetry
            {
                BeaconID = CNRugbyJunctionBeacon.BeaconID,
                AddressID = 23424,
                Source = newSource,
                Moving = true,
                CreatedAt = _currentDateTime,
                LastUpdate = _currentDateTime
            };

            var toBeaconRailroads = new List<BeaconRailroad>
            {
                CNRugbyJunctionBeacon,
                WSORRugbyJunctionBeacon
            };

            var fromBeaconRailroads = new List<BeaconRailroad>
            {
                CNSussexBeacon
            };

            var previousMapPin = new MapPin
            {
                BeaconID = CNSussexBeacon.BeaconID,
                RailroadID = CNSussexBeacon.RailroadID,
                CreatedAt = _currentDateTime,
                LastUpdate = _currentDateTime,
                BeaconRailroad = CNSussexBeacon,
                Moving = telemetry.Moving,
                Addresses =
                    [
                        new Address
                        {
                            AddressID = telemetry.AddressID,
                            Source = previousSource, // Different source.
                            LastUpdate = _currentDateTime
                        }
                    ],
            };

            var expectedMapPin = new MapPin
            {
                BeaconID = CNRugbyJunctionBeacon.BeaconID,
                RailroadID = CNRugbyJunctionBeacon.RailroadID,
                Direction = calculatedDirection,
                CreatedAt = _currentDateTime,
                LastUpdate = _currentDateTime,
                BeaconRailroad = CNRugbyJunctionBeacon,
                Moving = telemetry.Moving,
                Addresses =
                [
                    new Address
                    {
                        AddressID = telemetry.AddressID,
                        Source = previousSource,
                        LastUpdate = _currentDateTime
                    },
                    new Address
                    {
                        AddressID = telemetry.AddressID,
                        Source = newSource,
                        LastUpdate = _currentDateTime
                    }
                ],
            };

            var expectedMapPinObjects = new object[]
            {
                new MapPinDTO
                {
                    Direction = calculatedDirection,
                    BeaconID = telemetry.BeaconID,
                    RailroadID = CNRugbyJunctionBeacon.RailroadID,
                    Railroad = CNRugbyJunctionBeacon.Railroad?.Name,
                    Subdivision = CNRugbyJunctionBeacon.Railroad?.Subdivision,
                    Latitude = CNRugbyJunctionBeacon.Latitude,
                    Longitude = CNRugbyJunctionBeacon.Longitude,
                    Milepost = CNRugbyJunctionBeacon.Milepost,
                    Moving = telemetry.Moving,
                    CreatedAt = _currentDateTime,
                    LastUpdate = _currentDateTime,
                    Addresses =
                    [
                        new AddressDTO
                        {
                            AddressID = telemetry.AddressID,
                            Source = previousSource
                        },
                        new AddressDTO
                        {
                            AddressID = telemetry.AddressID,
                            Source = newSource
                        }
                    ],
                }
            };

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID))
                .ReturnsAsync(previousMapPin); // Simulate previous map pin exists.
            _mapPinRepositoryMock.Setup(r => r.GetByTimeThreshold(telemetry.BeaconID, CNSussexBeacon.RailroadID, 5))
                .ReturnsAsync((MapPin?)null);
            _beaconRailroadServiceMock.Setup(s => s.GetByIdAsync(previousMapPin.BeaconID, previousMapPin.RailroadID))
                .ReturnsAsync(fromBeaconRailroads[0]); // Not the same beacon as new telemetry.

            _clientProxyMock.Setup(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate, expectedMapPinObjects, default))
                    .Returns(Task.CompletedTask);
            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);

            // Act
            await _service.UpsertMapPin(telemetry, toBeaconRailroads);

            // Assert
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(expectedMapPin), Times.Once);

            _clientProxyMock?.Verify(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate,
                It.Is<object[]>(args => args[0].Equals(expectedMapPinObjects[0])),
                default), Times.Once);
        }

        [TestMethod]
        public async Task UpsertMapPin_UpdateMapPin_SingleRailroad_SameAddress_SameSource()
        {
            // Arrange
            var CNRugbyJunctionBeacon = TestData.CN_RugbyJunction_WI(_currentDateTime);
            var WSORRugbyJunctionBeacon = TestData.WSOR_RugbyJunction_WI(_currentDateTime);
            var CNSussexBeacon = TestData.CN_Sussex_WI(_currentDateTime);

            var calculatedDirection = "N";

            var telemetry = new Telemetry
            {
                BeaconID = CNRugbyJunctionBeacon.BeaconID,
                AddressID = 23424,
                Source = "HOT",
                Moving = true,
                CreatedAt = _currentDateTime,
                LastUpdate = _currentDateTime
            };

            var toBeaconRailroads = new List<BeaconRailroad>
            {
                CNRugbyJunctionBeacon,
                WSORRugbyJunctionBeacon
            };

            var fromBeaconRailroads = new List<BeaconRailroad>
            {
                CNSussexBeacon
            };

            var previousMapPin = new MapPin
            {
                BeaconID = CNSussexBeacon.BeaconID,
                RailroadID = CNSussexBeacon.RailroadID,
                CreatedAt = _currentDateTime,
                LastUpdate = _currentDateTime,
                BeaconRailroad = CNSussexBeacon,
                Moving = telemetry.Moving,
                Addresses =
                [
                    new Address
                    {
                        AddressID = telemetry.AddressID,
                        Source = telemetry.Source,
                        LastUpdate = _currentDateTime
                    }
                ],
            };

            var expectedMapPin = new MapPin
            {
                BeaconID = CNRugbyJunctionBeacon.BeaconID,
                RailroadID = CNRugbyJunctionBeacon.RailroadID,
                Direction = calculatedDirection,
                CreatedAt = _currentDateTime,
                LastUpdate = _currentDateTime,
                BeaconRailroad = CNRugbyJunctionBeacon,
                Moving = telemetry.Moving,
                Addresses =
                [
                    new Address
                    {
                        AddressID = telemetry.AddressID,
                        Source = telemetry.Source,
                        LastUpdate = _currentDateTime
                    }
                ],
            };

            var expectedMapPinObjects = new object[]
            {
                new MapPinDTO
                {
                    Direction = calculatedDirection,
                    BeaconID = telemetry.BeaconID,
                    RailroadID = CNRugbyJunctionBeacon.RailroadID,
                    Railroad = CNRugbyJunctionBeacon.Railroad?.Name,
                    Subdivision = CNRugbyJunctionBeacon.Railroad?.Subdivision,
                    Latitude = CNRugbyJunctionBeacon.Latitude,
                    Longitude = CNRugbyJunctionBeacon.Longitude,
                    Milepost = CNRugbyJunctionBeacon.Milepost,
                    Moving = telemetry.Moving,
                    CreatedAt = _currentDateTime,
                    LastUpdate = _currentDateTime,
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
                .ReturnsAsync(previousMapPin); // Simulate previous map pin exists.
            _mapPinRepositoryMock.Setup(r => r.GetByTimeThreshold(telemetry.BeaconID, CNSussexBeacon.RailroadID, 5))
                .ReturnsAsync((MapPin?)null);
            _beaconRailroadServiceMock.Setup(s => s.GetByIdAsync(previousMapPin.BeaconID, previousMapPin.RailroadID))
                .ReturnsAsync(fromBeaconRailroads[0]); // Not the same beacon as new telemetry.

            _clientProxyMock.Setup(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate, expectedMapPinObjects, default))
                    .Returns(Task.CompletedTask);
            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);

            // Act
            await _service.UpsertMapPin(telemetry, toBeaconRailroads);

            // Assert
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(expectedMapPin), Times.Once);

            _clientProxyMock?.Verify(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate,
                It.Is<object[]>(args => args[0].Equals(expectedMapPinObjects[0])),
                default), Times.Once);
        }

        [TestMethod]
        public async Task UpsertMapPin_UpdateMapPin_SingleRailroad_TimeThreshold_PreviousDirection()
        {
            // Arrange
            var CNSussexBeacon = TestData.CN_Sussex_WI(_currentDateTime);

            var calculatedDirection = "N";

            var newSource = "DPU";
            var previousSource = "HOT";

            var newAddressID = 23424;
            var previousAddressID = 92342;

            var telemetry = new Telemetry
            {
                BeaconID = CNSussexBeacon.BeaconID,
                AddressID = newAddressID,
                Source = newSource,
                Moving = true,
                CreatedAt = _currentDateTime,
                LastUpdate = _currentDateTime
            };

            var beaconRailroads = new List<BeaconRailroad>
            {
                CNSussexBeacon
            };

            var previousMapPin = new MapPin
            {
                BeaconID = CNSussexBeacon.BeaconID,
                RailroadID = CNSussexBeacon.RailroadID,
                Direction = calculatedDirection,
                CreatedAt = _currentDateTime,
                LastUpdate = _currentDateTime,
                BeaconRailroad = CNSussexBeacon,
                Moving = telemetry.Moving,
                Addresses =
                    [
                        new Address
                        {
                            AddressID = previousAddressID,
                            Source = previousSource,
                            LastUpdate = _currentDateTime
                        }
                    ],
            };

            var expectedMapPin = new MapPin
            {
                BeaconID = CNSussexBeacon.BeaconID,
                RailroadID = CNSussexBeacon.RailroadID,
                Direction = calculatedDirection,
                CreatedAt = _currentDateTime,
                LastUpdate = _currentDateTime,
                BeaconRailroad = CNSussexBeacon,
                Moving = telemetry.Moving,
                Addresses =
                [
                    new Address
                    {
                        AddressID = previousAddressID,
                        Source = previousSource,
                        LastUpdate = _currentDateTime
                    },
                    new Address
                    {
                        AddressID = newAddressID,
                        Source = newSource,
                        LastUpdate = _currentDateTime
                    }
                ],
            };

            var expectedMapPinObjects = new object[]
            {
                new MapPinDTO
                {
                    Direction = calculatedDirection,
                    BeaconID = telemetry.BeaconID,
                    RailroadID = CNSussexBeacon.RailroadID,
                    Railroad = CNSussexBeacon.Railroad?.Name,
                    Subdivision = CNSussexBeacon.Railroad?.Subdivision,
                    Latitude = CNSussexBeacon.Latitude,
                    Longitude = CNSussexBeacon.Longitude,
                    Milepost = CNSussexBeacon.Milepost,
                    Moving = telemetry.Moving,
                    CreatedAt = _currentDateTime,
                    LastUpdate = _currentDateTime,
                    Addresses =
                    [
                        new AddressDTO
                        {
                            AddressID = previousAddressID,
                            Source = previousSource
                        },
                        new AddressDTO
                        {
                            AddressID = newAddressID,
                            Source = newSource
                        }
                    ],
                }
            };

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID))
                .ReturnsAsync((MapPin?)null);
            _mapPinRepositoryMock.Setup(r => r.GetByTimeThreshold(telemetry.BeaconID, CNSussexBeacon.RailroadID, 5))
                .ReturnsAsync(previousMapPin);
            _beaconRailroadServiceMock.Setup(s => s.GetByIdAsync(previousMapPin.BeaconID, previousMapPin.RailroadID))
                .ReturnsAsync(beaconRailroads[0]);

            _clientProxyMock.Setup(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate, expectedMapPinObjects, default))
                    .Returns(Task.CompletedTask);
            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);

            // Act
            await _service.UpsertMapPin(telemetry, beaconRailroads);

            // Assert
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(expectedMapPin), Times.Once);

            _clientProxyMock?.Verify(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate,
                It.Is<object[]>(args => args[0].Equals(expectedMapPinObjects[0])),
                default), Times.Once);
        }

        [TestMethod]
        public async Task UpsertMapPin_UpdateMapPin_SingleDpuCapableRailroad_TimeThreshold_PreviousDirection()
        {
            // Arrange
            var CNRugbyJunctionBeacon = TestData.CN_RugbyJunction_WI(_currentDateTime);
            var WSORRugbyJunctionBeacon = TestData.WSOR_RugbyJunction_WI(_currentDateTime);

            var calculatedDirection = "N";

            var newDpuSource = "DPU";
            var previousHotSource = "HOT";

            var newAddressID = 23424;
            var previousAddressID = 92342;

            var telemetry = new Telemetry
            {
                BeaconID = CNRugbyJunctionBeacon.BeaconID,
                AddressID = newAddressID,
                Source = newDpuSource,
                Moving = true,
                CreatedAt = _currentDateTime,
                LastUpdate = _currentDateTime
            };

            var beaconRailroads = new List<BeaconRailroad>
            {
                CNRugbyJunctionBeacon,
                WSORRugbyJunctionBeacon
            };

            var previousMapPin = new MapPin
            {
                BeaconID = CNRugbyJunctionBeacon.BeaconID,
                RailroadID = CNRugbyJunctionBeacon.RailroadID,
                Direction = calculatedDirection,
                CreatedAt = _currentDateTime,
                LastUpdate = _currentDateTime,
                BeaconRailroad = CNRugbyJunctionBeacon,
                Moving = telemetry.Moving,
                Addresses =
                    [
                        new Address
                        {
                            AddressID = previousAddressID,
                            Source = previousHotSource,
                            LastUpdate = _currentDateTime
                        }
                    ],
            };

            var expectedMapPin = new MapPin
            {
                BeaconID = CNRugbyJunctionBeacon.BeaconID,
                RailroadID = CNRugbyJunctionBeacon.RailroadID,
                Direction = calculatedDirection,
                CreatedAt = _currentDateTime,
                LastUpdate = _currentDateTime,
                BeaconRailroad = CNRugbyJunctionBeacon,
                Moving = telemetry.Moving,
                Addresses =
                [
                    new Address
                    {
                        AddressID = previousAddressID,
                        Source = previousHotSource,
                        LastUpdate = _currentDateTime
                    },
                    new Address
                    {
                        AddressID = newAddressID,
                        Source = newDpuSource,
                        LastUpdate = _currentDateTime
                    }
                ],
            };

            var expectedMapPinObjects = new object[]
            {
                new MapPinDTO
                {
                    Direction = calculatedDirection,
                    BeaconID = telemetry.BeaconID,
                    RailroadID = CNRugbyJunctionBeacon.RailroadID,
                    Railroad = CNRugbyJunctionBeacon.Railroad?.Name,
                    Subdivision = CNRugbyJunctionBeacon.Railroad?.Subdivision,
                    Latitude = CNRugbyJunctionBeacon.Latitude,
                    Longitude = CNRugbyJunctionBeacon.Longitude,
                    Milepost = CNRugbyJunctionBeacon.Milepost,
                    Moving = telemetry.Moving,
                    CreatedAt = _currentDateTime,
                    LastUpdate = _currentDateTime,
                    Addresses =
                    [
                        new AddressDTO
                        {
                            AddressID = previousAddressID,
                            Source = previousHotSource
                        },
                        new AddressDTO
                        {
                            AddressID = newAddressID,
                            Source = newDpuSource
                        }
                    ],
                }
            };

            _mapPinRepositoryMock.Setup(r => r.GetByAddressIdAsync(telemetry.AddressID))
                .ReturnsAsync((MapPin?)null);
            _mapPinRepositoryMock.Setup(r => r.GetByTimeThreshold(telemetry.BeaconID, CNRugbyJunctionBeacon.RailroadID, 5))
                .ReturnsAsync(previousMapPin);
            _beaconRailroadServiceMock.Setup(s => s.GetByIdAsync(previousMapPin.BeaconID, previousMapPin.RailroadID))
                .ReturnsAsync(beaconRailroads[0]);

            _clientProxyMock.Setup(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate, expectedMapPinObjects, default))
                    .Returns(Task.CompletedTask);
            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
            _hubClientsMock.Setup(h => h.All).Returns(_clientProxyMock.Object);

            // Act
            await _service.UpsertMapPin(telemetry, beaconRailroads);

            // Assert
            _mapPinRepositoryMock.Verify(r => r.UpsertAsync(expectedMapPin), Times.Once);

            _clientProxyMock?.Verify(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapPinUpdate,
                It.Is<object[]>(args => args[0].Equals(expectedMapPinObjects[0])),
                default), Times.Once);
        }

        private static class TestData
        {
            public static BeaconRailroad CN_RugbyJunction_WI(DateTime currentDateTime)
            {
                return new BeaconRailroad
                {
                    BeaconID = 1,
                    RailroadID = 1,
                    Railroad = TestData.CN_Waukesha(),
                    Direction = Direction.NorthSouth,
                    Latitude = 43.280958,
                    Longitude = -88.214682,
                    Milepost = 117.2,
                    LastUpdate = currentDateTime
                };
            }

            public static BeaconRailroad CN_Sussex_WI(DateTime currentDateTime)
            {
                return new BeaconRailroad
                {
                    BeaconID = 2,
                    RailroadID = 1,
                    Railroad = TestData.CN_Waukesha(),
                    Direction = Direction.NorthSouth,
                    Latitude = 43.159517,
                    Longitude = -88.200492,
                    Milepost = 108.6,
                    LastUpdate = currentDateTime
                };
            }

            public static BeaconRailroad WSOR_RugbyJunction_WI(DateTime currentDateTime)
            {
                return new BeaconRailroad
                {
                    BeaconID = 1,
                    RailroadID = 2,
                    Railroad = TestData.WSOR_Milwaukee(),
                    Direction = Direction.NorthwestSoutheast,
                    Latitude = 43.280958,
                    Longitude = -88.213966,
                    Milepost = 112.16,
                    LastUpdate = currentDateTime
                };
            }

            public static Railroad CN_Waukesha()
            {
                return new Railroad
                {
                    ID = 1,
                    Name = "CN",
                    Subdivision = "Waukesha",
                    DpuCapable = true
                };
            }

            public static Railroad WSOR_Milwaukee()
            {
                return new Railroad
                {
                    ID = 2,
                    Name = "WSOR",
                    Subdivision = "Milwaukee",
                    DpuCapable = false
                };
            }
        }
    }
}