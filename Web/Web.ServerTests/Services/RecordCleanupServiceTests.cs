using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Web.Server.Data;
using Web.Server.Entities;
using Web.Server.Services;

namespace Web.ServerTests.Services
{
    [TestClass]
    public class RecordCleanupServiceTests
    {
        private static readonly DateTime Now = new(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);

        private static TelemetryDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<TelemetryDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new TelemetryDbContext(options);
        }

        [TestMethod]
        public async Task CleanUpOnce_RemovesOnlyRecordsPastTheirRetention()
        {
            using var dbContext = CreateInMemoryDbContext();
            dbContext.Telemetries.AddRange(
                new Telemetry { ID = 1, BeaconID = 1, AddressID = 1, Source = "EOT", CreatedAt = Now.AddHours(-13), LastUpdate = Now.AddHours(-13) },
                new Telemetry { ID = 2, BeaconID = 1, AddressID = 1, Source = "EOT", CreatedAt = Now.AddHours(-11), LastUpdate = Now.AddHours(-11) });
            dbContext.MapPins.AddRange(
                new MapPin { ID = 1, BeaconID = 1, SubdivisionId = 1, CreatedAt = Now.AddHours(-13), LastUpdate = Now.AddHours(-13) },
                new MapPin { ID = 2, BeaconID = 1, SubdivisionId = 1, CreatedAt = Now.AddHours(-11), LastUpdate = Now.AddHours(-11) });
            dbContext.MapPinHistories.AddRange(
                new MapPinHistory { ID = 1, BeaconID = 1, SubdivisionId = 1, AddressesJson = "[]", CreatedAt = Now.AddHours(-49), LastUpdate = Now.AddHours(-49) },
                new MapPinHistory { ID = 2, BeaconID = 1, SubdivisionId = 1, AddressesJson = "[]", CreatedAt = Now.AddHours(-47), LastUpdate = Now.AddHours(-47) });
            await dbContext.SaveChangesAsync();

            await TestableRecordCleanupService.CleanUpOnce(dbContext, Now);

            CollectionAssert.AreEqual(new[] { 2 }, dbContext.Telemetries.Select(t => t.ID).ToArray());
            CollectionAssert.AreEqual(new[] { 2 }, dbContext.MapPins.Select(p => p.ID).ToArray());
            CollectionAssert.AreEqual(new[] { 2 }, dbContext.MapPinHistories.Select(h => h.ID).ToArray());
        }

        [TestMethod]
        public async Task FailedCleanup_IsLoggedAndDoesNotStopTheService()
        {
            var scopeFactory = new Mock<IServiceScopeFactory>();
            scopeFactory.Setup(f => f.CreateScope()).Throws(new InvalidOperationException("database is locked"));
            var logger = new Mock<ILogger<RecordCleanupService>>();
            var errorLogged = new TaskCompletionSource();
            logger
                .Setup(l => l.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
                .Callback(() => errorLogged.TrySetResult());

            using var service = new RecordCleanupService(scopeFactory.Object, logger.Object);
            await service.StartAsync(CancellationToken.None);

            await errorLogged.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.IsNotNull(service.ExecuteTask);
            Assert.IsFalse(service.ExecuteTask.IsCompleted, "The cleanup loop should keep running after a failed cycle.");

            await service.StopAsync(CancellationToken.None);
        }

        private sealed class TestableRecordCleanupService : RecordCleanupService
        {
            private TestableRecordCleanupService() : base(null!, null!) { }

            public static Task CleanUpOnce(TelemetryDbContext dbContext, DateTime utcNow) =>
                CleanUpOnceAsync(dbContext, utcNow, CancellationToken.None);
        }
    }
}
