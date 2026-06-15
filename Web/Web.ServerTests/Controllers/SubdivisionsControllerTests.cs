using MapsterMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Web.Server.Controllers.v1;
using Web.Server.DTOs;
using Web.Server.Entities;
using Web.Server.Services;

namespace Web.ServerTests.Controllers;

[TestClass]
public class SubdivisionsControllerTests
{
    [TestMethod]
    public async Task PutSubdivision_AssignedCustodian_CanUpdateOnlyLocalTrainAddressIds()
    {
        var subdivisionServiceMock = new Mock<ISubdivisionService>();
        var userServiceMock = new Mock<IUserService>();
        var loggerMock = new Mock<ILogger<SubdivisionsController>>();
        var mapperMock = new Mock<IMapper>();

        var existing = new Subdivision
        {
            ID = 10,
            Name = "River",
            RailroadID = 3,
            DpuCapable = true,
            CustodianId = 50,
            LocalTrainAddressIDs = "100,101"
        };

        var dto = new UpdateSubdivisionDTO
        {
            ID = 10,
            Name = "River",
            RailroadID = 3,
            DpuCapable = true,
            CustodianId = 50,
            LocalTrainAddressIDs = "100,102"
        };

        Subdivision? persisted = null;

        subdivisionServiceMock
            .Setup(s => s.GetSubdivisionAsync(10))
            .ReturnsAsync(existing);

        subdivisionServiceMock
            .Setup(s => s.UpdateSubdivisionAsync(It.IsAny<Subdivision>()))
            .Callback<Subdivision>(s => persisted = s)
            .ReturnsAsync((Subdivision s) => s);

        userServiceMock
            .Setup(s => s.GetUserByIdAsync(50))
            .ReturnsAsync(new User
            {
                ID = 50,
                IsActive = true,
                UserRoles =
                [
                    new UserRole { Role = new Role { RoleName = "Custodian" }, User = null!, AssignedAt = DateTime.UtcNow }
                ]
            });

        mapperMock
            .Setup(m => m.Map<SubdivisionDTO>(It.IsAny<Subdivision>()))
            .Returns((Subdivision s) => new SubdivisionDTO
            {
                ID = s.ID,
                Name = s.Name,
                RailroadID = s.RailroadID,
                DpuCapable = s.DpuCapable,
                LocalTrainAddressIDs = s.LocalTrainAddressIDs,
                Railroad = "Test RR",
                CreatedAt = DateTime.UtcNow,
                LastUpdate = DateTime.UtcNow,
                CustodianId = s.CustodianId
            });

        var controller = CreateController(subdivisionServiceMock, userServiceMock, loggerMock, mapperMock, 50);

        var actionResult = await controller.PutSubdivision(10, dto);

        var okResult = actionResult as OkObjectResult;
        Assert.IsNotNull(okResult);
        Assert.AreEqual(StatusCodes.Status200OK, okResult.StatusCode);

        Assert.IsNotNull(persisted);
        Assert.AreEqual(existing.Name, persisted.Name);
        Assert.AreEqual(existing.RailroadID, persisted.RailroadID);
        Assert.AreEqual(existing.DpuCapable, persisted.DpuCapable);
        Assert.AreEqual(existing.CustodianId, persisted.CustodianId);
        Assert.AreEqual("100,102", persisted.LocalTrainAddressIDs);

        mapperMock.Verify(m => m.Map<Subdivision>(It.IsAny<UpdateSubdivisionDTO>()), Times.Never);
    }

    [TestMethod]
    public async Task PutSubdivision_AssignedCustodian_CannotChangeReadOnlyFields()
    {
        var subdivisionServiceMock = new Mock<ISubdivisionService>();
        var userServiceMock = new Mock<IUserService>();
        var loggerMock = new Mock<ILogger<SubdivisionsController>>();
        var mapperMock = new Mock<IMapper>();

        var existing = new Subdivision
        {
            ID = 11,
            Name = "North",
            RailroadID = 4,
            DpuCapable = false,
            CustodianId = 60,
            LocalTrainAddressIDs = "200"
        };

        var dto = new UpdateSubdivisionDTO
        {
            ID = 11,
            Name = "North Updated",
            RailroadID = 4,
            DpuCapable = false,
            CustodianId = 60,
            LocalTrainAddressIDs = "201"
        };

        subdivisionServiceMock
            .Setup(s => s.GetSubdivisionAsync(11))
            .ReturnsAsync(existing);

        userServiceMock
            .Setup(s => s.GetUserByIdAsync(60))
            .ReturnsAsync(new User
            {
                ID = 60,
                IsActive = true,
                UserRoles =
                [
                    new UserRole { Role = new Role { RoleName = "Custodian" }, User = null!, AssignedAt = DateTime.UtcNow }
                ]
            });

        var controller = CreateController(subdivisionServiceMock, userServiceMock, loggerMock, mapperMock, 60);

        var actionResult = await controller.PutSubdivision(11, dto);

        var objectResult = actionResult as ObjectResult;
        Assert.IsNotNull(objectResult);
        Assert.AreEqual(StatusCodes.Status403Forbidden, objectResult.StatusCode);

        var envelope = objectResult.Value as MessageEnvelope<SubdivisionDTO>;
        Assert.IsNotNull(envelope);
        Assert.IsTrue(envelope.Errors.Any(e => e.Contains("only update LocalTrainAddressIDs", StringComparison.OrdinalIgnoreCase)));

        subdivisionServiceMock.Verify(s => s.UpdateSubdivisionAsync(It.IsAny<Subdivision>()), Times.Never);
    }

    [TestMethod]
    public async Task PutSubdivision_UnassignedCustodian_IsForbidden()
    {
        var subdivisionServiceMock = new Mock<ISubdivisionService>();
        var userServiceMock = new Mock<IUserService>();
        var loggerMock = new Mock<ILogger<SubdivisionsController>>();
        var mapperMock = new Mock<IMapper>();

        subdivisionServiceMock
            .Setup(s => s.GetSubdivisionAsync(12))
            .ReturnsAsync(new Subdivision
            {
                ID = 12,
                Name = "East",
                RailroadID = 9,
                DpuCapable = true,
                CustodianId = 999,
                LocalTrainAddressIDs = "300"
            });

        userServiceMock
            .Setup(s => s.GetUserByIdAsync(61))
            .ReturnsAsync(new User
            {
                ID = 61,
                IsActive = true,
                UserRoles =
                [
                    new UserRole { Role = new Role { RoleName = "Custodian" }, User = null!, AssignedAt = DateTime.UtcNow }
                ]
            });

        var dto = new UpdateSubdivisionDTO
        {
            ID = 12,
            Name = "East",
            RailroadID = 9,
            DpuCapable = true,
            CustodianId = 999,
            LocalTrainAddressIDs = "301"
        };

        var controller = CreateController(subdivisionServiceMock, userServiceMock, loggerMock, mapperMock, 61);

        var actionResult = await controller.PutSubdivision(12, dto);

        var objectResult = actionResult as ObjectResult;
        Assert.IsNotNull(objectResult);
        Assert.AreEqual(StatusCodes.Status403Forbidden, objectResult.StatusCode);

        subdivisionServiceMock.Verify(s => s.UpdateSubdivisionAsync(It.IsAny<Subdivision>()), Times.Never);
    }

    [TestMethod]
    public async Task PutSubdivision_Admin_CanUpdateAllFields()
    {
        var subdivisionServiceMock = new Mock<ISubdivisionService>();
        var userServiceMock = new Mock<IUserService>();
        var loggerMock = new Mock<ILogger<SubdivisionsController>>();
        var mapperMock = new Mock<IMapper>();

        var dto = new UpdateSubdivisionDTO
        {
            ID = 13,
            Name = "West Updated",
            RailroadID = 14,
            DpuCapable = true,
            CustodianId = 77,
            LocalTrainAddressIDs = "400"
        };

        var mapped = new Subdivision
        {
            ID = 13,
            Name = "West Updated",
            RailroadID = 14,
            DpuCapable = true,
            CustodianId = 77,
            LocalTrainAddressIDs = "400"
        };

        userServiceMock
            .Setup(s => s.GetUserByIdAsync(70))
            .ReturnsAsync(new User
            {
                ID = 70,
                IsActive = true,
                UserRoles =
                [
                    new UserRole { Role = new Role { RoleName = "Admin" }, User = null!, AssignedAt = DateTime.UtcNow }
                ]
            });

        mapperMock
            .Setup(m => m.Map<Subdivision>(It.IsAny<UpdateSubdivisionDTO>()))
            .Returns(mapped);

        subdivisionServiceMock
            .Setup(s => s.UpdateSubdivisionAsync(mapped))
            .ReturnsAsync(mapped);

        mapperMock
            .Setup(m => m.Map<SubdivisionDTO>(mapped))
            .Returns(new SubdivisionDTO
            {
                ID = 13,
                Name = "West Updated",
                RailroadID = 14,
                DpuCapable = true,
                LocalTrainAddressIDs = "400",
                Railroad = "Test RR",
                CreatedAt = DateTime.UtcNow,
                LastUpdate = DateTime.UtcNow,
                CustodianId = 77
            });

        var controller = CreateController(subdivisionServiceMock, userServiceMock, loggerMock, mapperMock, 70);

        var actionResult = await controller.PutSubdivision(13, dto);

        var okResult = actionResult as OkObjectResult;
        Assert.IsNotNull(okResult);
        Assert.AreEqual(StatusCodes.Status200OK, okResult.StatusCode);

        mapperMock.Verify(m => m.Map<Subdivision>(dto), Times.Once);
        subdivisionServiceMock.Verify(s => s.UpdateSubdivisionAsync(mapped), Times.Once);
    }

    private static SubdivisionsController CreateController(
        Mock<ISubdivisionService> subdivisionServiceMock,
        Mock<IUserService> userServiceMock,
        Mock<ILogger<SubdivisionsController>> loggerMock,
        Mock<IMapper> mapperMock,
        int userId)
    {
        var controller = new SubdivisionsController(
            subdivisionServiceMock.Object,
            userServiceMock.Object,
            loggerMock.Object,
            mapperMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Items["UserId"] = userId;

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        return controller;
    }
}
