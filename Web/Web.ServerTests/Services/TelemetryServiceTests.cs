using AutoMapper;
using Microsoft.AspNetCore.SignalR;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        private IMapper _mapper;

        [TestInitialize]
        public void Initialize()
        {
            _mockBeaconRepository = new Mock<IBeaconRepository>();
            _mockTelemetryRepository = new Mock<ITelemetryRepository>();
            _mockHubContext = new Mock<IHubContext<NotificationHub>>();

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
                Beacon = new Beacon { ID = 1, Latitude = 10.0, Longitude = 20.0, Timestamp = DateTime.UtcNow }
            };

            var existingBeacon = new Beacon
            {
                ID = 1,
                Latitude = 10.0,
                Longitude = 20.0,
                Timestamp = DateTime.UtcNow
            };

            _mockTelemetryRepository.Setup(repo => repo.AddAsync(It.IsAny<Telemetry>()))
                .ReturnsAsync(telemetry);
            _mockBeaconRepository.Setup(repo => repo.GetByIdAsync(telemetry.Beacon.ID))
                .ReturnsAsync(existingBeacon);
            _mockBeaconRepository.Setup(repo => repo.UpdateAsync(It.IsAny<Beacon>()))
                .ReturnsAsync(existingBeacon);

            // Act
            _telemetryService.CreateTelemetry(telemetry);

            // Assert
            _mockTelemetryRepository.Verify(repo => repo.AddAsync(It.Is<Telemetry>(t => t == telemetry)), Times.Once);
            _mockBeaconRepository.Verify(repo => repo.GetByIdAsync(telemetry.Beacon.ID), Times.Once);
            _mockBeaconRepository.Verify(repo => repo.UpdateAsync(It.Is<Beacon>(b => b.ID == telemetry.Beacon.ID)), Times.Once);
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