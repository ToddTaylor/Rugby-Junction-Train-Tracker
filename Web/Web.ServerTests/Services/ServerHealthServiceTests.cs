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

    private static (ServerHealthService Service, Mock<IBeaconRepository> BeaconRepository, Mock<IBeaconDiagnosticsRepository> DiagnosticsRepository) CreateService(
        IEnumerable<Beacon> beacons,
        IEnumerable<BeaconDiagnostics> diagnostics)
    {
        var beaconList = beacons.ToList();
        var beaconRepositoryMock = new Mock<IBeaconRepository>();
        beaconRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(beaconList);
        beaconRepositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<int>()))
            .ReturnsAsync((int id) => beaconList.FirstOrDefault(b => b.ID == id));

        var diagnosticsRepositoryMock = new Mock<IBeaconDiagnosticsRepository>();
        diagnosticsRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(diagnostics.ToList());

        var timeProviderMock = new Mock<ITimeProvider>();
        timeProviderMock.Setup(t => t.UtcNow).Returns(Now);

        var config = new TypeAdapterConfig();
        config.Scan(typeof(MapsterProfile).Assembly);
        var mapper = new ServiceMapper(new ServiceCollection().BuildServiceProvider(), config);

        var service = new ServerHealthService(
            beaconRepositoryMock.Object,
            diagnosticsRepositoryMock.Object,
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
}
