using AutoMapper;
using CloneExtensions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Web.Server.Entities;
using Web.Server.Hubs;
using Web.Server.Mappers;
using Web.Server.Providers;
using Web.Server.Repositories;

namespace Web.Server.Services.Tests
{
    [TestClass]
    public class TelemetryServiceTests
    {
        private TelemetryService? _telemetryService;

        private readonly Mock<IBeaconRepository> _mockBeaconRepository = new();
        private readonly Mock<ITelemetryRepository> _mockTelemetryRepository = new();
        private readonly Mock<IHubContext<NotificationHub>> _mockHubContext = new();
        private readonly Mock<IHubClients> _mockHubClients = new();
        private readonly Mock<IClientProxy>? _mockClientProxy = new();
        private readonly Mock<IMapPinsService> _mockMapPinService = new();
        private readonly Mock<ITimeProvider> _mockTimeProvider = new();
        private IMapper? _mapper;
        private DateTime _currentDateTime;

        [TestInitialize]
        public void Initialize()
        {
            _currentDateTime = DateTime.UtcNow;
            _mockTimeProvider.Setup(tp => tp.UtcNow).Returns(_currentDateTime);

            var config = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<AutoMapperProfile>();
            });
            _mapper = config.CreateMapper();

            _telemetryService = new TelemetryService(
                _mockHubContext.Object,
                _mockTelemetryRepository.Object,
                _mockBeaconRepository.Object,
                _mapper,
                _mockMapPinService.Object,
                _mockTimeProvider.Object);
        }

        [TestMethod]
        public async Task GetTelemetries_ReturnsAllTelemetries()
        {
            // Arrange
            var telemetries = new List<Telemetry>
            {
                new Telemetry { ID = 1, AddressID = 100, Source = "HOT", CreatedAt = DateTime.UtcNow },
                new Telemetry { ID = 2, AddressID = 200, Source = "EOT", CreatedAt = DateTime.UtcNow }
            };

            _mockTelemetryRepository.Setup(repo => repo.GetAllAsync())
                .ReturnsAsync(telemetries);

            // Act
            var result = await _telemetryService.GetTelemetriesAsync();

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
                CreatedAt = DateTime.UtcNow,
                Beacon = new Beacon { ID = 1, CreatedAt = DateTime.UtcNow }
            };

            _mockBeaconRepository.Setup(repo => repo.GetByIdAsync(telemetry.Beacon.ID))
                .ReturnsAsync((Beacon?)null);

            // Act
            var ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
            {
                await _telemetryService.CreateTelemetryAsync(telemetry);
            });

            // Assert
            Assert.AreEqual(ex.Message, "Beacon not found.");
            _mockBeaconRepository.Verify(repo => repo.GetByIdAsync(telemetry.Beacon.ID), Times.Once);
            _mockBeaconRepository.Verify(repo => repo.UpdateAsync(It.IsAny<Beacon>()), Times.Never);
        }

        /// <summary>
        /// Test to ensure that a map alert is created if the telemetry is the first telemetry
        /// for a train and the beacon is single-railroad.
        /// </summary>
        [TestMethod]
        public void CreateTelemetry_FirstTelemetry_OneRailroadBeacon()
        {
            var trainAddressId = 90234;
            var trainSource = "HOT";
            var telemetryId = 432;
            var ownerId = 19;
            var beaconId = 534;
            var beaconLatitude = 10.0;
            var beaconLongitude = 20.0;
            var milepost = 123.45;
            var railroadId = 12;

            // Arrange
            var telemetry = new Telemetry
            {
                ID = telemetryId,
                AddressID = trainAddressId,
                Source = trainSource,
                CreatedAt = _currentDateTime.AddMicroseconds(-234), // Simulate beacon's timestamp that will be ignored
                Beacon = new Beacon
                {
                    ID = beaconId,
                    CreatedAt = _currentDateTime.AddMilliseconds(-300), // Simulate beacon's timestamp
                }
            };

            var telemetryBeforeAdd = telemetry.GetClone();
            telemetryBeforeAdd.CreatedAt = _currentDateTime; // Timestamp set by APi

            var telemetryAfterAdd = new Telemetry
            {
                ID = telemetryId,
                AddressID = trainAddressId,
                Source = trainSource,
                CreatedAt = _currentDateTime,
                Beacon = new Beacon
                {
                    ID = beaconId,
                    CreatedAt = _currentDateTime.AddMilliseconds(-300), // Simulate last beacon check-in timestamp
                    BeaconRailroads = new List<BeaconRailroad> {
                        new BeaconRailroad {
                            RailroadID = railroadId,
                            BeaconID = beaconId,
                            Latitude = beaconLatitude,
                            Longitude = beaconLongitude,
                            Milepost = milepost,
                            Direction = Enums.Direction.NorthSouth
                        }
                    }
                }
            };

            var existingTelemetry = new List<Telemetry>(); // No previous telemetry

            var beaconBeforeUpdate = new Beacon
            {
                ID = beaconId,
                OwnerID = ownerId,
                CreatedAt = _currentDateTime // Should get new timestamp from API
            };

            var beaconAfterUpdate = beaconBeforeUpdate.GetClone();

            var expectedMapAlert = new MapPin
            {
                AddressID = trainAddressId,
                Latitude = beaconLatitude,
                Longitude = beaconLongitude,
                Milepost = milepost,
                Source = trainSource,
                CreatedAt = _currentDateTime,
                Direction = "" // Direction should not / cannot be calculated
            };

            var expectedMapAlertObjects = new object[]
            {
                expectedMapAlert
            };

            _mockBeaconRepository.Setup(repo => repo.GetByIdAsync(telemetry.Beacon.ID))
                    .ReturnsAsync(beaconBeforeUpdate);
            _mockBeaconRepository.Setup(repo => repo.UpdateAsync(beaconBeforeUpdate))
                    .ReturnsAsync(beaconAfterUpdate);
            _mockTelemetryRepository.Setup(repo => repo.GetAllAsync())
                    .ReturnsAsync(existingTelemetry);
            _mockTelemetryRepository.Setup(repo => repo.AddAsync(telemetryBeforeAdd))
                    .ReturnsAsync(telemetryAfterAdd);
            _mockClientProxy?.Setup(proxy => proxy.SendCoreAsync(NotificationMethods.MapAlert, expectedMapAlertObjects, default))
                    .Returns(Task.CompletedTask);
            _mockHubContext.Setup(h => h.Clients).Returns(_mockHubClients.Object);
            _mockHubClients.Setup(h => h.All).Returns(_mockClientProxy?.Object);

            // Act
            _telemetryService?.CreateTelemetryAsync(telemetry);

            // Assert
            _mockTelemetryRepository.Verify(repo => repo.AddAsync(It.Is<Telemetry>(t => t == telemetry)), Times.Once);
            _mockBeaconRepository.Verify(repo => repo.GetByIdAsync(telemetry.Beacon.ID), Times.Once);
            _mockBeaconRepository.Verify(repo => repo.UpdateAsync(It.Is<Beacon>(b => b.ID == telemetry.Beacon.ID)), Times.Once);

            _mockClientProxy.Verify(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapAlert,
                It.Is<object[]>(args => args[0].Equals(expectedMapAlert)),
                default), Times.Once);
        }

        /// <summary>
        /// Test to ensure that no map alert is created if the telemetry is the first telemetry
        /// for a train and the beacon is multi-railroad.
        /// </summary>
        [TestMethod]
        public void CreateTelemetry_FirstTelemetry_MultipleRailroadBeacon()
        {
            var trainAddressId = 90234;
            var trainSource = "HOT";
            var telemetryId = 432;
            var ownerId = 19;
            var beaconId = 534;
            var beacon1Latitude = 10.0;
            var beacon1Longitude = 11.0;
            var beacon2Latitude = 20.0;
            var beacon2Longitude = 21.0;
            var milepost1 = 123.45;
            var milepost2 = 234.56;
            var railroadId1 = 12;
            var railroadId2 = 41;

            // Arrange
            var telemetry = new Telemetry
            {
                ID = telemetryId,
                AddressID = trainAddressId,
                Source = trainSource,
                CreatedAt = _currentDateTime.AddMicroseconds(-234), // Simulate beacon's timestamp that will be ignored
                Beacon = new Beacon
                {
                    ID = beaconId,
                    CreatedAt = _currentDateTime.AddMilliseconds(-300), // Simulate beacon's timestamp
                }
            };

            var telemetryBeforeAdd = telemetry.GetClone();
            telemetryBeforeAdd.CreatedAt = _currentDateTime; // Timestamp set by APi

            var telemetryAfterAdd = new Telemetry
            {
                ID = telemetryId,
                AddressID = trainAddressId,
                Source = trainSource,
                CreatedAt = _currentDateTime,
                Beacon = new Beacon
                {
                    ID = beaconId,
                    OwnerID = ownerId,
                    CreatedAt = _currentDateTime.AddMilliseconds(-300), // Simulate last beacon check-in timestamp
                    BeaconRailroads = new List<BeaconRailroad> {
                        new BeaconRailroad {
                            RailroadID = railroadId1,
                            BeaconID = beaconId,
                            Latitude = beacon1Latitude,
                            Longitude = beacon1Longitude,
                            Milepost = milepost1,
                            Direction = Enums.Direction.NorthSouth
                        },
                        new BeaconRailroad {
                            RailroadID = railroadId2,
                            BeaconID = beaconId,
                            Latitude = beacon2Latitude,
                            Longitude = beacon2Longitude,
                            Milepost = milepost2,
                            Direction = Enums.Direction.NorthwestSoutheast
                        }
                    }
                }
            };

            var existingTelemetry = new List<Telemetry>(); // No previous telemetry

            var beaconBeforeUpdate = new Beacon
            {
                ID = beaconId,
                OwnerID = ownerId,
                CreatedAt = _currentDateTime // Should get new timestamp from API
            };

            var beaconAfterUpdate = beaconBeforeUpdate.GetClone();

            _mockBeaconRepository.Setup(repo => repo.GetByIdAsync(telemetry.Beacon.ID))
                .ReturnsAsync(beaconBeforeUpdate);
            _mockBeaconRepository.Setup(repo => repo.UpdateAsync(beaconBeforeUpdate))
                .ReturnsAsync(beaconAfterUpdate);
            _mockTelemetryRepository.Setup(repo => repo.GetAllAsync())
                .ReturnsAsync(existingTelemetry);
            _mockTelemetryRepository.Setup(repo => repo.AddAsync(telemetryBeforeAdd))
                .ReturnsAsync(telemetryAfterAdd);

            // Act
            _telemetryService?.CreateTelemetryAsync(telemetry);

            // Assert
            _mockTelemetryRepository.Verify(repo => repo.AddAsync(It.Is<Telemetry>(t => t == telemetry)), Times.Once);
            _mockBeaconRepository.Verify(repo => repo.GetByIdAsync(telemetry.Beacon.ID), Times.Once);
            _mockBeaconRepository.Verify(repo => repo.UpdateAsync(It.Is<Beacon>(b => b.ID == telemetry.Beacon.ID)), Times.Once);

            _mockClientProxy.Verify(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapAlert,
                It.IsAny<object[]>(),
                default), Times.Never);
        }

        /// <summary>
        /// Test to ensure that a map alert is created if the telemetry is the second telemetry
        /// for a train and both the new telemetry and existing telemetry beacons are single-railroad.
        /// </summary>
        [TestMethod]
        public void CreateTelemetry_SecondTelemetry_OneRailroadBeacon_OneRailroadBeacon()
        {
            var trainAddressId = 90234;
            var train1Source = "HOT";
            var train2Source = "EOT";
            var telemetry1Id = 432;
            var telemetry2Id = 433;
            var owner1Id = 19;
            var owner2Id = 29;
            var beacon1Id = 134;
            var beacon1Latitude = 10.0;
            var beacon1Longitude = 11.0;
            var beacon2Id = 234;
            var beacon2Latitude = 20.0;
            var beacon2Longitude = 21.0;
            var milepost1 = 123.45;
            var milepost2 = 234.56;
            var railroad1Id = 12;

            // Arrange
            var telemetry = new Telemetry
            {
                ID = telemetry1Id,
                AddressID = trainAddressId,
                Source = train1Source,
                CreatedAt = _currentDateTime.AddMicroseconds(-234), // Simulate beacon's timestamp that will be ignored
                Beacon = new Beacon
                {
                    ID = beacon1Id,
                    OwnerID = owner1Id,
                    CreatedAt = _currentDateTime.AddMilliseconds(-300), // Simulate beacon's timestamp
                }
            };

            var telemetryBeforeAdd = telemetry.GetClone();
            telemetryBeforeAdd.CreatedAt = _currentDateTime; // Timestamp set by APi

            var telemetryAfterAdd = new Telemetry
            {
                ID = telemetry1Id,
                AddressID = trainAddressId,
                Source = train1Source,
                CreatedAt = _currentDateTime,
                Beacon = new Beacon
                {
                    ID = beacon1Id,
                    OwnerID = owner1Id,
                    CreatedAt = _currentDateTime.AddMilliseconds(-300), // Simulate last beacon check-in timestamp
                    BeaconRailroads = [
                        new BeaconRailroad {
                            RailroadID = railroad1Id,
                            BeaconID = beacon1Id,
                            Latitude = beacon1Latitude,
                            Longitude = beacon1Longitude,
                            Milepost = milepost1,
                            Direction = Enums.Direction.NorthSouth
                        }
                    ]
                }
            };

            var singleRailroadBeacon = new Beacon
            {
                ID = beacon2Id,
                OwnerID = owner2Id,
                CreatedAt = _currentDateTime.AddMilliseconds(-500),
                BeaconRailroads = [
                            new BeaconRailroad {
                                RailroadID = railroad1Id,
                                BeaconID = beacon2Id,
                                Latitude = beacon2Latitude,
                                Longitude = beacon2Longitude,
                                Milepost = milepost2,
                                Direction = Enums.Direction.NorthSouth
                            }
                        ]
            };

            var existingTelemetry = new List<Telemetry>
            {
                new() {
                    ID = telemetry2Id,
                    AddressID = trainAddressId,
                    Source = train2Source,
                    CreatedAt = _currentDateTime.AddMilliseconds(-500),
                    Beacon = singleRailroadBeacon
                }
            };

            var beaconBeforeUpdate = new Beacon
            {
                ID = beacon1Id,
                OwnerID = owner1Id,
                CreatedAt = _currentDateTime // Should get new timestamp from API
            };

            var beaconAfterUpdate = beaconBeforeUpdate.GetClone();

            var expectedMapAlert = new MapPin
            {
                AddressID = 90234,
                Latitude = beacon1Latitude,
                Longitude = beacon1Longitude,
                Milepost = milepost1,
                Source = train1Source,
                CreatedAt = _currentDateTime,
                Direction = "S"
            };

            var expectedMapAlertObjects = new object[]
            {
                expectedMapAlert
            };

            _mockBeaconRepository.Setup(repo => repo.GetByIdAsync(telemetry.Beacon.ID))
                    .ReturnsAsync(beaconBeforeUpdate);
            _mockBeaconRepository.Setup(repo => repo.UpdateAsync(beaconBeforeUpdate))
                    .ReturnsAsync(beaconAfterUpdate);
            _mockTelemetryRepository.Setup(repo => repo.GetAllAsync())
                    .ReturnsAsync(existingTelemetry);
            _mockTelemetryRepository.Setup(repo => repo.AddAsync(telemetryBeforeAdd))
                    .ReturnsAsync(telemetryAfterAdd);
            _mockClientProxy?.Setup(proxy => proxy.SendCoreAsync(NotificationMethods.MapAlert, expectedMapAlertObjects, default))
                    .Returns(Task.CompletedTask);
            _mockHubContext.Setup(h => h.Clients).Returns(_mockHubClients.Object);
            _mockHubClients.Setup(h => h.All).Returns(_mockClientProxy?.Object);

            // Act
            _telemetryService?.CreateTelemetryAsync(telemetry);

            // Assert
            _mockTelemetryRepository.Verify(repo => repo.AddAsync(It.Is<Telemetry>(t => t == telemetry)), Times.Once);
            _mockBeaconRepository.Verify(repo => repo.GetByIdAsync(telemetry.Beacon.ID), Times.Once);
            _mockBeaconRepository.Verify(repo => repo.UpdateAsync(It.Is<Beacon>(b => b.ID == telemetry.Beacon.ID)), Times.Once);

            _mockClientProxy.Verify(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapAlert,
                It.Is<object[]>(args => args[0].Equals(expectedMapAlert)),
                default), Times.Once);
        }

        /// <summary>
        /// Test to ensure that a map alert is created if the telemetry is the second telemetry
        /// for a train and the new telemetry beacin is multi-railroad and existing telemetry beacon
        /// is single-railroad.
        /// </summary>
        [TestMethod]
        public void CreateTelemetry_SecondTelemetry_MultiRailroadBeacon_OneRailroadBeacon()
        {
            var trainAddressId = 90234;
            var train1Source = "HOT";
            var train2Source = "EOT";
            var telemetry1Id = 432;
            var telemetry2Id = 433;
            var owner1Id = 19;
            var owner2Id = 29;
            var beacon1Id = 134;
            var beacon1Latitude = 10.0;
            var beacon1Longitude = 11.0;
            var beacon2Id = 234;
            var beacon2Latitude = 20.0;
            var beacon2Longitude = 21.0;
            var milepost1 = 123.45;
            var milepost2 = 234.56;
            var railroad1Id = 12;
            var railroad2Id = 22;

            // Arrange
            var telemetry = new Telemetry
            {
                ID = telemetry1Id,
                AddressID = trainAddressId,
                Source = train1Source,
                CreatedAt = _currentDateTime.AddMicroseconds(-234), // Simulate beacon's timestamp that will be ignored
                Beacon = new Beacon
                {
                    ID = beacon1Id,
                    OwnerID = owner1Id,
                    CreatedAt = _currentDateTime.AddMilliseconds(-300), // Simulate beacon's timestamp
                }
            };

            var telemetryBeforeAdd = telemetry.GetClone();
            telemetryBeforeAdd.CreatedAt = _currentDateTime; // Timestamp set by APi

            var telemetryAfterAdd = new Telemetry
            {
                ID = telemetry1Id,
                AddressID = trainAddressId,
                Source = train1Source,
                CreatedAt = _currentDateTime,
                Beacon = new Beacon
                {
                    ID = beacon1Id,
                    OwnerID = owner1Id,
                    CreatedAt = _currentDateTime.AddMilliseconds(-300), // Simulate last beacon check-in timestamp
                    BeaconRailroads = [
                        new BeaconRailroad {
                            RailroadID = railroad1Id,
                            BeaconID = beacon1Id,
                            Latitude = beacon1Latitude,
                            Longitude = beacon1Longitude,
                            Milepost = milepost1,
                            Direction = Enums.Direction.NorthSouth
                        },
                        new BeaconRailroad {
                            RailroadID = railroad2Id, // Not the same railroad as the previous telemetry
                            BeaconID = beacon2Id,
                            Latitude = beacon2Latitude,
                            Longitude = beacon2Longitude,
                            Milepost = milepost2,
                            Direction = Enums.Direction.NorthwestSoutheast
                        }
                    ]
                }
            };

            var singleRailroadBeacon = new Beacon
            {
                ID = beacon2Id,
                OwnerID = owner2Id,
                CreatedAt = _currentDateTime.AddMilliseconds(-500),
                BeaconRailroads = [
                            new BeaconRailroad {
                                RailroadID = railroad1Id,
                                BeaconID = beacon2Id,
                                Latitude = beacon2Latitude,
                                Longitude = beacon2Longitude,
                                Milepost = milepost2,
                                Direction = Enums.Direction.NorthSouth
                            }
                        ]
            };

            var existingTelemetry = new List<Telemetry>
            {
                new() {
                    ID = telemetry2Id,
                    AddressID = trainAddressId,
                    Source = train2Source,
                    CreatedAt = _currentDateTime.AddMilliseconds(-500),
                    Beacon = singleRailroadBeacon
                }
            };

            var beaconBeforeUpdate = new Beacon
            {
                ID = beacon1Id,
                OwnerID = owner1Id,
                CreatedAt = _currentDateTime // Should get new timestamp from API
            };

            var beaconAfterUpdate = beaconBeforeUpdate.GetClone();

            var expectedMapAlert = new MapPin
            {
                AddressID = 90234,
                Latitude = beacon1Latitude,
                Longitude = beacon1Longitude,
                Milepost = milepost1,
                Source = train1Source,
                CreatedAt = _currentDateTime,
                Direction = "S"
            };

            var expectedMapAlertObjects = new object[]
            {
                expectedMapAlert
            };

            _mockBeaconRepository.Setup(repo => repo.GetByIdAsync(telemetry.Beacon.ID))
                    .ReturnsAsync(beaconBeforeUpdate);
            _mockBeaconRepository.Setup(repo => repo.UpdateAsync(beaconBeforeUpdate))
                    .ReturnsAsync(beaconAfterUpdate);
            _mockTelemetryRepository.Setup(repo => repo.GetAllAsync())
                    .ReturnsAsync(existingTelemetry);
            _mockTelemetryRepository.Setup(repo => repo.AddAsync(telemetryBeforeAdd))
                    .ReturnsAsync(telemetryAfterAdd);
            _mockClientProxy?.Setup(proxy => proxy.SendCoreAsync(NotificationMethods.MapAlert, expectedMapAlertObjects, default))
                    .Returns(Task.CompletedTask);
            _mockHubContext.Setup(h => h.Clients).Returns(_mockHubClients.Object);
            _mockHubClients.Setup(h => h.All).Returns(_mockClientProxy?.Object);

            // Act
            _telemetryService?.CreateTelemetryAsync(telemetry);

            // Assert
            _mockTelemetryRepository.Verify(repo => repo.AddAsync(It.Is<Telemetry>(t => t == telemetry)), Times.Once);
            _mockBeaconRepository.Verify(repo => repo.GetByIdAsync(telemetry.Beacon.ID), Times.Once);
            _mockBeaconRepository.Verify(repo => repo.UpdateAsync(It.Is<Beacon>(b => b.ID == telemetry.Beacon.ID)), Times.Once);

            _mockClientProxy.Verify(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapAlert,
                It.Is<object[]>(args => args[0].Equals(expectedMapAlert)),
                default), Times.Once);
        }

        /// <summary>
        /// Test to ensure that a map alert is created if the telemetry is the second telemetry
        /// for a train and both the new telemetry and existing telemetry beacons are multi-railroad.
        /// </summary>
        [TestMethod]
        public void CreateTelemetry_SecondTelemetry_MultiRailroadBeacon_MultiRailroadBeacon()
        {
            var trainAddressId = 90234;
            var train1Source = "HOT";
            var train2Source = "EOT";
            var telemetry1Id = 432;
            var telemetry2Id = 433;
            var owner1Id = 19;
            var owner2Id = 29;
            var beacon1Id = 134;
            var beacon1Latitude = 10.0;
            var beacon1Longitude = 11.0;
            var beacon2Id = 234;
            var beacon2Latitude = 20.0;
            var beacon2Longitude = 21.0;
            var beacon3Latitude = 30.0;
            var beacon3Longitude = 31.0;
            var milepost1 = 123.45;
            var milepost2 = 234.56;
            var milepost3 = 345.67;
            var railroad1Id = 12;
            var railroad2Id = 22;
            var railroad3Id = 32;

            // Arrange
            var telemetry = new Telemetry
            {
                ID = telemetry1Id,
                AddressID = trainAddressId,
                Source = train1Source,
                CreatedAt = _currentDateTime.AddMicroseconds(-234), // Simulate beacon's timestamp that will be ignored
                Beacon = new Beacon
                {
                    ID = beacon1Id,
                    OwnerID = owner1Id,
                    CreatedAt = _currentDateTime.AddMilliseconds(-300), // Simulate beacon's timestamp
                }
            };

            var telemetryBeforeAdd = telemetry.GetClone();
            telemetryBeforeAdd.CreatedAt = _currentDateTime; // Timestamp set by APi

            var telemetryAfterAdd = new Telemetry
            {
                ID = telemetry1Id,
                AddressID = trainAddressId,
                Source = train1Source,
                CreatedAt = _currentDateTime,
                Beacon = new Beacon
                {
                    ID = beacon1Id,
                    OwnerID = owner1Id,
                    CreatedAt = _currentDateTime.AddMilliseconds(-300), // Simulate last beacon check-in timestamp
                    BeaconRailroads = [
                        new BeaconRailroad {
                            RailroadID = railroad1Id,
                            BeaconID = beacon1Id,
                            Latitude = beacon1Latitude,
                            Longitude = beacon1Longitude,
                            Milepost = milepost1,
                            Direction = Enums.Direction.NorthSouth
                        },
                        new BeaconRailroad {
                            RailroadID = railroad2Id, // Not the same railroad as the previous telemetry
                            BeaconID = beacon2Id,
                            Latitude = beacon2Latitude,
                            Longitude = beacon2Longitude,
                            Milepost = milepost2,
                            Direction = Enums.Direction.NorthwestSoutheast
                        }
                    ]
                }
            };

            var multipleRailroadBeacon = new Beacon
            {
                ID = beacon2Id,
                OwnerID = owner2Id,
                CreatedAt = _currentDateTime.AddMilliseconds(-500),
                BeaconRailroads = [
                            new BeaconRailroad {
                                RailroadID = railroad1Id,
                                BeaconID = beacon2Id,
                                Latitude = beacon2Latitude,
                                Longitude = beacon2Longitude,
                                Milepost = milepost2,
                                Direction = Enums.Direction.NorthSouth
                            },
                            new BeaconRailroad {
                                RailroadID = railroad3Id, // Not the same railroad as the new telemetry
                                BeaconID = beacon2Id,
                                Latitude = beacon3Latitude,
                                Longitude = beacon3Longitude,
                                Milepost = milepost3,
                                Direction = Enums.Direction.NorthwestSoutheast
                            }
                        ]
            };

            var existingTelemetry = new List<Telemetry>
            {
                new() {
                    ID = telemetry2Id,
                    AddressID = trainAddressId,
                    Source = train2Source,
                    CreatedAt = _currentDateTime.AddMilliseconds(-500),
                    Beacon = multipleRailroadBeacon
                }
            };

            var beaconBeforeUpdate = new Beacon
            {
                ID = beacon1Id,
                OwnerID = owner1Id,
                CreatedAt = _currentDateTime // Should get new timestamp from API
            };

            var beaconAfterUpdate = beaconBeforeUpdate.GetClone();

            var expectedMapAlert = new MapPin
            {
                AddressID = 90234,
                Latitude = beacon1Latitude,
                Longitude = beacon1Longitude,
                Milepost = milepost1,
                Source = train1Source,
                CreatedAt = _currentDateTime,
                Direction = "S"
            };

            var expectedMapAlertObjects = new object[]
            {
                expectedMapAlert
            };

            _mockBeaconRepository.Setup(repo => repo.GetByIdAsync(telemetry.Beacon.ID))
                    .ReturnsAsync(beaconBeforeUpdate);
            _mockBeaconRepository.Setup(repo => repo.UpdateAsync(beaconBeforeUpdate))
                    .ReturnsAsync(beaconAfterUpdate);
            _mockTelemetryRepository.Setup(repo => repo.GetAllAsync())
                    .ReturnsAsync(existingTelemetry);
            _mockTelemetryRepository.Setup(repo => repo.AddAsync(telemetryBeforeAdd))
                    .ReturnsAsync(telemetryAfterAdd);
            _mockClientProxy?.Setup(proxy => proxy.SendCoreAsync(NotificationMethods.MapAlert, expectedMapAlertObjects, default))
                    .Returns(Task.CompletedTask);
            _mockHubContext.Setup(h => h.Clients).Returns(_mockHubClients.Object);
            _mockHubClients.Setup(h => h.All).Returns(_mockClientProxy?.Object);

            // Act
            _telemetryService?.CreateTelemetryAsync(telemetry);

            // Assert
            _mockTelemetryRepository.Verify(repo => repo.AddAsync(It.Is<Telemetry>(t => t == telemetry)), Times.Once);
            _mockBeaconRepository.Verify(repo => repo.GetByIdAsync(telemetry.Beacon.ID), Times.Once);
            _mockBeaconRepository.Verify(repo => repo.UpdateAsync(It.Is<Beacon>(b => b.ID == telemetry.Beacon.ID)), Times.Once);

            _mockClientProxy.Verify(proxy => proxy.SendCoreAsync(
                NotificationMethods.MapAlert,
                It.Is<object[]>(args => args[0].Equals(expectedMapAlert)),
                default), Times.Once);
        }
    }
}