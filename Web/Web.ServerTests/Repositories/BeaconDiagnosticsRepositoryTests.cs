using Microsoft.EntityFrameworkCore;
using Moq;
using Web.Server.Data;
using Web.Server.Entities;
using Web.Server.Providers;
using Web.Server.Repositories;

namespace Web.ServerTests.Repositories;

[TestClass]
public class BeaconDiagnosticsRepositoryTests
{
    private static readonly DateTime Now = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);

    [TestMethod]
    public async Task Upsert_StoresReport_ThenReplacesItOnTheNextCheckIn()
    {
        await using var context = await CreateSqliteContextAsync();
        var repository = new BeaconDiagnosticsRepository(context, TimeProviderAt(Now));

        await repository.UpsertAsync(new BeaconDiagnostics
        {
            BeaconID = 1,
            CollectedAt = Now.AddSeconds(-5),
            Hostname = "RJ-SDR01",
            LastBootAt = Now.AddDays(-3),
            Programs = [new DiagnosticsProgramStatus { Name = "SoftEOT", Running = 1 }],
            Logs = [new DiagnosticsLogFileStatus { Prefix = "eot", LastWriteAt = Now.AddMinutes(-12) }],
            StartupTasks = [new DiagnosticsStartupTaskStatus { Name = "Start SoftEOT", Found = true, LastResult = 0 }],
        });

        var laterRepository = new BeaconDiagnosticsRepository(context, TimeProviderAt(Now.AddMinutes(2)));
        await laterRepository.UpsertAsync(new BeaconDiagnostics
        {
            BeaconID = 1,
            CollectedAt = Now.AddMinutes(2),
            Hostname = "RJ-SDR01",
            Programs = [new DiagnosticsProgramStatus { Name = "SoftEOT", Running = 2 }],
        });

        context.ChangeTracker.Clear();
        var stored = (await repository.GetAllAsync()).Single();

        Assert.AreEqual(Now, stored.CreatedAt);
        Assert.AreEqual(Now.AddMinutes(2), stored.LastUpdate);
        Assert.AreEqual(DateTimeKind.Utc, stored.CollectedAt.Kind);
        Assert.IsNull(stored.LastBootAt);
        Assert.AreEqual(2, stored.Programs.Single().Running);
        Assert.AreEqual(0, stored.Logs.Count);
        Assert.AreEqual(0, stored.StartupTasks.Count);
    }

    [TestMethod]
    public async Task Upsert_RoundTripsJsonDetailsWithUtcTimes()
    {
        await using var context = await CreateSqliteContextAsync();
        var repository = new BeaconDiagnosticsRepository(context, TimeProviderAt(Now));

        await repository.UpsertAsync(new BeaconDiagnostics
        {
            BeaconID = 1,
            CollectedAt = Now,
            Logs = [new DiagnosticsLogFileStatus { Prefix = "dpu1", LastWriteAt = Now.AddMinutes(-3) }],
            StartupTasks = [new DiagnosticsStartupTaskStatus { Name = "Start SDR#", Found = true, LastResult = 1, LastRunAt = Now.AddHours(-6) }],
        });

        context.ChangeTracker.Clear();
        var stored = (await repository.GetAllAsync()).Single();

        Assert.AreEqual(Now.AddMinutes(-3), stored.Logs.Single().LastWriteAt);
        Assert.AreEqual(DateTimeKind.Utc, stored.Logs.Single().LastWriteAt!.Value.Kind);
        Assert.AreEqual(1, stored.StartupTasks.Single().LastResult);
        Assert.AreEqual(Now.AddHours(-6), stored.StartupTasks.Single().LastRunAt);
    }

    private static ITimeProvider TimeProviderAt(DateTime utcNow)
    {
        var mock = new Mock<ITimeProvider>();
        mock.Setup(t => t.UtcNow).Returns(utcNow);
        return mock.Object;
    }

    private static async Task<TelemetryDbContext> CreateSqliteContextAsync()
    {
        var options = new DbContextOptionsBuilder<TelemetryDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        var context = new TelemetryDbContext(options);
        await context.Database.OpenConnectionAsync();
        await context.Database.EnsureCreatedAsync();

        var owner = new User
        {
            FirstName = "Test",
            LastName = "Owner",
            Email = "owner@example.com",
            IsActive = true,
            CreatedAt = Now,
            LastUpdate = Now,
            UserRoles = [],
        };
        context.Users.Add(owner);
        await context.SaveChangesAsync();

        context.Beacons.Add(new Beacon { ID = 1, Name = "Rugby Junction", OwnerID = owner.ID, CreatedAt = Now, LastUpdate = Now });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        return context;
    }
}
