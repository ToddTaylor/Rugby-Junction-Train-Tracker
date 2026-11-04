using Mapster;
using MapsterMapper;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Web.Server.DTOs;
using Web.Server.Entities;
using Web.Server.Enums;
using Web.Server.Mappers;
using Web.Server.Providers;
using Web.Server.Repositories;
using Web.Server.Services;

namespace Web.ServerTests.Services;

[TestClass]
public class ServerHealthServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);

    private const int CustodianId = 40;

    [TestMethod]
    public async Task Admin_SeesEveryBeacon_SortedByName()
    {
        var (service, _, _) = CreateService(
            [CreateBeacon(1, "Sussex", custodianId: null), CreateBeacon(2, "Duplainville", custodianId: CustodianId)],
            []);

        var rows = await service.GetServerHealthAsync(CreateUser(1, "Admin"));

        CollectionAssert.AreEqual(new[] { "Duplainville", "Sussex" }, rows.Select(r => r.BeaconName).ToArray());
    }

    [TestMethod]
    public async Task Custodian_SeesOnlyBeaconsOnTheirSubdivisions()
    {
        var (service, _, _) = CreateService(
            [CreateBeacon(1, "Sussex", custodianId: 99), CreateBeacon(2, "Duplainville", custodianId: CustodianId)],
            []);

        var rows = await service.GetServerHealthAsync(CreateUser(CustodianId, "Custodian"));

        CollectionAssert.AreEqual(new[] { "Duplainville" }, rows.Select(r => r.BeaconName).ToArray());
    }

    [TestMethod]
    public async Task Viewer_SeesNothing()
    {
        var (service, _, _) = CreateService([CreateBeacon(1, "Sussex", custodianId: null)], []);

        var rows = await service.GetServerHealthAsync(CreateUser(5, "Viewer"));

        Assert.AreEqual(0, rows.Count);
    }

    [TestMethod]
    public async Task Row_UsesNewestRailroadCheckIn_AndReportsDiagnostics()
    {
        var beacon = CreateBeacon(1, "Rugby Junction", custodianId: null, lastHealthCheck: Now.AddMinutes(-1));
        beacon.ExpectedSoftEotCount = 2;
        var diagnostics = new BeaconDiagnostics
        {
            BeaconID = 1,
            LastUpdate = Now.AddMinutes(-1),
            HealthPingIntervalMinutes = 2,
            Hostname = "RJ-SDR01",
            Programs = [new DiagnosticsProgramStatus { Name = "SoftEOT", Running = 1 }],
        };
        var (service, _, _) = CreateService([beacon], [diagnostics]);

        var row = (await service.GetServerHealthAsync(CreateUser(1, "Admin"))).Single();

        Assert.AreEqual(ServerStatus.Degraded, row.Status);
        CollectionAssert.AreEqual(new[] { "Only 1 of 2 SoftEOT instances running" }, row.Reasons);
        Assert.AreEqual(Now.AddMinutes(-1), row.LastCheckIn);
        Assert.IsTrue(row.HasDiagnostics);
        Assert.AreEqual("RJ-SDR01", row.Diagnostics!.Hostname);
        Assert.AreEqual(Now.AddMinutes(-1), row.Diagnostics.ReceivedAt);
        Assert.AreEqual(6, row.OfflineThresholdMinutes);
        CollectionAssert.AreEqual(new[] { "CN Waukesha Sub" }, row.Subdivisions);

        var softEot = row.Programs.Single(p => p.Name == "SoftEOT");
        Assert.AreEqual(2, softEot.Expected);
        Assert.AreEqual(1, softEot.Running);
        Assert.AreEqual(0, row.Programs.Single(p => p.Name == "SDR#").Running);
    }

    [TestMethod]
    public async Task Row_WithoutDiagnostics_LeavesRunningCountsEmpty()
    {
        var beacon = CreateBeacon(1, "Pewaukee", custodianId: null, lastHealthCheck: Now.AddMinutes(-3));
        var (service, _, _) = CreateService([beacon], []);

        var row = (await service.GetServerHealthAsync(CreateUser(1, "Admin"))).Single();

        Assert.AreEqual(ServerStatus.Online, row.Status);
        Assert.IsFalse(row.HasDiagnostics);
        Assert.IsNull(row.Diagnostics);
        Assert.IsTrue(row.Programs.All(p => p.Running == null));
    }

    [TestMethod]
    public async Task CanManageBeacon_AdminAlways_CustodianOnlyForOwnSubdivision()
    {
        var (service, _, _) = CreateService(
            [CreateBeacon(1, "Sussex", custodianId: 99), CreateBeacon(2, "Duplainville", custodianId: CustodianId)],
            []);

        Assert.IsTrue(await service.CanManageBeaconAsync(CreateUser(1, "Admin"), 1));
        Assert.IsTrue(await service.CanManageBeaconAsync(CreateUser(CustodianId, "Custodian"), 2));
        Assert.IsFalse(await service.CanManageBeaconAsync(CreateUser(CustodianId, "Custodian"), 1));
        Assert.IsFalse(await service.CanManageBeaconAsync(CreateUser(5, "Viewer"), 2));
    }

    [TestMethod]
    public async Task RecordDiagnostics_StoresReportUnderTheBeacon()
    {
        var (service, _, diagnosticsRepositoryMock) = CreateService([], []);

        await service.RecordDiagnosticsAsync(7, new BeaconDiagnostics { Hostname = "SUSSEX-PC" });

        diagnosticsRepositoryMock.Verify(r => r.UpsertAsync(It.Is<BeaconDiagnostics>(d => d.BeaconID == 7 && d.Hostname == "SUSSEX-PC")), Times.Once);
    }

    [TestMethod]
    public async Task UpdateExpectedPrograms_UnknownBeacon_ReturnsFalse()
    {
        var (service, beaconRepositoryMock, _) = CreateService([], []);
        beaconRepositoryMock
            .Setup(r => r.UpdateExpectedProgramCountsAsync(3, 1, 2, 4))
            .ReturnsAsync((Beacon?)null);

        var updated = await service.UpdateExpectedProgramsAsync(3, new UpdateServerExpectationsDTO
        {
            ExpectedSdrSharpCount = 1,
            ExpectedSoftEotCount = 2,
            ExpectedSoftDpuCount = 4,
        });

        Assert.IsFalse(updated);
    }

    [TestMethod]
    public async Task DetectStatusChanges_FirstSighting_RecordsStatusWithoutAlerting()
    {
        var beacon = CreateBeacon(1, "Sussex", custodianId: null, lastHealthCheck: Now.AddMinutes(-30));
        var states = new InMemoryServerStateRepository();
        var (service, _, _) = CreateService([beacon], [], states, [CreateUser(1, "Admin")], new Clock(Now));

        var changes = await service.DetectStatusChangesAsync();

        Assert.AreEqual(0, changes.Count);
        Assert.AreEqual(ServerStatus.Offline, states.Get(1)!.Status);
    }

    [TestMethod]
    public async Task DetectStatusChanges_Degraded_IsAnnouncedOnlyAfterItLasts()
    {
        var clock = new Clock(Now);
        var (beacon, diagnostics) = CreateReportingBeacon(softEotRunning: 2);
        var states = new InMemoryServerStateRepository();
        var (service, _, _) = CreateService([beacon], [diagnostics], states, [CreateUser(1, "Admin")], clock);
        await service.DetectStatusChangesAsync();

        diagnostics.Programs = [new DiagnosticsProgramStatus { Name = "SoftEOT", Running = 1 }];
        var degradedAt = Now.AddMinutes(1);
        foreach (var minutes in new[] { 1, 2, 3 })
        {
            CheckIn(beacon, diagnostics, clock, Now.AddMinutes(minutes));
            Assert.AreEqual(0, (await service.DetectStatusChangesAsync()).Count, $"minute {minutes}");
        }

        CheckIn(beacon, diagnostics, clock, Now.AddMinutes(4));
        var change = (await service.DetectStatusChangesAsync()).Single();

        Assert.AreEqual(ServerStatus.Online, change.Notification.PreviousStatus);
        Assert.AreEqual(ServerStatus.Degraded, change.Notification.Status);
        Assert.AreEqual(degradedAt, change.Notification.ChangedAt);
        CollectionAssert.AreEqual(new[] { "Only 1 of 2 SoftEOT instances running" }, change.Notification.Reasons);
        Assert.IsFalse(change.Notification.AlertsMuted);
        Assert.IsNull(states.Get(1)!.PendingStatus);
    }

    [TestMethod]
    public async Task DetectStatusChanges_BriefProblem_ClearsWithoutAlerting()
    {
        var clock = new Clock(Now);
        var (beacon, diagnostics) = CreateReportingBeacon(softEotRunning: 2);
        var states = new InMemoryServerStateRepository();
        var (service, _, _) = CreateService([beacon], [diagnostics], states, [CreateUser(1, "Admin")], clock);
        await service.DetectStatusChangesAsync();

        diagnostics.Programs = [new DiagnosticsProgramStatus { Name = "SoftEOT", Running = 1 }];
        CheckIn(beacon, diagnostics, clock, Now.AddMinutes(1));
        await service.DetectStatusChangesAsync();
        Assert.AreEqual(ServerStatus.Degraded, states.Get(1)!.PendingStatus);

        diagnostics.Programs = [new DiagnosticsProgramStatus { Name = "SoftEOT", Running = 2 }];
        CheckIn(beacon, diagnostics, clock, Now.AddMinutes(2));
        Assert.AreEqual(0, (await service.DetectStatusChangesAsync()).Count);
        Assert.IsNull(states.Get(1)!.PendingStatus);

        // The confirm period starts again from scratch the next time the problem appears.
        diagnostics.Programs = [new DiagnosticsProgramStatus { Name = "SoftEOT", Running = 1 }];
        CheckIn(beacon, diagnostics, clock, Now.AddMinutes(4));
        Assert.AreEqual(0, (await service.DetectStatusChangesAsync()).Count);
        Assert.AreEqual(ServerStatus.Online, states.Get(1)!.Status);
    }

    [TestMethod]
    public async Task DetectStatusChanges_OfflineAndRecovery_AreAnnouncedAtOnce()
    {
        var clock = new Clock(Now);
        var (beacon, diagnostics) = CreateReportingBeacon(softEotRunning: 2);
        var states = new InMemoryServerStateRepository();
        var (service, _, _) = CreateService([beacon], [diagnostics], states, [CreateUser(1, "Admin")], clock);
        await service.DetectStatusChangesAsync();

        // Checks in every 2 minutes, so it is offline once 6 minutes pass without a check-in.
        clock.UtcNow = Now.AddMinutes(7);
        var offline = (await service.DetectStatusChangesAsync()).Single();
        Assert.AreEqual(ServerStatus.Offline, offline.Notification.Status);

        CheckIn(beacon, diagnostics, clock, Now.AddMinutes(8));
        var recovered = (await service.DetectStatusChangesAsync()).Single();
        Assert.AreEqual(ServerStatus.Offline, recovered.Notification.PreviousStatus);
        Assert.AreEqual(ServerStatus.Online, recovered.Notification.Status);
        Assert.AreEqual(0, recovered.Notification.Reasons.Count);
    }

    [TestMethod]
    public async Task DetectStatusChanges_GoesToAdminsAndTheBeaconsActiveCustodian()
    {
        var clock = new Clock(Now);
        var sussex = CreateBeacon(1, "Sussex", custodianId: CustodianId);
        var states = new InMemoryServerStateRepository();
        var inactiveAdmin = CreateUser(2, "Admin");
        inactiveAdmin.IsActive = false;
        var users = new[]
        {
            CreateUser(1, "Admin"),
            inactiveAdmin,
            CreateUser(CustodianId, "Custodian"),
            CreateUser(41, "Custodian"),
            CreateUser(5, "Viewer"),
        };
        var (service, _, _) = CreateService([sussex], [], states, users, clock);
        await service.DetectStatusChangesAsync();

        clock.UtcNow = Now.AddMinutes(20);
        var change = (await service.DetectStatusChangesAsync()).Single();

        CollectionAssert.AreEquivalent(new[] { 1, CustodianId }, change.RecipientUserIds.ToArray());
    }

    [TestMethod]
    public async Task DetectStatusChanges_MutedServer_IsFlaggedSoNoPopUpIsShown()
    {
        var clock = new Clock(Now);
        var beacon = CreateBeacon(1, "Sussex", custodianId: null);
        var states = new InMemoryServerStateRepository();
        var (service, _, _) = CreateService([beacon], [], states, [CreateUser(1, "Admin")], clock);
        await service.DetectStatusChangesAsync();
        await service.MuteAlertsAsync(1, new MuteServerAlertsDTO { MutedUntil = Now.AddDays(1) }, CreateUser(1, "Admin"));

        clock.UtcNow = Now.AddMinutes(20);
        var change = (await service.DetectStatusChangesAsync()).Single();

        Assert.IsTrue(change.Notification.AlertsMuted);
    }

    [TestMethod]
    public async Task MuteAlerts_StoresNoteAndName_AndShowsOnTheRow()
    {
        var states = new InMemoryServerStateRepository();
        var (service, _, _) = CreateService([CreateBeacon(1, "Sussex", custodianId: null)], [], states, [], new Clock(Now));
        var admin = CreateUser(1, "Admin");
        admin.FirstName = "Todd";
        admin.LastName = "Taylor";
        await service.DetectStatusChangesAsync();

        var muted = await service.MuteAlertsAsync(1, new MuteServerAlertsDTO { MutedUntil = null, Note = "  Antenna down until spring  " }, admin);
        var row = (await service.GetServerHealthAsync(admin)).Single();

        Assert.IsTrue(muted);
        Assert.IsTrue(row.AlertsMuted);
        Assert.IsNull(row.AlertsMutedUntil);
        Assert.AreEqual("Antenna down until spring", row.AlertsMuteNote);
        Assert.AreEqual("Todd Taylor", row.AlertsMutedBy);
        Assert.AreEqual(Now, row.StatusSince);

        Assert.IsTrue(await service.UnmuteAlertsAsync(1));
        Assert.IsFalse((await service.GetServerHealthAsync(admin)).Single().AlertsMuted);
    }

    [TestMethod]
    public async Task MuteAlerts_ExpiredMute_IsNotShown()
    {
        var clock = new Clock(Now);
        var states = new InMemoryServerStateRepository();
        var (service, _, _) = CreateService([CreateBeacon(1, "Sussex", custodianId: null)], [], states, [], clock);
        await service.MuteAlertsAsync(1, new MuteServerAlertsDTO { MutedUntil = Now.AddHours(1) }, CreateUser(1, "Admin"));

        clock.UtcNow = Now.AddHours(2);
        var row = (await service.GetServerHealthAsync(CreateUser(1, "Admin"))).Single();

        Assert.IsFalse(row.AlertsMuted);
        Assert.IsNull(row.AlertsMuteNote);
    }

    [TestMethod]
    public async Task MuteAndUnmute_UnknownBeacon_ReturnFalse()
    {
        var (service, _, _) = CreateService([], []);

        Assert.IsFalse(await service.MuteAlertsAsync(9, new MuteServerAlertsDTO(), CreateUser(1, "Admin")));
        Assert.IsFalse(await service.UnmuteAlertsAsync(9));
    }

    private static (Beacon Beacon, BeaconDiagnostics Diagnostics) CreateReportingBeacon(int softEotRunning)
    {
        var beacon = CreateBeacon(1, "Rugby Junction", custodianId: null, lastHealthCheck: Now);
        beacon.ExpectedSoftEotCount = 2;
        var diagnostics = new BeaconDiagnostics
        {
            BeaconID = 1,
            LastUpdate = Now,
            HealthPingIntervalMinutes = 2,
            Programs = [new DiagnosticsProgramStatus { Name = "SoftEOT", Running = softEotRunning }],
        };
        return (beacon, diagnostics);
    }

    private static void CheckIn(Beacon beacon, BeaconDiagnostics diagnostics, Clock clock, DateTime at)
    {
        clock.UtcNow = at;
        diagnostics.LastUpdate = at;
        foreach (var beaconRailroad in beacon.BeaconRailroads)
        {
            beaconRailroad.LastUpdate = at;
        }
    }

    private static (ServerHealthService Service, Mock<IBeaconRepository> BeaconRepository, Mock<IBeaconDiagnosticsRepository> DiagnosticsRepository) CreateService(
        IEnumerable<Beacon> beacons,
        IEnumerable<BeaconDiagnostics> diagnostics)
    {
        return CreateService(beacons, diagnostics, new InMemoryServerStateRepository(), [], new Clock(Now));
    }

    private static (ServerHealthService Service, Mock<IBeaconRepository> BeaconRepository, Mock<IBeaconDiagnosticsRepository> DiagnosticsRepository) CreateService(
        IEnumerable<Beacon> beacons,
        IEnumerable<BeaconDiagnostics> diagnostics,
        IBeaconServerStateRepository stateRepository,
        IEnumerable<User> users,
        Clock clock)
    {
        var beaconList = beacons.ToList();
        var beaconRepositoryMock = new Mock<IBeaconRepository>();
        beaconRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(beaconList);
        beaconRepositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<int>()))
            .ReturnsAsync((int id) => beaconList.FirstOrDefault(b => b.ID == id));

        var diagnosticsRepositoryMock = new Mock<IBeaconDiagnosticsRepository>();
        diagnosticsRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(diagnostics.ToList());

        var userRepositoryMock = new Mock<IUserRepository>();
        userRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(users.ToList());

        var timeProviderMock = new Mock<ITimeProvider>();
        timeProviderMock.Setup(t => t.UtcNow).Returns(() => clock.UtcNow);

        var config = new TypeAdapterConfig();
        config.Scan(typeof(MapsterProfile).Assembly);
        var mapper = new ServiceMapper(new ServiceCollection().BuildServiceProvider(), config);

        var service = new ServerHealthService(
            beaconRepositoryMock.Object,
            diagnosticsRepositoryMock.Object,
            stateRepository,
            userRepositoryMock.Object,
            mapper,
            timeProviderMock.Object);

        return (service, beaconRepositoryMock, diagnosticsRepositoryMock);
    }

    private static Beacon CreateBeacon(int id, string name, int? custodianId, DateTime? lastHealthCheck = null) => new()
    {
        ID = id,
        Name = name,
        BeaconRailroads =
        [
            new BeaconRailroad
            {
                BeaconID = id,
                SubdivisionID = 10 + id,
                LastUpdate = lastHealthCheck ?? Now.AddMinutes(-2),
                Subdivision = new Subdivision
                {
                    ID = 10 + id,
                    Name = "Waukesha Sub",
                    CustodianId = custodianId,
                    Railroad = new Railroad { Name = "CN" },
                },
            },
        ],
    };

    private static User CreateUser(int id, string roleName) => new()
    {
        ID = id,
        IsActive = true,
        UserRoles = [new UserRole { Role = new Role { RoleName = roleName }, User = null!, AssignedAt = Now }],
    };

    private sealed class Clock(DateTime utcNow)
    {
        public DateTime UtcNow { get; set; } = utcNow;
    }

    /// <summary>
    /// Stores alert state in memory and hands out copies, as the EF repository does with AsNoTracking.
    /// </summary>
    private sealed class InMemoryServerStateRepository : IBeaconServerStateRepository
    {
        private readonly Dictionary<int, BeaconServerState> _states = [];

        public BeaconServerState? Get(int beaconId) => _states.TryGetValue(beaconId, out var state) ? Copy(state) : null;

        public Task<IEnumerable<BeaconServerState>> GetAllAsync() =>
            Task.FromResult<IEnumerable<BeaconServerState>>(_states.Values.Select(Copy).ToList());

        public Task UpdateStatusAsync(BeaconServerState state)
        {
            var stored = GetOrAdd(state.BeaconID);
            stored.Status = state.Status;
            stored.StatusSince = state.StatusSince;
            stored.PendingStatus = state.PendingStatus;
            stored.PendingSince = state.PendingSince;
            return Task.CompletedTask;
        }

        public Task SetMuteAsync(int beaconId, DateTime? mutedUntil, string? note, string mutedBy)
        {
            var stored = GetOrAdd(beaconId);
            stored.AlertsMutedAt = Now;
            stored.AlertsMutedUntil = mutedUntil;
            stored.AlertsMuteNote = note;
            stored.AlertsMutedBy = mutedBy;
            return Task.CompletedTask;
        }

        public Task ClearMuteAsync(int beaconId)
        {
            if (_states.TryGetValue(beaconId, out var stored))
            {
                stored.AlertsMutedAt = null;
                stored.AlertsMutedUntil = null;
                stored.AlertsMuteNote = null;
                stored.AlertsMutedBy = null;
            }

            return Task.CompletedTask;
        }

        private BeaconServerState GetOrAdd(int beaconId)
        {
            if (!_states.TryGetValue(beaconId, out var stored))
            {
                stored = new BeaconServerState { BeaconID = beaconId, StatusSince = Now };
                _states[beaconId] = stored;
            }

            return stored;
        }

        private static BeaconServerState Copy(BeaconServerState state) => new()
        {
            BeaconID = state.BeaconID,
            Status = state.Status,
            StatusSince = state.StatusSince,
            PendingStatus = state.PendingStatus,
            PendingSince = state.PendingSince,
            AlertsMutedAt = state.AlertsMutedAt,
            AlertsMutedUntil = state.AlertsMutedUntil,
            AlertsMuteNote = state.AlertsMuteNote,
            AlertsMutedBy = state.AlertsMutedBy,
        };
    }
}
