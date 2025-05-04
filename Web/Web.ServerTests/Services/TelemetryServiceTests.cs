using AutoMapper;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Web.Server.Entities;
using Web.Server.Hubs;
using Web.Server.Mappers;

namespace Web.Server.Services.Tests
{
    // https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-csharp-with-mstest
    // https://learn.microsoft.com/en-us/ef/ef6/fundamentals/testing/mocking
    [TestClass()]
    public class TelemetryServiceTests
    {
        private TelemetryService? _telemetryService;

        private Mock<IBeaconRepository> _mockBeaconRepository;
        private Mock<ITelemetryRepository> _mockTelemetryRepository;
        private Mock<DbSet<Telemetry>> _mockAlertDbSet;
        private Mock<IHubContext<NotificationHub>> _mockHubContext;
        private IMapper _mapper; // TODO: Don't mock this.

        [TestInitialize()]
        public void Initialize()
        {
            _mockBeaconRepository = new Mock<IBeaconRepository>();
            _mockHubContext = new Mock<IHubContext<NotificationHub>>();
            _mockTelemetryRepository = new Mock<ITelemetryRepository>();
            var config = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<AutoMapperProfile>();
            });
            _mapper = config.CreateMapper();
            _mockAlertDbSet = new Mock<DbSet<Telemetry>>();

            _telemetryService = new TelemetryService(_mockHubContext.Object, _mockTelemetryRepository.Object, _mockBeaconRepository.Object, _mapper);
        }

        [TestMethod()]
        public void ProcessTelemetryTest()
        {
            // Arrange
            _mockBeaconRepository.Setup(m => m.Set<Telemetry>()).Returns(_mockAlertDbSet.Object);

            var alert = new Telemetry
            {
                ID = 1,
                Beacon = "123",
                AddressID = 1,
                TrainID = 2,
                Moving = true,
                Source = "HOT",
                Timestamp = DateTime.UtcNow
            };

            // Act
            _telemetryService.CreateTelemetry(alert);

            // Assert
            _mockAlertDbSet.Verify(m => m.Add(alert), Times.Once);
            _mockBeaconRepository.Verify(m => m.SaveChanges(), Times.Once);
        }
    }
}