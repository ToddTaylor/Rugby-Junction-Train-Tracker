using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Web.Server.Data;
using Web.Server.DTOs;
using Web.Server.Entities;
using Web.Server.Enums;
using Web.Server.Hubs;
using Web.Server.Mappers;
using Web.Server.Services;

namespace Web.ServerTests.Services
{
    [TestClass]
    public class BeaconRailroadHealthServiceTests
    {
        private Mock<IHubContext<NotificationHub>> _hubContextMock;
        private Mock<IServiceScopeFactory> _scopeFactoryMock;
        private Mock<IServiceScope> _scopeMock;
        private Mock<IServiceProvider> _serviceProviderMock;
        private IMapper _mapper;

        [TestInitialize]
        public void Setup()
        {
            _hubContextMock = new Mock<IHubContext<NotificationHub>>();
            _scopeFactoryMock = new Mock<IServiceScopeFactory>();
            _scopeMock = new Mock<IServiceScope>();
            _serviceProviderMock = new Mock<IServiceProvider>();

            var clientsMock = new Mock<IHubClients>();
            var clientProxyMock = new Mock<IClientProxy>();
            clientsMock.Setup(c => c.All).Returns(clientProxyMock.Object);
            _hubContextMock.Setup(h => h.Clients).Returns(clientsMock.Object);

            _scopeMock.Setup(s => s.ServiceProvider).Returns(_serviceProviderMock.Object);
            _scopeFactoryMock.Setup(f => f.CreateScope()).Returns(_scopeMock.Object);

            var config = new TypeAdapterConfig();
            config.Scan(typeof(MapsterProfile).Assembly);
            _mapper = new ServiceMapper(new ServiceCollection().BuildServiceProvider(), config);
        }

        private TelemetryDbContext CreateInMemoryDbContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<TelemetryDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;
            return new TelemetryDbContext(options);
        }

        private IConfiguration BuildConfig(int defaultHours = 6)
        {
            var inMemorySettings = new Dictionary<string, string?>
            {
                { "ApplicationSettings:TelemetryStaleHoursDefault", defaultHours.ToString() }
            };
            return new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();
        }

        private BeaconRailroadHealthService CreateService(IConfiguration config)
        {
            return new BeaconRailroadHealthService(
                _hubContextMock.Object,
                _mapper,
                _scopeFactoryMock.Object,
                config);
        }

        // ── Threshold resolution ────────────────────────────────────────────────

        [TestMethod]
        public void EffectiveThreshold_UsesOverride_WhenOverrideIsSet()
        {
            // The BeaconRailroadHealthService reads per-record override first.
            // We verify this by creating a beacon with an override and checking that
            // TelemetryStale is computed using the override threshold.
            var dbName = nameof(EffectiveThreshold_UsesOverride_WhenOverrideIsSet);
            using var dbContext = CreateInMemoryDbContext(dbName);

            // Beacon with a 2-hour override; telemetry received 3 hours ago → stale
            SeedHealthyBeacon(dbContext, beaconId: 1, subdivisionId: 1, telemetryStaleHoursOverride: 2,
                lastHealthUpdate: DateTime.UtcNow,
                lastTelemetryUpdate: DateTime.UtcNow.AddHours(-3));

            _serviceProviderMock.Setup(sp => sp.GetService(typeof(TelemetryDbContext))).Returns(dbContext);

            var config = BuildConfig(defaultHours: 6);
            var sentDTOs = CaptureBeaconUpdate();

            // Act: run one iteration
            RunOneIteration(config, dbContext);

            // Assert: TelemetryStale = true because 3h > 2h override
            Assert.IsTrue(sentDTOs.Any(), "Should have sent at least one DTO");
            var dto = sentDTOs.First(d => d.BeaconID == 1);
            Assert.IsTrue(dto.Online, "Beacon should be online (health is fresh)");
            Assert.IsTrue(dto.TelemetryStale, "TelemetryStale should be true — 3h > 2h override");
        }

        [TestMethod]
        public void EffectiveThreshold_UsesDefault_WhenOverrideIsNull()
        {
            // Beacon has no override; telemetry received 7 hours ago with default 6h → stale
            var dbName = nameof(EffectiveThreshold_UsesDefault_WhenOverrideIsNull);
            using var dbContext = CreateInMemoryDbContext(dbName);

            SeedHealthyBeacon(dbContext, beaconId: 1, subdivisionId: 1, telemetryStaleHoursOverride: null,
                lastHealthUpdate: DateTime.UtcNow,
                lastTelemetryUpdate: DateTime.UtcNow.AddHours(-7));

            _serviceProviderMock.Setup(sp => sp.GetService(typeof(TelemetryDbContext))).Returns(dbContext);

            var config = BuildConfig(defaultHours: 6);
            var sentDTOs = CaptureBeaconUpdate();

            RunOneIteration(config, dbContext);

            var dto = sentDTOs.First(d => d.BeaconID == 1);
            Assert.IsTrue(dto.Online);
            Assert.IsTrue(dto.TelemetryStale, "TelemetryStale should be true — 7h > default 6h");
        }

        [TestMethod]
        public void EffectiveThreshold_NotStale_WhenTelemetryWithinDefault()
        {
            // Telemetry received 3 hours ago with default 6h → not stale
            var dbName = nameof(EffectiveThreshold_NotStale_WhenTelemetryWithinDefault);
            using var dbContext = CreateInMemoryDbContext(dbName);

            SeedHealthyBeacon(dbContext, beaconId: 1, subdivisionId: 1, telemetryStaleHoursOverride: null,
                lastHealthUpdate: DateTime.UtcNow,
                lastTelemetryUpdate: DateTime.UtcNow.AddHours(-3));

            _serviceProviderMock.Setup(sp => sp.GetService(typeof(TelemetryDbContext))).Returns(dbContext);

            var config = BuildConfig(defaultHours: 6);
            var sentDTOs = CaptureBeaconUpdate();

            RunOneIteration(config, dbContext);

            var dto = sentDTOs.First(d => d.BeaconID == 1);
            Assert.IsTrue(dto.Online);
            Assert.IsFalse(dto.TelemetryStale, "TelemetryStale should be false — 3h < default 6h");
        }

        [TestMethod]
        public void EffectiveThreshold_NotStale_WhenTelemetryWithinOverride()
        {
            // Telemetry received 1 hour ago with 2h override → not stale
            var dbName = nameof(EffectiveThreshold_NotStale_WhenTelemetryWithinOverride);
            using var dbContext = CreateInMemoryDbContext(dbName);

            SeedHealthyBeacon(dbContext, beaconId: 1, subdivisionId: 1, telemetryStaleHoursOverride: 2,
                lastHealthUpdate: DateTime.UtcNow,
                lastTelemetryUpdate: DateTime.UtcNow.AddHours(-1));

            _serviceProviderMock.Setup(sp => sp.GetService(typeof(TelemetryDbContext))).Returns(dbContext);

            var config = BuildConfig(defaultHours: 6);
            var sentDTOs = CaptureBeaconUpdate();

            RunOneIteration(config, dbContext);

            var dto = sentDTOs.First(d => d.BeaconID == 1);
            Assert.IsTrue(dto.Online);
            Assert.IsFalse(dto.TelemetryStale, "TelemetryStale should be false — 1h < 2h override");
        }

        // ── State classification ────────────────────────────────────────────────

        [TestMethod]
        public void StateClassification_HealthOffline_IsGrayState()
        {
            // Beacon with LastUpdate > 15 min ago → Online = false, TelemetryStale = false
            var dbName = nameof(StateClassification_HealthOffline_IsGrayState);
            using var dbContext = CreateInMemoryDbContext(dbName);

            SeedHealthyBeacon(dbContext, beaconId: 1, subdivisionId: 1, telemetryStaleHoursOverride: null,
                lastHealthUpdate: DateTime.UtcNow.AddMinutes(-20), // offline
                lastTelemetryUpdate: DateTime.UtcNow.AddHours(-1));

            _serviceProviderMock.Setup(sp => sp.GetService(typeof(TelemetryDbContext))).Returns(dbContext);

            var config = BuildConfig(defaultHours: 6);
            var sentDTOs = CaptureBeaconUpdate();

            RunOneIteration(config, dbContext);

            var dto = sentDTOs.First(d => d.BeaconID == 1);
            Assert.IsFalse(dto.Online, "Beacon should be offline (health-offline = gray state)");
            Assert.IsFalse(dto.TelemetryStale, "TelemetryStale should be false when health-offline");
        }

        [TestMethod]
        public void StateClassification_HealthOnline_TelemetryStale_IsBlueRingState()
        {
            // Beacon: health is fresh (within 15 min), telemetry is stale (> threshold)
            var dbName = nameof(StateClassification_HealthOnline_TelemetryStale_IsBlueRingState);
            using var dbContext = CreateInMemoryDbContext(dbName);

            SeedHealthyBeacon(dbContext, beaconId: 1, subdivisionId: 1, telemetryStaleHoursOverride: null,
                lastHealthUpdate: DateTime.UtcNow.AddMinutes(-5), // health online
                lastTelemetryUpdate: DateTime.UtcNow.AddHours(-8)); // telemetry stale (> 6h default)

            _serviceProviderMock.Setup(sp => sp.GetService(typeof(TelemetryDbContext))).Returns(dbContext);

            var config = BuildConfig(defaultHours: 6);
            var sentDTOs = CaptureBeaconUpdate();

            RunOneIteration(config, dbContext);

            var dto = sentDTOs.First(d => d.BeaconID == 1);
            Assert.IsTrue(dto.Online, "Beacon should be online (blue-ring state)");
            Assert.IsTrue(dto.TelemetryStale, "TelemetryStale should be true (blue-ring state)");
        }

        [TestMethod]
        public void StateClassification_TelemetryOlderThanTelemetriesRetentionWindow_StillReportsStale()
        {
            // Reproduces the "Eagle" production bug: RecordCleanupService purges raw Telemetries
            // rows after 12 hours, but a beacon can easily go 12+ hours without a real train on a
            // low-traffic subdivision. The last train passage (17h ago, well past that window, and
            // well past a 1h override) must still be visible via MapPinHistories (48h retention).
            var dbName = nameof(StateClassification_TelemetryOlderThanTelemetriesRetentionWindow_StillReportsStale);
            using var dbContext = CreateInMemoryDbContext(dbName);

            SeedHealthyBeacon(dbContext, beaconId: 1, subdivisionId: 1, telemetryStaleHoursOverride: 1,
                lastHealthUpdate: DateTime.UtcNow, // health-ping still fresh, beacon appears online
                lastTelemetryUpdate: DateTime.UtcNow.AddHours(-17)); // last real train, well past both the 1h override and the 12h Telemetries retention window

            _serviceProviderMock.Setup(sp => sp.GetService(typeof(TelemetryDbContext))).Returns(dbContext);

            var config = BuildConfig(defaultHours: 6);
            var sentDTOs = CaptureBeaconUpdate();

            RunOneIteration(config, dbContext);

            var dto = sentDTOs.First(d => d.BeaconID == 1);
            Assert.IsTrue(dto.Online, "Health-ping is fresh, so beacon should still appear online");
            Assert.IsTrue(dto.TelemetryStale,
                "Last train was 17h ago (> 1h override); this must be reported stale even though a " +
                "Telemetries row that old would already have been purged by RecordCleanupService");
        }

        [TestMethod]
        public void StateClassification_HealthyBeacon_NoStaleFlags()
        {
            // Beacon: health fresh and telemetry fresh → fully healthy
            var dbName = nameof(StateClassification_HealthyBeacon_NoStaleFlags);
            using var dbContext = CreateInMemoryDbContext(dbName);

            SeedHealthyBeacon(dbContext, beaconId: 1, subdivisionId: 1, telemetryStaleHoursOverride: null,
                lastHealthUpdate: DateTime.UtcNow.AddMinutes(-5),
                lastTelemetryUpdate: DateTime.UtcNow.AddHours(-2)); // within 6h default

            _serviceProviderMock.Setup(sp => sp.GetService(typeof(TelemetryDbContext))).Returns(dbContext);

            var config = BuildConfig(defaultHours: 6);
            var sentDTOs = CaptureBeaconUpdate();

            RunOneIteration(config, dbContext);

            var dto = sentDTOs.First(d => d.BeaconID == 1);
            Assert.IsTrue(dto.Online, "Beacon should be online");
            Assert.IsFalse(dto.TelemetryStale, "TelemetryStale should be false for healthy beacon");
        }

        [TestMethod]
        public void StateClassification_NoTelemetryEver_NotConsideredStale()
        {
            // Beacon: health fresh, but no telemetry records at all → TelemetryStale = false
            var dbName = nameof(StateClassification_NoTelemetryEver_NotConsideredStale);
            using var dbContext = CreateInMemoryDbContext(dbName);

            // Add beacon railroad with no telemetry
            var beacon = new Beacon
            {
                ID = 1,
                OwnerID = 1,
                Name = "TestBeacon",
                CreatedAt = DateTime.UtcNow.AddMinutes(-5),
                LastUpdate = DateTime.UtcNow.AddMinutes(-5)
            };
            var subdivision = new Subdivision { ID = 1, Name = "TestSub", RailroadID = 1, CustodianId = null };
            var railroad = new Railroad { ID = 1, Name = "TestRR" };
            subdivision.Railroad = railroad;

            dbContext.Railroads.Add(railroad);
            dbContext.Subdivisions.Add(subdivision);
            dbContext.Beacons.Add(beacon);
            dbContext.BeaconRailroads.Add(new BeaconRailroad
            {
                BeaconID = 1,
                SubdivisionID = 1,
                Beacon = beacon,
                Subdivision = subdivision,
                Direction = Direction.All,
                Latitude = 0,
                Longitude = 0,
                Milepost = 0,
                MultipleTracks = false,
                CreatedAt = DateTime.UtcNow.AddMinutes(-5),
                LastUpdate = DateTime.UtcNow.AddMinutes(-5) // health online
            });
            dbContext.SaveChanges();

            _serviceProviderMock.Setup(sp => sp.GetService(typeof(TelemetryDbContext))).Returns(dbContext);

            var config = BuildConfig(defaultHours: 6);
            var sentDTOs = CaptureBeaconUpdate();

            RunOneIteration(config, dbContext);

            var dto = sentDTOs.First(d => d.BeaconID == 1);
            Assert.IsTrue(dto.Online, "Beacon should be online");
            Assert.IsFalse(dto.TelemetryStale, "TelemetryStale should be false when no telemetry ever received");
        }

        // ── TelemetryStaleHoursOverride passthrough in DTO ────────────────────

        [TestMethod]
        public void OverrideField_IsIncludedInSentDTO()
        {
            var dbName = nameof(OverrideField_IsIncludedInSentDTO);
            using var dbContext = CreateInMemoryDbContext(dbName);

            SeedHealthyBeacon(dbContext, beaconId: 1, subdivisionId: 1, telemetryStaleHoursOverride: 12,
                lastHealthUpdate: DateTime.UtcNow,
                lastTelemetryUpdate: DateTime.UtcNow.AddHours(-1));

            _serviceProviderMock.Setup(sp => sp.GetService(typeof(TelemetryDbContext))).Returns(dbContext);

            var config = BuildConfig(defaultHours: 6);
            var sentDTOs = CaptureBeaconUpdate();

            RunOneIteration(config, dbContext);

            var dto = sentDTOs.First(d => d.BeaconID == 1);
            Assert.AreEqual(12, dto.TelemetryStaleHoursOverride, "TelemetryStaleHoursOverride should be 12 as set on the entity");
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private List<BeaconRailroadDTO> CaptureBeaconUpdate()
        {
            var captured = new List<BeaconRailroadDTO>();
            var clientsMock = new Mock<IHubClients>();
            var clientProxyMock = new Mock<IClientProxy>();

            clientProxyMock
                .Setup(c => c.SendCoreAsync(
                    NotificationMethods.BeaconUpdate,
                    It.IsAny<object[]>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, object[], CancellationToken>((method, args, ct) =>
                {
                    if (args.Length > 0 && args[0] is IEnumerable<BeaconRailroadDTO> dtos)
                        captured.AddRange(dtos);
                })
                .Returns(Task.CompletedTask);

            clientsMock.Setup(c => c.All).Returns(clientProxyMock.Object);
            _hubContextMock.Setup(h => h.Clients).Returns(clientsMock.Object);

            return captured;
        }

        private void RunOneIteration(IConfiguration config, TelemetryDbContext dbContext)
        {
            // Bypass the background service loop by directly calling the logic
            // via a test-accessible shim using reflection on a testable subclass.
            var service = new TestableBeaconRailroadHealthService(
                _hubContextMock.Object, _mapper, _scopeFactoryMock.Object, config, dbContext);

            service.RunIteration().GetAwaiter().GetResult();
        }

        private void SeedHealthyBeacon(TelemetryDbContext dbContext, int beaconId, int subdivisionId,
            int? telemetryStaleHoursOverride, DateTime lastHealthUpdate, DateTime lastTelemetryUpdate)
        {
            var beacon = new Beacon
            {
                ID = beaconId,
                OwnerID = 1,
                Name = $"Beacon{beaconId}",
                CreatedAt = lastHealthUpdate,
                LastUpdate = lastHealthUpdate
            };
            var railroad = new Railroad { ID = subdivisionId, Name = $"RR{subdivisionId}" };
            var subdivision = new Subdivision
            {
                ID = subdivisionId,
                Name = $"Sub{subdivisionId}",
                RailroadID = subdivisionId,
                Railroad = railroad,
                CustodianId = null
            };

            dbContext.Railroads.Add(railroad);
            dbContext.Subdivisions.Add(subdivision);
            dbContext.Beacons.Add(beacon);
            dbContext.BeaconRailroads.Add(new BeaconRailroad
            {
                BeaconID = beaconId,
                SubdivisionID = subdivisionId,
                Beacon = beacon,
                Subdivision = subdivision,
                Direction = Direction.All,
                Latitude = 0,
                Longitude = 0,
                Milepost = 0,
                MultipleTracks = false,
                TelemetryStaleHoursOverride = telemetryStaleHoursOverride,
                CreatedAt = lastHealthUpdate,
                LastUpdate = lastHealthUpdate
            });
            dbContext.MapPinHistories.Add(new MapPinHistory
            {
                BeaconID = beaconId,
                SubdivisionId = subdivisionId,
                AddressesJson = "[]",
                CreatedAt = lastTelemetryUpdate,
                LastUpdate = lastTelemetryUpdate
            });
            dbContext.SaveChanges();
        }
    }

    /// <summary>
    /// Testable subclass of BeaconRailroadHealthService that exposes the iteration
    /// logic directly without requiring the background service loop.
    /// </summary>
    internal class TestableBeaconRailroadHealthService : BeaconRailroadHealthService
    {
        private readonly TelemetryDbContext _testDbContext;

        public TestableBeaconRailroadHealthService(
            IHubContext<NotificationHub> hubContext,
            IMapper mapper,
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration,
            TelemetryDbContext testDbContext)
            : base(hubContext, mapper, scopeFactory, configuration)
        {
            _testDbContext = testDbContext;
        }

        public async Task RunIteration()
        {
            await ComputeAndSendBeaconStatusAsync(_testDbContext, CancellationToken.None);
        }
    }
}
