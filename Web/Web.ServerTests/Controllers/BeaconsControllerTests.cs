using MapsterMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Logging;
using Moq;
using Web.Server.Controllers.v1;
using Web.Server.DTOs;
using Web.Server.Entities;
using Web.Server.Services;

namespace Web.ServerTests.Controllers;

[TestClass]
public class BeaconsControllerTests
{
    private static readonly DateTime Now = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);

    [TestMethod]
    public async Task Create_NonAdmin_IsForbidden()
    {
        var (controller, beaconServiceMock, _, _) = CreateController(userId: 2, roleName: "Custodian");

        var result = await controller.PostBeacon(new CreateBeaconDTO { Name = "Sussex", OwnerID = 1 });

        AssertStatus(StatusCodes.Status403Forbidden, result);
        beaconServiceMock.Verify(s => s.CreateBeaconAsync(It.IsAny<Beacon>()), Times.Never);
    }

    [TestMethod]
    public async Task Update_WithoutSignedInUser_IsForbidden()
    {
        var (controller, beaconServiceMock, _, _) = CreateController(userId: null, roleName: null);

        var result = await controller.PutBeacon(1, new UpdateBeaconDTO { ID = 1, Name = "Sussex", OwnerID = 1 });

        AssertStatus(StatusCodes.Status403Forbidden, result);
        beaconServiceMock.Verify(s => s.UpdateBeaconAsync(It.IsAny<int>(), It.IsAny<Beacon>()), Times.Never);
    }

    [TestMethod]
    public async Task Delete_NonAdmin_IsForbidden()
    {
        var (controller, beaconServiceMock, _, _) = CreateController(userId: 3, roleName: "Viewer");

        var result = await controller.DeleteBeacon(1);

        AssertStatus(StatusCodes.Status403Forbidden, result);
        beaconServiceMock.Verify(s => s.DeleteBeaconAsync(It.IsAny<int>()), Times.Never);
    }

    [TestMethod]
    public async Task Delete_Admin_DeletesBeacon()
    {
        var (controller, beaconServiceMock, _, _) = CreateController(userId: 1, roleName: "Admin");
        beaconServiceMock.Setup(s => s.DeleteBeaconAsync(1)).ReturnsAsync(true);

        var result = await controller.DeleteBeacon(1);

        AssertStatus(StatusCodes.Status204NoContent, result);
    }

    [TestMethod]
    public async Task CheckHealth_WithReport_RecordsCheckInAndDiagnostics()
    {
        var (controller, beaconServiceMock, serverHealthServiceMock, mapperMock) = CreateController(userId: null, roleName: null);
        var beacon = new Beacon { ID = 4, Name = "Slinger" };
        var report = new BeaconDiagnosticsReportDTO { Hostname = "SLINGER-NUC", HealthPingIntervalMinutes = 2 };
        var mapped = new BeaconDiagnostics { Hostname = "SLINGER-NUC" };
        beaconServiceMock.Setup(s => s.GetBeaconByIdAsync(4)).ReturnsAsync(beacon);
        mapperMock.Setup(m => m.Map<BeaconDiagnostics>(report)).Returns(mapped);

        var result = await controller.CheckHealth(4, report);

        AssertStatus(StatusCodes.Status204NoContent, result);
        beaconServiceMock.Verify(s => s.UpdateBeaconHealthAsync(4, beacon), Times.Once);
        serverHealthServiceMock.Verify(s => s.RecordDiagnosticsAsync(4, mapped), Times.Once);
    }

    [TestMethod]
    public async Task CheckHealth_WithoutReport_OnlyRecordsCheckIn()
    {
        var (controller, beaconServiceMock, serverHealthServiceMock, _) = CreateController(userId: null, roleName: null);
        var beacon = new Beacon { ID = 4, Name = "Slinger" };
        beaconServiceMock.Setup(s => s.GetBeaconByIdAsync(4)).ReturnsAsync(beacon);

        var result = await controller.CheckHealth(4);

        AssertStatus(StatusCodes.Status204NoContent, result);
        beaconServiceMock.Verify(s => s.UpdateBeaconHealthAsync(4, beacon), Times.Once);
        serverHealthServiceMock.Verify(s => s.RecordDiagnosticsAsync(It.IsAny<int>(), It.IsAny<BeaconDiagnostics>()), Times.Never);
    }

    [TestMethod]
    public async Task CheckHealth_UnknownBeacon_IsNotFound_AndStoresNothing()
    {
        var (controller, _, serverHealthServiceMock, _) = CreateController(userId: null, roleName: null);

        var result = await controller.CheckHealth(99, new BeaconDiagnosticsReportDTO());

        AssertStatus(StatusCodes.Status404NotFound, result);
        serverHealthServiceMock.Verify(s => s.RecordDiagnosticsAsync(It.IsAny<int>(), It.IsAny<BeaconDiagnostics>()), Times.Never);
    }

    private static void AssertStatus(int expected, object result)
    {
        var statusResult = (result is ActionResult<BeaconDTO> typed ? typed.Result : result) as IStatusCodeActionResult;
        Assert.IsNotNull(statusResult);
        Assert.AreEqual(expected, statusResult.StatusCode);
    }

    private static (BeaconsController Controller, Mock<IBeaconService> BeaconService, Mock<IServerHealthService> ServerHealthService, Mock<IMapper> Mapper) CreateController(
        int? userId,
        string? roleName)
    {
        var beaconServiceMock = new Mock<IBeaconService>();
        var serverHealthServiceMock = new Mock<IServerHealthService>();
        var userServiceMock = new Mock<IUserService>();
        var mapperMock = new Mock<IMapper>();

        if (userId.HasValue)
        {
            userServiceMock
                .Setup(s => s.GetUserByIdAsync(userId.Value))
                .ReturnsAsync(new User
                {
                    ID = userId.Value,
                    IsActive = true,
                    UserRoles = [new UserRole { Role = new Role { RoleName = roleName! }, User = null!, AssignedAt = Now }],
                });
        }

        var controller = new BeaconsController(
            beaconServiceMock.Object,
            serverHealthServiceMock.Object,
            userServiceMock.Object,
            new Mock<ILogger<BeaconsController>>().Object,
            mapperMock.Object);

        var httpContext = new DefaultHttpContext();
        if (userId.HasValue)
        {
            httpContext.Items["UserId"] = userId.Value;
        }

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        return (controller, beaconServiceMock, serverHealthServiceMock, mapperMock);
    }
}
