using MapsterMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Logging;
using Moq;
using Web.Server.Controllers.v1;
using Web.Server.DTOs;
using Web.Server.Entities;
using Web.Server.Enums;
using Web.Server.Services;

namespace Web.ServerTests.Controllers;

[TestClass]
public class BeaconRailroadsControllerTests
{
    private static readonly DateTime Now = new(2026, 7, 26, 12, 0, 0, DateTimeKind.Utc);

    [TestMethod]
    public async Task Create_NonAdmin_IsForbidden()
    {
        var (controller, serviceMock, _, _, _) = CreateController(userId: 1, roleName: "Custodian");

        var result = await controller.Create(new CreateBeaconRailroadDTO { BeaconID = 1, SubdivisionID = 1 });

        var objectResult = result as ObjectResult;
        Assert.IsNotNull(objectResult);
        Assert.AreEqual(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        serviceMock.Verify(s => s.AddAsync(It.IsAny<BeaconRailroad>()), Times.Never);
    }

    [TestMethod]
    public async Task Delete_NonAdmin_IsForbidden()
    {
        var (controller, serviceMock, _, _, _) = CreateController(userId: 1, roleName: "Custodian");

        var result = await controller.Delete(1, 1);

        var statusResult = result as IStatusCodeActionResult;
        Assert.IsNotNull(statusResult);
        Assert.AreEqual(StatusCodes.Status403Forbidden, statusResult.StatusCode);
        serviceMock.Verify(s => s.DeleteAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [TestMethod]
    public async Task GetAll_SetsOnline_FromServerHealthStatus()
    {
        var degradedEntity = SeedBeaconRailroad(custodianId: null, lastUpdate: Now);
        var onlineEntity = SeedBeaconRailroad(custodianId: null, lastUpdate: Now);
        onlineEntity.BeaconID = 2;

        var (controller, serviceMock, _, mapperMock, serverHealthMock) = CreateController(userId: 70, roleName: "Admin");

        serviceMock.Setup(s => s.GetAllAsync()).ReturnsAsync([degradedEntity, onlineEntity]);
        mapperMock
            .Setup(m => m.Map<IEnumerable<BeaconRailroadDTO>>(It.IsAny<IEnumerable<BeaconRailroad>>()))
            .Returns((IEnumerable<BeaconRailroad> entities) => entities.Select(e => new BeaconRailroadDTO
            {
                BeaconID = e.BeaconID,
                SubdivisionID = e.SubdivisionID,
                LastUpdate = e.LastUpdate
            }));
        serverHealthMock
            .Setup(s => s.GetMapStatusesAsync())
            .ReturnsAsync(new Dictionary<int, BeaconMapStatus>
            {
                [1] = new(ServerStatus.Degraded, ["SoftEOT not running"]),
                [2] = new(ServerStatus.Online, []),
            });

        var result = await controller.GetAll();

        var okResult = result as OkObjectResult;
        Assert.IsNotNull(okResult);
        var envelope = okResult.Value as MessageEnvelope<IEnumerable<BeaconRailroadDTO>>;
        Assert.IsNotNull(envelope);
        var dtos = envelope.Data!.ToList();

        var degraded = dtos.First(d => d.BeaconID == 1);
        Assert.IsFalse(degraded.Online, "A Degraded server is not online, even with a fresh LastUpdate.");
        Assert.AreEqual(ServerStatus.Degraded, degraded.ServerStatus);
        CollectionAssert.AreEqual(new[] { "SoftEOT not running" }, degraded.ServerStatusReasons);

        var online = dtos.First(d => d.BeaconID == 2);
        Assert.IsTrue(online.Online);
        Assert.AreEqual(ServerStatus.Online, online.ServerStatus);
    }

    [TestMethod]
    public async Task GetById_SetsOnline_FromServerHealthStatus()
    {
        var entity = SeedBeaconRailroad(custodianId: null, lastUpdate: Now);

        var (controller, _, _, mapperMock, serverHealthMock) = CreateController(userId: 70, roleName: "Admin", existing: entity);

        mapperMock
            .Setup(m => m.Map<BeaconRailroadDTO>(entity))
            .Returns(new BeaconRailroadDTO
            {
                BeaconID = entity.BeaconID,
                SubdivisionID = entity.SubdivisionID,
                LastUpdate = entity.LastUpdate
            });
        serverHealthMock
            .Setup(s => s.GetMapStatusesAsync())
            .ReturnsAsync(new Dictionary<int, BeaconMapStatus> { [1] = new(ServerStatus.Offline, ["No check-in for 20m"]) });

        var result = await controller.GetById(entity.BeaconID, entity.SubdivisionID);

        var okResult = result.Result as OkObjectResult;
        Assert.IsNotNull(okResult);
        var envelope = okResult.Value as MessageEnvelope<BeaconRailroadDTO>;
        Assert.IsNotNull(envelope);
        Assert.IsFalse(envelope.Data!.Online);
        Assert.AreEqual(ServerStatus.Offline, envelope.Data.ServerStatus);
    }

    [TestMethod]
    public async Task Update_Admin_UpdatesBeaconRailroad()
    {
        var existing = SeedBeaconRailroad(custodianId: null, lastUpdate: Now);
        var dto = ToUpdateDto(existing);
        dto.Milepost = 102.0;

        var mapped = new BeaconRailroad
        {
            Direction = dto.Direction,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            Milepost = dto.Milepost,
            MultipleTracks = dto.MultipleTracks,
            TelemetryStaleHoursOverride = dto.TelemetryStaleHoursOverride
        };

        var (controller, serviceMock, _, mapperMock, _) = CreateController(userId: 70, roleName: "Admin", existing: existing);

        mapperMock.Setup(m => m.Map<BeaconRailroad>(dto)).Returns(mapped);

        var result = await controller.Update(existing.BeaconID, existing.SubdivisionID, dto);

        Assert.IsInstanceOfType(result, typeof(NoContentResult));
        serviceMock.Verify(s => s.UpdateAsync(It.Is<BeaconRailroad>(br =>
            br.BeaconID == existing.BeaconID &&
            br.SubdivisionID == existing.SubdivisionID &&
            br.Milepost == 102.0)), Times.Once);
    }

    [TestMethod]
    public async Task Update_Custodian_IsForbidden()
    {
        // Custodians could only set the offline note, which Server Health status replaced.
        var existing = SeedBeaconRailroad(custodianId: 50, lastUpdate: Now);
        var dto = ToUpdateDto(existing);

        var (controller, serviceMock, _, _, _) = CreateController(userId: 50, roleName: "Custodian", existing: existing);

        var result = await controller.Update(existing.BeaconID, existing.SubdivisionID, dto);

        var objectResult = result as ObjectResult;
        Assert.IsNotNull(objectResult);
        Assert.AreEqual(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        serviceMock.Verify(s => s.UpdateAsync(It.IsAny<BeaconRailroad>()), Times.Never);
    }

    private static BeaconRailroad SeedBeaconRailroad(int? custodianId, DateTime lastUpdate)
    {
        return new BeaconRailroad
        {
            BeaconID = 1,
            SubdivisionID = 1,
            Direction = Direction.NorthSouth,
            Latitude = 43.3,
            Longitude = -88.2,
            Milepost = 101.5,
            MultipleTracks = true,
            TelemetryStaleHoursOverride = null,
            LastUpdate = lastUpdate,
            Subdivision = new Subdivision
            {
                ID = 1,
                Name = "Test",
                RailroadID = 1,
                CustodianId = custodianId
            }
        };
    }

    private static UpdateBeaconRailroadDTO ToUpdateDto(BeaconRailroad existing)
    {
        return new UpdateBeaconRailroadDTO
        {
            BeaconID = existing.BeaconID,
            SubdivisionID = existing.SubdivisionID,
            Direction = existing.Direction,
            Latitude = existing.Latitude,
            Longitude = existing.Longitude,
            Milepost = existing.Milepost,
            MultipleTracks = existing.MultipleTracks,
            TelemetryStaleHoursOverride = existing.TelemetryStaleHoursOverride
        };
    }

    private static (BeaconRailroadsController Controller, Mock<IBeaconRailroadService> ServiceMock, Mock<IUserService> UserServiceMock, Mock<IMapper> MapperMock, Mock<IServerHealthService> ServerHealthMock) CreateController(
        int userId, string roleName, BeaconRailroad? existing = null)
    {
        var serviceMock = new Mock<IBeaconRailroadService>();
        var serverHealthMock = new Mock<IServerHealthService>();
        var userServiceMock = new Mock<IUserService>();
        var loggerMock = new Mock<ILogger<BeaconRailroadsController>>();
        var mapperMock = new Mock<IMapper>();

        serverHealthMock
            .Setup(s => s.GetMapStatusesAsync())
            .ReturnsAsync(new Dictionary<int, BeaconMapStatus>());

        userServiceMock
            .Setup(s => s.GetUserByIdAsync(userId))
            .ReturnsAsync(new User
            {
                ID = userId,
                IsActive = true,
                UserRoles =
                [
                    new UserRole { Role = new Role { RoleName = roleName }, User = null!, AssignedAt = Now }
                ]
            });

        if (existing != null)
        {
            serviceMock
                .Setup(s => s.GetByIdAsync(existing.BeaconID, existing.SubdivisionID))
                .ReturnsAsync(existing);
        }

        serviceMock
            .Setup(s => s.UpdateAsync(It.IsAny<BeaconRailroad>()))
            .ReturnsAsync((BeaconRailroad br) => br);

        var controller = new BeaconRailroadsController(
            serviceMock.Object,
            serverHealthMock.Object,
            userServiceMock.Object,
            loggerMock.Object,
            mapperMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Items["UserId"] = userId;

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        return (controller, serviceMock, userServiceMock, mapperMock, serverHealthMock);
    }
}
