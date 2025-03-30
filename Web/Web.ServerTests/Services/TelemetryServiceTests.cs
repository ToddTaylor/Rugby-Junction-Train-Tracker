using AutoMapper;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Web.Server.Hubs;
using Web.Server.Mappers;
using Web.Server.Models;

namespace Web.Server.Services.Tests
{
    // https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-csharp-with-mstest
    // https://learn.microsoft.com/en-us/ef/ef6/fundamentals/testing/mocking
    [TestClass()]
    public class TelemetryServiceTests
    {
        private TelemetryService _telemetryService;

        private Mock<DbContext> _mockDbContext;
        private Mock<DbSet<Alert>> _mockAlertDbSet;
        private Mock<IHubContext<NotificationHub>> _mockHubContext;
        private IMapper _mapper; // TODO: Don't mock this.

        [TestInitialize()]
        public void Initialize()
        {
            _mockDbContext = new Mock<DbContext>();
            _mockHubContext = new Mock<IHubContext<NotificationHub>>();
            var config = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<AutoMapperProfile>();
            });
            _mapper = config.CreateMapper();
            _mockAlertDbSet = new Mock<DbSet<Alert>>();

            _telemetryService = new TelemetryService(_mockHubContext.Object, _mapper, _mockDbContext.Object);
        }

        [TestMethod()]
        public void ProcessTelemetryTest()
        {
            // Arrange
            _mockDbContext.Setup(m => m.Set<Alert>()).Returns(_mockAlertDbSet.Object);

            var alert = new Alert
            {
                BeaconID = "123",
                AddressID = 1,
                TrainID = 2,
                Latitude = 45.0,
                Longitude = -93.0,
                Moving = true,
                Source = "HOT",
                Timestamp = DateTime.UtcNow
            };

            // Act
            _telemetryService.ProcessTelemetry(alert);

            // Assert
            _mockAlertDbSet.Verify(m => m.Add(alert), Times.Once);
            _mockDbContext.Verify(m => m.SaveChanges(), Times.Once);
        }
    }
}