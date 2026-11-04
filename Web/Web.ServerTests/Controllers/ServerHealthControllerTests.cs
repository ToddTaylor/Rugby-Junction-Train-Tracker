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

    private static void AssertStatus(int expected, object result)
    {
        var statusResult = result as IStatusCodeActionResult;
        Assert.IsNotNull(statusResult);
        Assert.AreEqual(expected, statusResult.StatusCode);
    }

    private static (ServerHealthController Controller, Mock<IServerHealthService> Service) CreateController(int? userId, string? roleName)
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
