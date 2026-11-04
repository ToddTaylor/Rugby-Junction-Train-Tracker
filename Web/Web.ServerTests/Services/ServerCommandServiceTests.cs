using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Web.Server.Data;
using Web.Server.DTOs;
using Web.Server.Entities;
using Web.Server.Enums;
using Web.Server.Hubs;
using Web.Server.Providers;
using Web.Server.Repositories;
using Web.Server.Services;

namespace Web.ServerTests.Services;

[TestClass]
public class ServerCommandServiceTests
{
    private static readonly DateTime Start = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);
    private const int BeaconId = 1;
    private const int AdminId = 1;
    private const int CustodianId = 40;

    private TelemetryDbContext _context = null!;
    private MutableClock _clock = null!;
    private Beacon _beacon = null!;
    private Mock<IClientProxy> _clientProxy = null!;
    private List<IReadOnlyList<string>> _sentToGroups = null!;
    private ServerCommandService _service = null!;

    [TestInitialize]
    public async Task InitializeAsync()
    {
        _clock = new MutableClock(Start);
        _context = await CreateSqliteContextAsync();
        _beacon = new Beacon
        {
            ID = BeaconId,
            Name = "Sussex",
            BeaconRailroads = [new BeaconRailroad { BeaconID = BeaconId, LastUpdate = Start, Subdivision = new Subdivision { Name = "Waukesha", CustodianId = CustodianId } }],
        };

        var beaconRepository = new Mock<IBeaconRepository>();
        beaconRepository.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((int id) => id == BeaconId ? _beacon : null);

        var userRepository = new Mock<IUserRepository>();
        userRepository.Setup(r => r.GetAllAsync()).ReturnsAsync([CreateUser(AdminId, "Admin"), CreateUser(CustodianId, "Custodian"), CreateUser(5, "Viewer")]);

        _clientProxy = new Mock<IClientProxy>();
        _sentToGroups = [];
        var hubClients = new Mock<IHubClients>();
        hubClients
            .Setup(c => c.Groups(It.IsAny<IReadOnlyList<string>>()))
            .Callback<IReadOnlyList<string>>(groups => _sentToGroups.Add(groups))
            .Returns(_clientProxy.Object);
        var hubContext = new Mock<IHubContext<NotificationHub>>();
        hubContext.Setup(h => h.Clients).Returns(hubClients.Object);

        _service = new ServerCommandService(
            beaconRepository.Object,
            new BeaconDiagnosticsRepository(_context, _clock),
            new BeaconDeviceKeyRepository(_context, _clock),
            new BeaconCommandRepository(_context, _clock),
            userRepository.Object,
            hubContext.Object,
            _clock,
            new Mock<ILogger<ServerCommandService>>().Object);
    }

    [TestCleanup]
    public async Task CleanupAsync() => await _context.DisposeAsync();

    [TestMethod]
    public async Task DeviceKey_OnlyTheCurrentKeyIsAccepted_AndRefusalsAreRecorded()
    {
        Assert.AreEqual(DeviceKeyCheck.NotConfigured, await _service.CheckDeviceKeyAsync(BeaconId, null));

        var firstKey = await _service.CreateDeviceKeyAsync(BeaconId, CreateUser(AdminId, "Admin"));
        Assert.IsNotNull(firstKey);
        Assert.IsTrue(firstKey.Length >= 40, "The key should carry at least 32 random bytes.");
        Assert.AreEqual(DeviceKeyCheck.Valid, await _service.CheckDeviceKeyAsync(BeaconId, firstKey));
        Assert.AreEqual(DeviceKeyCheck.Invalid, await _service.CheckDeviceKeyAsync(BeaconId, null));
        Assert.AreEqual(DeviceKeyCheck.Invalid, await _service.CheckDeviceKeyAsync(BeaconId, "guess"));

        var stored = await _context.BeaconDeviceKeys.AsNoTracking().SingleAsync();
        Assert.AreNotEqual(firstKey, stored.KeyHash, "Only a hash of the key is stored.");
        Assert.AreEqual(Start, stored.LastRejectedAt);

        var secondKey = await _service.CreateDeviceKeyAsync(BeaconId, CreateUser(AdminId, "Admin"));
        Assert.AreEqual(DeviceKeyCheck.Invalid, await _service.CheckDeviceKeyAsync(BeaconId, firstKey));
        Assert.AreEqual(DeviceKeyCheck.Valid, await _service.CheckDeviceKeyAsync(BeaconId, secondKey));

        Assert.IsTrue(await _service.RemoveDeviceKeyAsync(BeaconId));
        Assert.AreEqual(DeviceKeyCheck.NotConfigured, await _service.CheckDeviceKeyAsync(BeaconId, secondKey));
        Assert.IsNull(await _service.CreateDeviceKeyAsync(99, CreateUser(AdminId, "Admin")));
    }

    [TestMethod]
    public async Task Request_WithoutDeviceKey_IsRefused()
    {
        await ReportDiagnosticsAsync(remoteCommandsEnabled: true);

        var result = await RequestRebootAsync();

        Assert.AreEqual(CommandRequestError.NoDeviceKey, result.Error);
        Assert.AreEqual(0, await _context.BeaconCommands.CountAsync());
    }

    [TestMethod]
    public async Task Request_WhenThePcDoesNotAllowRemoteCommands_IsRefused()
    {
        await _service.CreateDeviceKeyAsync(BeaconId, CreateUser(AdminId, "Admin"));
        await ReportDiagnosticsAsync(remoteCommandsEnabled: false);

        Assert.AreEqual(CommandRequestError.RemoteCommandsOff, (await RequestRebootAsync()).Error);
    }

    [TestMethod]
    public async Task Request_ForAnOfflineServer_IsRefused()
    {
        await SetUpReadyBeaconAsync();
        _clock.UtcNow = Start.AddHours(1);

        Assert.AreEqual(CommandRequestError.ServerOffline, (await RequestRebootAsync()).Error);
    }

    [TestMethod]
    public async Task Request_RestartAppsWithoutTheCommandOnThePc_IsRefused()
    {
        await SetUpReadyBeaconAsync(restartAppsAvailable: false);

        var result = await _service.RequestCommandAsync(BeaconId, BeaconCommandType.RestartApps, CreateUser(AdminId, "Admin"));

        Assert.AreEqual(CommandRequestError.RestartAppsUnavailable, result.Error);
    }

    [TestMethod]
    public async Task Reboot_FullLifecycle_IsDeliveredOnce_StartedOnce_AndFinishesWhenThePcComesBack()
    {
        var deviceKey = await SetUpReadyBeaconAsync();

        var request = await RequestRebootAsync();
        Assert.AreEqual(CommandRequestError.None, request.Error);
        Assert.AreEqual(BeaconCommandStatus.Pending, request.Command!.Status);
        Assert.AreEqual("Ada Admin", request.Command.RequestedBy);
        CollectionAssert.AreEquivalent(new[] { "user-1", "user-40" }, _sentToGroups.Last().ToArray());

        Assert.AreEqual(CommandRequestError.CommandPending, (await RequestRebootAsync()).Error);

        // A PC that has remote commands turned off is never handed the command.
        Assert.IsNull(await _service.HandleCheckInAsync(BeaconId, Diagnostics(remoteCommandsEnabled: false)));

        _clock.UtcNow = Start.AddMinutes(1);
        var delivered = await _service.HandleCheckInAsync(BeaconId, Diagnostics(remoteCommandsEnabled: true));
        Assert.AreEqual(request.Command.ID, delivered!.ID);
        Assert.AreEqual(BeaconCommandType.Reboot, delivered.Type);

        var started = await _service.ReportCommandAsync(BeaconId, delivered.ID, new BeaconCommandReportDTO { Status = BeaconCommandReportStatus.Started });
        Assert.AreEqual(CommandChangeResult.Updated, started);
        Assert.AreEqual(CommandChangeResult.Conflict,
            await _service.ReportCommandAsync(BeaconId, delivered.ID, new BeaconCommandReportDTO { Status = BeaconCommandReportStatus.Started }));
        Assert.IsNull(await _service.HandleCheckInAsync(BeaconId, Diagnostics(remoteCommandsEnabled: true)), "A started command is not handed out again.");

        _clock.UtcNow = Start.AddMinutes(4);
        var afterRestart = Diagnostics(remoteCommandsEnabled: true);
        afterRestart.LastBootAt = Start.AddMinutes(2.5);
        await _service.HandleCheckInAsync(BeaconId, afterRestart);

        var stored = await _context.BeaconCommands.AsNoTracking().SingleAsync();
        Assert.AreEqual(BeaconCommandStatus.Succeeded, stored.GetStatus(_clock.UtcNow));
        Assert.AreEqual(Start.AddMinutes(1), stored.DeliveredAt);
        Assert.AreEqual(Start.AddMinutes(1), stored.StartedAt);
        Assert.AreEqual(AdminId, stored.RequestedByUserId);
        Assert.IsNotNull(deviceKey);
    }

    [TestMethod]
    public async Task Reboot_ThatNeverHappens_IsMarkedFailedOnALaterCheckIn()
    {
        await SetUpReadyBeaconAsync();
        var command = (await RequestRebootAsync()).Command!;
        await _service.HandleCheckInAsync(BeaconId, Diagnostics(remoteCommandsEnabled: true));
        await _service.ReportCommandAsync(BeaconId, command.ID, new BeaconCommandReportDTO { Status = BeaconCommandReportStatus.Started });

        _clock.UtcNow = Start.AddMinutes(3);
        await _service.HandleCheckInAsync(BeaconId, Diagnostics(remoteCommandsEnabled: true));
        Assert.AreEqual(BeaconCommandStatus.Running, await StatusOfAsync(command.ID), "Still within the restart window.");

        _clock.UtcNow = Start.AddMinutes(6);
        await _service.HandleCheckInAsync(BeaconId, Diagnostics(remoteCommandsEnabled: true));
        Assert.AreEqual(BeaconCommandStatus.Failed, await StatusOfAsync(command.ID));
    }

    [TestMethod]
    public async Task Cancelled_Command_IsNotDelivered_AndThePcCannotStartIt()
    {
        await SetUpReadyBeaconAsync();
        var command = (await RequestRebootAsync()).Command!;
        var delivered = await _service.HandleCheckInAsync(BeaconId, Diagnostics(remoteCommandsEnabled: true));
        Assert.IsNotNull(delivered);

        Assert.AreEqual(CommandChangeResult.Updated, await _service.CancelCommandAsync(BeaconId, command.ID, CreateUser(CustodianId, "Custodian")));
        Assert.AreEqual(CommandChangeResult.Conflict, await _service.CancelCommandAsync(BeaconId, command.ID, CreateUser(AdminId, "Admin")));
        Assert.IsNull(await _service.HandleCheckInAsync(BeaconId, Diagnostics(remoteCommandsEnabled: true)));
        Assert.AreEqual(CommandChangeResult.Conflict,
            await _service.ReportCommandAsync(BeaconId, command.ID, new BeaconCommandReportDTO { Status = BeaconCommandReportStatus.Started }));
        Assert.AreEqual(BeaconCommandStatus.Cancelled, await StatusOfAsync(command.ID));
    }

    [TestMethod]
    public async Task Expired_Command_IsNotDelivered_AndThePcCannotStartIt()
    {
        await SetUpReadyBeaconAsync();
        var command = (await RequestRebootAsync()).Command!;

        _clock.UtcNow = Start + ServerCommandService.CommandLifetime;
        var diagnostics = Diagnostics(remoteCommandsEnabled: true);
        await ReportDiagnosticsAsync(remoteCommandsEnabled: true);

        Assert.IsNull(await _service.HandleCheckInAsync(BeaconId, diagnostics));
        Assert.AreEqual(CommandChangeResult.Conflict,
            await _service.ReportCommandAsync(BeaconId, command.ID, new BeaconCommandReportDTO { Status = BeaconCommandReportStatus.Started }));
        Assert.AreEqual(BeaconCommandStatus.Expired, await StatusOfAsync(command.ID));
        Assert.AreEqual(CommandRequestError.None, (await RequestRebootAsync()).Error, "An expired command no longer blocks a new one.");
    }

    [TestMethod]
    public async Task Reboots_AreLimitedPerDay_ButRefusedOnesDoNotCount()
    {
        await SetUpReadyBeaconAsync();

        // One the PC refused, then the daily maximum that it carried out.
        var refused = (await RequestRebootAsync()).Command!;
        Assert.AreEqual(CommandChangeResult.Updated, await _service.ReportCommandAsync(BeaconId, refused.ID,
            new BeaconCommandReportDTO { Status = BeaconCommandReportStatus.Rejected, Message = "The PC started 3 min ago." }));
        Assert.AreEqual(BeaconCommandStatus.Rejected, await StatusOfAsync(refused.ID));

        for (var i = 0; i < ServerCommandService.MaxRebootsPerDay; i++)
        {
            _clock.UtcNow = Start.AddMinutes(20 * (i + 1));
            await ReportDiagnosticsAsync(remoteCommandsEnabled: true);
            var command = (await RequestRebootAsync()).Command!;
            await _service.ReportCommandAsync(BeaconId, command.ID, new BeaconCommandReportDTO { Status = BeaconCommandReportStatus.Started });
            var back = Diagnostics(remoteCommandsEnabled: true);
            back.LastBootAt = _clock.UtcNow.AddMinutes(1);
            _clock.UtcNow = _clock.UtcNow.AddMinutes(3);
            await _service.HandleCheckInAsync(BeaconId, back);
            Assert.AreEqual(BeaconCommandStatus.Succeeded, await StatusOfAsync(command.ID));
        }

        _clock.UtcNow = Start.AddHours(2);
        await ReportDiagnosticsAsync(remoteCommandsEnabled: true);
        var limited = await RequestRebootAsync();
        Assert.AreEqual(CommandRequestError.DailyLimitReached, limited.Error);

        // Restarting the apps has its own, separate limit.
        var restartApps = await _service.RequestCommandAsync(BeaconId, BeaconCommandType.RestartApps, CreateUser(AdminId, "Admin"));
        Assert.AreEqual(CommandRequestError.None, restartApps.Error);
    }

    [TestMethod]
    public async Task RestartApps_ReportsItsResult()
    {
        await SetUpReadyBeaconAsync();
        var command = (await _service.RequestCommandAsync(BeaconId, BeaconCommandType.RestartApps, CreateUser(AdminId, "Admin"))).Command!;

        Assert.AreEqual(CommandChangeResult.Conflict, await _service.ReportCommandAsync(BeaconId, command.ID,
            new BeaconCommandReportDTO { Status = BeaconCommandReportStatus.Succeeded }), "A result needs a start first.");
        await _service.ReportCommandAsync(BeaconId, command.ID, new BeaconCommandReportDTO { Status = BeaconCommandReportStatus.Started });
        await _service.ReportCommandAsync(BeaconId, command.ID, new BeaconCommandReportDTO
        {
            Status = BeaconCommandReportStatus.Failed,
            Message = "RestartAppsCommand exited with code 2." + new string('x', 600),
        });

        var stored = await _context.BeaconCommands.AsNoTracking().SingleAsync();
        Assert.AreEqual(BeaconCommandStatus.Failed, stored.GetStatus(_clock.UtcNow));
        Assert.AreEqual(ServerCommandService.MaxResultMessageLength, stored.ResultMessage!.Length);
    }

    [TestMethod]
    public async Task Report_ForAnotherBeaconsCommand_IsNotFound()
    {
        await SetUpReadyBeaconAsync();
        var command = (await RequestRebootAsync()).Command!;

        var result = await _service.ReportCommandAsync(2, command.ID, new BeaconCommandReportDTO { Status = BeaconCommandReportStatus.Started });

        Assert.AreEqual(CommandChangeResult.NotFound, result);
        Assert.AreEqual(CommandChangeResult.NotFound, await _service.CancelCommandAsync(2, command.ID, CreateUser(AdminId, "Admin")));
    }

    private async Task<string> SetUpReadyBeaconAsync(bool restartAppsAvailable = true)
    {
        var key = await _service.CreateDeviceKeyAsync(BeaconId, CreateUser(AdminId, "Admin"));
        await ReportDiagnosticsAsync(remoteCommandsEnabled: true, restartAppsAvailable);
        return key!;
    }

    private Task<CommandRequestResult> RequestRebootAsync() =>
        _service.RequestCommandAsync(BeaconId, BeaconCommandType.Reboot, CreateUser(AdminId, "Admin"));

    private async Task<BeaconCommandStatus> StatusOfAsync(int commandId)
    {
        var command = await _context.BeaconCommands.AsNoTracking().SingleAsync(c => c.ID == commandId);
        return command.GetStatus(_clock.UtcNow);
    }

    /// <summary>
    /// Stores a fresh report and moves the beacon's last check-in to now, as a health check would.
    /// </summary>
    private async Task ReportDiagnosticsAsync(bool remoteCommandsEnabled, bool restartAppsAvailable = true)
    {
        await new BeaconDiagnosticsRepository(_context, _clock).UpsertAsync(Diagnostics(remoteCommandsEnabled, restartAppsAvailable));
        _context.ChangeTracker.Clear();
        foreach (var beaconRailroad in _beacon.BeaconRailroads)
        {
            beaconRailroad.LastUpdate = _clock.UtcNow;
        }
    }

    private BeaconDiagnostics Diagnostics(bool remoteCommandsEnabled, bool restartAppsAvailable = true) => new()
    {
        BeaconID = BeaconId,
        CollectedAt = _clock.UtcNow,
        HealthPingIntervalMinutes = 2,
        LastBootAt = Start.AddDays(-3),
        RemoteCommandsEnabled = remoteCommandsEnabled,
        RestartAppsAvailable = restartAppsAvailable,
    };

    private static User CreateUser(int id, string roleName) => new()
    {
        ID = id,
        FirstName = id == AdminId ? "Ada" : "Cody",
        LastName = id == AdminId ? "Admin" : "Custodian",
        Email = $"user{id}@example.com",
        IsActive = true,
        UserRoles = [new UserRole { Role = new Role { RoleName = roleName }, User = null!, AssignedAt = Start }],
    };

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
            CreatedAt = Start,
            LastUpdate = Start,
            UserRoles = [],
        };
        context.Users.Add(owner);
        await context.SaveChangesAsync();

        context.Beacons.Add(new Beacon { ID = BeaconId, Name = "Sussex", OwnerID = owner.ID, CreatedAt = Start, LastUpdate = Start });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        return context;
    }

    private sealed class MutableClock(DateTime utcNow) : ITimeProvider
    {
        public DateTime UtcNow { get; set; } = utcNow;
    }
}
