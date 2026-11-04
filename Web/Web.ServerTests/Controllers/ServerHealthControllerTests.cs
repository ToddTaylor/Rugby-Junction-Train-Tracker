using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Logging;
using Moq;
using Web.Server.Controllers.v1;
using Web.Server.DTOs;
using Web.Server.Entities;
using Web.Server.Enums;
using Web.Server.Filters;
using Web.Server.Services;

namespace Web.ServerTests.Controllers;

[TestClass]
public class ServerHealthControllerTests
{
    private static readonly DateTime Now = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);

    [TestMethod]
    public async Task Get_WithoutSignedInUser_IsUnauthorized()
    {
        var (controller, serviceMock) = CreateController(userId: null, roleName: null);

        var result = await controller.GetServerHealth();

        AssertStatus(StatusCodes.Status401Unauthorized, result);
        serviceMock.Verify(s => s.GetServerHealthAsync(It.IsAny<User>()), Times.Never);
    }

    [TestMethod]
    public async Task Get_Viewer_IsForbidden()
    {
        var (controller, serviceMock) = CreateController(userId: 5, roleName: "Viewer");

        var result = await controller.GetServerHealth();

        AssertStatus(StatusCodes.Status403Forbidden, result);
        serviceMock.Verify(s => s.GetServerHealthAsync(It.IsAny<User>()), Times.Never);
    }

    [TestMethod]
    public async Task Get_Custodian_ReturnsRows()
    {
        var (controller, serviceMock) = CreateController(userId: 40, roleName: "Custodian");
        serviceMock
            .Setup(s => s.GetServerHealthAsync(It.Is<User>(u => u.ID == 40)))
            .ReturnsAsync([new ServerHealthDTO { BeaconID = 2, BeaconName = "Duplainville" }]);

        var result = await controller.GetServerHealth();

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);
        var envelope = ok.Value as MessageEnvelope<IReadOnlyList<ServerHealthDTO>>;
        Assert.AreEqual("Duplainville", envelope!.Data.Single().BeaconName);
    }

    [TestMethod]
    public async Task UpdateExpectations_CustodianOfAnotherSubdivision_IsForbidden()
    {
        var (controller, serviceMock) = CreateController(userId: 40, roleName: "Custodian");
        serviceMock.Setup(s => s.CanManageBeaconAsync(It.IsAny<User>(), 1)).ReturnsAsync(false);

        var result = await controller.UpdateExpectations(1, new UpdateServerExpectationsDTO { ExpectedSoftEotCount = 2 });

        AssertStatus(StatusCodes.Status403Forbidden, result);
        serviceMock.Verify(s => s.UpdateExpectedProgramsAsync(It.IsAny<int>(), It.IsAny<UpdateServerExpectationsDTO>()), Times.Never);
    }

    [TestMethod]
    public async Task UpdateExpectations_OutOfRangeCount_IsBadRequest()
    {
        var (controller, serviceMock) = CreateController(userId: 1, roleName: "Admin");
        serviceMock.Setup(s => s.CanManageBeaconAsync(It.IsAny<User>(), 1)).ReturnsAsync(true);

        var result = await controller.UpdateExpectations(1, new UpdateServerExpectationsDTO { ExpectedSoftDpuCount = -1 });

        AssertStatus(StatusCodes.Status400BadRequest, result);
        serviceMock.Verify(s => s.UpdateExpectedProgramsAsync(It.IsAny<int>(), It.IsAny<UpdateServerExpectationsDTO>()), Times.Never);
    }

    [TestMethod]
    public async Task UpdateExpectations_Allowed_SavesCounts()
    {
        var (controller, serviceMock) = CreateController(userId: 1, roleName: "Admin");
        var dto = new UpdateServerExpectationsDTO { ExpectedSdrSharpCount = 2, ExpectedSoftEotCount = 2, ExpectedSoftDpuCount = 4 };
        serviceMock.Setup(s => s.CanManageBeaconAsync(It.IsAny<User>(), 1)).ReturnsAsync(true);
        serviceMock.Setup(s => s.UpdateExpectedProgramsAsync(1, dto)).ReturnsAsync(true);

        var result = await controller.UpdateExpectations(1, dto);

        AssertStatus(StatusCodes.Status204NoContent, result);
    }

    [TestMethod]
    public async Task Mute_WithoutSignedInUser_IsUnauthorized()
    {
        var (controller, serviceMock) = CreateController(userId: null, roleName: null);

        var result = await controller.MuteAlerts(1, new MuteServerAlertsDTO());

        AssertStatus(StatusCodes.Status401Unauthorized, result);
        serviceMock.Verify(s => s.MuteAlertsAsync(It.IsAny<int>(), It.IsAny<MuteServerAlertsDTO>(), It.IsAny<User>()), Times.Never);
    }

    [TestMethod]
    public async Task Mute_CustodianOfAnotherSubdivision_IsForbidden()
    {
        var (controller, serviceMock) = CreateController(userId: 40, roleName: "Custodian");
        serviceMock.Setup(s => s.CanManageBeaconAsync(It.IsAny<User>(), 1)).ReturnsAsync(false);

        var muteResult = await controller.MuteAlerts(1, new MuteServerAlertsDTO());
        var unmuteResult = await controller.UnmuteAlerts(1);

        AssertStatus(StatusCodes.Status403Forbidden, muteResult);
        AssertStatus(StatusCodes.Status403Forbidden, unmuteResult);
        serviceMock.Verify(s => s.MuteAlertsAsync(It.IsAny<int>(), It.IsAny<MuteServerAlertsDTO>(), It.IsAny<User>()), Times.Never);
        serviceMock.Verify(s => s.UnmuteAlertsAsync(It.IsAny<int>()), Times.Never);
    }

    [TestMethod]
    public async Task Mute_EndTimeInThePast_IsBadRequest()
    {
        var (controller, serviceMock) = CreateController(userId: 1, roleName: "Admin");
        serviceMock.Setup(s => s.CanManageBeaconAsync(It.IsAny<User>(), 1)).ReturnsAsync(true);

        var result = await controller.MuteAlerts(1, new MuteServerAlertsDTO { MutedUntil = DateTime.UtcNow.AddMinutes(-1) });

        AssertStatus(StatusCodes.Status400BadRequest, result);
    }

    [TestMethod]
    public async Task Mute_NoteTooLong_IsBadRequest()
    {
        var (controller, serviceMock) = CreateController(userId: 1, roleName: "Admin");
        serviceMock.Setup(s => s.CanManageBeaconAsync(It.IsAny<User>(), 1)).ReturnsAsync(true);

        var result = await controller.MuteAlerts(1, new MuteServerAlertsDTO { Note = new string('x', ServerHealthController.MaxMuteNoteLength + 1) });

        AssertStatus(StatusCodes.Status400BadRequest, result);
    }

    [TestMethod]
    public async Task Mute_Allowed_MutesAsTheSignedInUser()
    {
        var (controller, serviceMock) = CreateController(userId: 40, roleName: "Custodian");
        var dto = new MuteServerAlertsDTO { MutedUntil = DateTime.UtcNow.AddDays(1), Note = "Waiting on parts" };
        serviceMock.Setup(s => s.CanManageBeaconAsync(It.IsAny<User>(), 2)).ReturnsAsync(true);
        serviceMock.Setup(s => s.MuteAlertsAsync(2, dto, It.Is<User>(u => u.ID == 40))).ReturnsAsync(true);

        var result = await controller.MuteAlerts(2, dto);

        AssertStatus(StatusCodes.Status204NoContent, result);
    }

    [TestMethod]
    public async Task Unmute_UnknownBeacon_IsNotFound()
    {
        var (controller, serviceMock) = CreateController(userId: 1, roleName: "Admin");
        serviceMock.Setup(s => s.CanManageBeaconAsync(It.IsAny<User>(), 9)).ReturnsAsync(true);
        serviceMock.Setup(s => s.UnmuteAlertsAsync(9)).ReturnsAsync(false);

        var result = await controller.UnmuteAlerts(9);

        AssertStatus(StatusCodes.Status404NotFound, result);
    }

    [TestMethod]
    public async Task RequestCommand_WithoutSignedInUser_IsUnauthorized()
    {
        var (controller, _) = CreateController(userId: null, roleName: null);

        var result = await controller.RequestCommand(1, new RequestBeaconCommandDTO { Type = BeaconCommandType.Reboot });

        AssertStatus(StatusCodes.Status401Unauthorized, result);
        VerifyNoCommandRequested();
    }

    [TestMethod]
    public async Task RequestCommand_Viewer_IsForbidden()
    {
        var (controller, serviceMock) = CreateController(userId: 5, roleName: "Viewer");
        serviceMock.Setup(s => s.CanManageBeaconAsync(It.IsAny<User>(), 1)).ReturnsAsync(false);

        var result = await controller.RequestCommand(1, new RequestBeaconCommandDTO { Type = BeaconCommandType.Reboot });

        AssertStatus(StatusCodes.Status403Forbidden, result);
        VerifyNoCommandRequested();
    }

    [TestMethod]
    public async Task RequestCommand_CustodianOfAnotherSubdivision_IsForbidden()
    {
        var (controller, serviceMock) = CreateController(userId: 40, roleName: "Custodian");
        serviceMock.Setup(s => s.CanManageBeaconAsync(It.IsAny<User>(), 1)).ReturnsAsync(false);

        var reboot = await controller.RequestCommand(1, new RequestBeaconCommandDTO { Type = BeaconCommandType.Reboot });
        var cancel = await controller.CancelCommand(1, 7);
        var createKey = await controller.CreateDeviceKey(1);
        var removeKey = await controller.RemoveDeviceKey(1);

        foreach (var result in new[] { reboot, cancel, createKey, removeKey })
        {
            AssertStatus(StatusCodes.Status403Forbidden, result);
        }

        VerifyNoCommandRequested();
        _commandService.Verify(s => s.CreateDeviceKeyAsync(It.IsAny<int>(), It.IsAny<User>()), Times.Never);
        _commandService.Verify(s => s.RemoveDeviceKeyAsync(It.IsAny<int>()), Times.Never);
        _commandService.Verify(s => s.CancelCommandAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<User>()), Times.Never);
    }

    [TestMethod]
    public async Task RequestCommand_Allowed_CreatesCommandAsTheSignedInUser()
    {
        var (controller, serviceMock) = CreateController(userId: 40, roleName: "Custodian");
        serviceMock.Setup(s => s.CanManageBeaconAsync(It.IsAny<User>(), 2)).ReturnsAsync(true);
        _commandService
            .Setup(s => s.RequestCommandAsync(2, BeaconCommandType.Reboot, It.Is<User>(u => u.ID == 40)))
            .ReturnsAsync(new CommandRequestResult(CommandRequestError.None, null, new BeaconCommandDTO { ID = 9 }));

        var result = await controller.RequestCommand(2, new RequestBeaconCommandDTO { Type = BeaconCommandType.Reboot });

        AssertStatus(StatusCodes.Status201Created, result);
    }

    [TestMethod]
    [DataRow(CommandRequestError.CommandPending, StatusCodes.Status409Conflict)]
    [DataRow(CommandRequestError.NoDeviceKey, StatusCodes.Status409Conflict)]
    [DataRow(CommandRequestError.RemoteCommandsOff, StatusCodes.Status409Conflict)]
    [DataRow(CommandRequestError.ServerOffline, StatusCodes.Status409Conflict)]
    [DataRow(CommandRequestError.DailyLimitReached, StatusCodes.Status429TooManyRequests)]
    [DataRow(CommandRequestError.BeaconNotFound, StatusCodes.Status404NotFound)]
    public async Task RequestCommand_Refused_ReturnsMatchingStatus(CommandRequestError error, int expectedStatus)
    {
        var (controller, serviceMock) = CreateController(userId: 1, roleName: "Admin");
        serviceMock.Setup(s => s.CanManageBeaconAsync(It.IsAny<User>(), 1)).ReturnsAsync(true);
        _commandService
            .Setup(s => s.RequestCommandAsync(1, BeaconCommandType.Reboot, It.IsAny<User>()))
            .ReturnsAsync(new CommandRequestResult(error, "Refused."));

        var result = await controller.RequestCommand(1, new RequestBeaconCommandDTO { Type = BeaconCommandType.Reboot });

        AssertStatus(expectedStatus, result);
    }

    [TestMethod]
    public async Task RequestCommand_UnknownType_IsBadRequest()
    {
        var (controller, serviceMock) = CreateController(userId: 1, roleName: "Admin");
        serviceMock.Setup(s => s.CanManageBeaconAsync(It.IsAny<User>(), 1)).ReturnsAsync(true);

        var result = await controller.RequestCommand(1, new RequestBeaconCommandDTO { Type = (BeaconCommandType)99 });

        AssertStatus(StatusCodes.Status400BadRequest, result);
        VerifyNoCommandRequested();
    }

    [TestMethod]
    public async Task CreateDeviceKey_Allowed_ReturnsTheKeyOnce()
    {
        var (controller, serviceMock) = CreateController(userId: 1, roleName: "Admin");
        serviceMock.Setup(s => s.CanManageBeaconAsync(It.IsAny<User>(), 1)).ReturnsAsync(true);
        _commandService.Setup(s => s.CreateDeviceKeyAsync(1, It.IsAny<User>())).ReturnsAsync("new-key");

        var result = await controller.CreateDeviceKey(1);

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);
        Assert.AreEqual("new-key", (ok.Value as MessageEnvelope<DeviceKeyCreatedDTO?>)!.Data!.DeviceKey);
    }

    [TestMethod]
    public async Task CancelCommand_AlreadyStarted_IsConflict()
    {
        var (controller, serviceMock) = CreateController(userId: 1, roleName: "Admin");
        serviceMock.Setup(s => s.CanManageBeaconAsync(It.IsAny<User>(), 1)).ReturnsAsync(true);
        _commandService.Setup(s => s.CancelCommandAsync(1, 7, It.IsAny<User>())).ReturnsAsync(CommandChangeResult.Conflict);

        var result = await controller.CancelCommand(1, 7);

        AssertStatus(StatusCodes.Status409Conflict, result);
    }

    [TestMethod]
    public void CommandAndDeviceKeyActions_RefuseTheQueryStringToken()
    {
        var actions = new[]
        {
            nameof(ServerHealthController.RequestCommand),
            nameof(ServerHealthController.CancelCommand),
            nameof(ServerHealthController.CreateDeviceKey),
            nameof(ServerHealthController.RemoveDeviceKey),
        };

        foreach (var action in actions)
        {
            var method = typeof(ServerHealthController).GetMethod(action)!;
            Assert.IsTrue(
                method.GetCustomAttributes(typeof(RequireHeaderAuthTokenAttribute), inherit: true).Length == 1,
                $"{action} must require the sign-in token in a header.");
        }
    }

    private void VerifyNoCommandRequested() =>
        _commandService.Verify(s => s.RequestCommandAsync(It.IsAny<int>(), It.IsAny<BeaconCommandType>(), It.IsAny<User>()), Times.Never);

    private static void AssertStatus(int expected, object result)
    {
        var statusResult = result as IStatusCodeActionResult;
        Assert.IsNotNull(statusResult);
        Assert.AreEqual(expected, statusResult.StatusCode);
    }

    private readonly Mock<IServerCommandService> _commandService = new();

    private (ServerHealthController Controller, Mock<IServerHealthService> Service) CreateController(int? userId, string? roleName)
    {
        var serviceMock = new Mock<IServerHealthService>();
        var userServiceMock = new Mock<IUserService>();

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

        var controller = new ServerHealthController(
            serviceMock.Object,
            _commandService.Object,
            userServiceMock.Object,
            new Mock<ILogger<ServerHealthController>>().Object);

        var httpContext = new DefaultHttpContext();
        if (userId.HasValue)
        {
            httpContext.Items["UserId"] = userId.Value;
        }

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        return (controller, serviceMock);
    }
}
