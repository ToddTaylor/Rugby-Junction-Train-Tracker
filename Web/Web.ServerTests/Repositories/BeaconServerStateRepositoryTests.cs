using Microsoft.EntityFrameworkCore;
using Moq;
using Web.Server.Data;
using Web.Server.Entities;
using Web.Server.Enums;
using Web.Server.Providers;
using Web.Server.Repositories;

namespace Web.ServerTests.Repositories;

[TestClass]
public class BeaconServerStateRepositoryTests
{
    private static readonly DateTime Now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    [TestMethod]
    public async Task UpdateStatus_AddsRow_ThenUpdatesStatusWithoutTouchingMute()
    {
        await using var context = await CreateSqliteContextAsync();
        var repository = new BeaconServerStateRepository(context, TimeProviderAt(Now));

        await repository.UpdateStatusAsync(new BeaconServerState { BeaconID = 1, Status = ServerStatus.Online, StatusSince = Now });
        await repository.SetMuteAsync(1, Now.AddDays(1), "Waiting on new SDR", "Todd Taylor");
        await repository.UpdateStatusAsync(new BeaconServerState
        {
            BeaconID = 1,
            Status = ServerStatus.Online,
            StatusSince = Now,
            PendingStatus = ServerStatus.Degraded,
            PendingSince = Now.AddMinutes(1),
        });

        context.ChangeTracker.Clear();
        var stored = (await repository.GetAllAsync()).Single();

        Assert.AreEqual(ServerStatus.Online, stored.Status);
        Assert.AreEqual(ServerStatus.Degraded, stored.PendingStatus);
        Assert.AreEqual(DateTimeKind.Utc, stored.PendingSince!.Value.Kind);
        Assert.AreEqual(Now.AddDays(1), stored.AlertsMutedUntil);
        Assert.AreEqual("Waiting on new SDR", stored.AlertsMuteNote);
        Assert.AreEqual("Todd Taylor", stored.AlertsMutedBy);
        Assert.IsTrue(stored.IsMuted(Now));
    }

    [TestMethod]
    public async Task SetMute_WithoutEndTime_MutesUntilCleared()
    {
        await using var context = await CreateSqliteContextAsync();
        var repository = new BeaconServerStateRepository(context, TimeProviderAt(Now));

        await repository.SetMuteAsync(1, null, null, "Todd Taylor");
        context.ChangeTracker.Clear();
        Assert.IsTrue((await repository.GetAllAsync()).Single().IsMuted(Now.AddYears(5)));

        await repository.ClearMuteAsync(1);
        context.ChangeTracker.Clear();
        var stored = (await repository.GetAllAsync()).Single();

        Assert.IsFalse(stored.IsMuted(Now));
        Assert.IsNull(stored.AlertsMutedBy);
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
