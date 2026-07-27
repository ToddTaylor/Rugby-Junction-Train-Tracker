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
using Web.Server.Providers;
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
    public async Task GetAll_ComputesOnline_FromLastUpdate()
    {
        var offlineEntity = SeedBeaconRailroad(custodianId: null, lastUpdate: Now.AddMinutes(-20));
        var onlineEntity = SeedBeaconRailroad(custodianId: null, lastUpdate: Now.AddMinutes(-5));
        onlineEntity.BeaconID = 2;

        var (controller, serviceMock, _, mapperMock, _) = CreateController(userId: 70, roleName: "Admin");

        serviceMock.Setup(s => s.GetAllAsync()).ReturnsAsync([offlineEntity, onlineEntity]);
        mapperMock
            .Setup(m => m.Map<IEnumerable<BeaconRailroadDTO>>(It.IsAny<IEnumerable<BeaconRailroad>>()))
            .Returns((IEnumerable<BeaconRailroad> entities) => entities.Select(e => new BeaconRailroadDTO
            {
                BeaconID = e.BeaconID,
                SubdivisionID = e.SubdivisionID,
                LastUpdate = e.LastUpdate
            }));

        var result = await controller.GetAll();

        var okResult = result as OkObjectResult;
        Assert.IsNotNull(okResult);
        var envelope = okResult.Value as MessageEnvelope<IEnumerable<BeaconRailroadDTO>>;
        Assert.IsNotNull(envelope);
        var dtos = envelope.Data!.ToList();

        Assert.IsFalse(dtos.First(d => d.BeaconID == 1).Online, "20-minute-old LastUpdate should be offline.");
        Assert.IsTrue(dtos.First(d => d.BeaconID == 2).Online, "5-minute-old LastUpdate should be online.");
    }

    [TestMethod]
    public async Task GetById_ComputesOnline_FromLastUpdate()
    {
        var offlineEntity = SeedBeaconRailroad(custodianId: null, lastUpdate: Now.AddMinutes(-20));

        var (controller, serviceMock, _, mapperMock, _) = CreateController(userId: 70, roleName: "Admin", existing: offlineEntity);

        mapperMock
            .Setup(m => m.Map<BeaconRailroadDTO>(offlineEntity))
            .Returns(new BeaconRailroadDTO
            {
                BeaconID = offlineEntity.BeaconID,
                SubdivisionID = offlineEntity.SubdivisionID,
                LastUpdate = offlineEntity.LastUpdate
            });

        var result = await controller.GetById(offlineEntity.BeaconID, offlineEntity.SubdivisionID);

        var okResult = result.Result as OkObjectResult;
        Assert.IsNotNull(okResult);
        var envelope = okResult.Value as MessageEnvelope<BeaconRailroadDTO>;
        Assert.IsNotNull(envelope);
        Assert.IsFalse(envelope.Data!.Online, "20-minute-old LastUpdate should be offline.");
    }

    [TestMethod]
    public async Task Update_Admin_CanSetOfflineNote_WhenOffline()
    {
        var existing = SeedBeaconRailroad(custodianId: null, lastUpdate: Now.AddMinutes(-20)); // offline
        var dto = ToUpdateDto(existing);
        dto.OfflineNote = "Storm damage to relay box";

        var mapped = new BeaconRailroad
        {
            BeaconID = existing.BeaconID,
            SubdivisionID = existing.SubdivisionID,
            Direction = dto.Direction,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            Milepost = dto.Milepost,
            MultipleTracks = dto.MultipleTracks,
            TelemetryStaleHoursOverride = dto.TelemetryStaleHoursOverride,
            OfflineNote = dto.OfflineNote
        };

        var (controller, serviceMock, _, mapperMock, _) = CreateController(userId: 70, roleName: "Admin", existing: existing);

        mapperMock.Setup(m => m.Map<BeaconRailroad>(dto)).Returns(mapped);

        var result = await controller.Update(existing.BeaconID, existing.SubdivisionID, dto);

        Assert.IsInstanceOfType(result, typeof(NoContentResult));
        serviceMock.Verify(s => s.UpdateAsync(It.Is<BeaconRailroad>(br => br.OfflineNote == "Storm damage to relay box")), Times.Once);
    }

    [TestMethod]
    public async Task Update_SettingOfflineNote_WhileOnline_IsRejected()
    {
        var existing = SeedBeaconRailroad(custodianId: null, lastUpdate: Now); // online
        var dto = ToUpdateDto(existing);
        dto.OfflineNote = "Should not be allowed";

        var (controller, serviceMock, _, _, _) = CreateController(userId: 70, roleName: "Admin", existing: existing);

        var result = await controller.Update(existing.BeaconID, existing.SubdivisionID, dto);

        var badRequest = result as BadRequestObjectResult;
        Assert.IsNotNull(badRequest);
        var envelope = badRequest.Value as MessageEnvelope<BeaconRailroadDTO>;
        Assert.IsNotNull(envelope);
        Assert.IsTrue(envelope.Errors.Any(e => e.Contains("currently online", StringComparison.OrdinalIgnoreCase)));
        serviceMock.Verify(s => s.UpdateAsync(It.IsAny<BeaconRailroad>()), Times.Never);
    }

    [TestMethod]
    public async Task Update_SettingOfflineNote_WhenTelemetryRecentlyReceived_IsRejected_EvenIfHealthCheckOffline()
    {
        // Per the offline-note feature's own definition, "offline" means neither health check
        // NOR telemetry is being received. A beacon that's health-offline but has recent
        // telemetry is not truly offline, so setting a note on it must be rejected the same as
        // for a fully online beacon.
        var existing = SeedBeaconRailroad(custodianId: null, lastUpdate: Now.AddMinutes(-20)); // health-offline
        var dto = ToUpdateDto(existing);
        dto.OfflineNote = "Should not be allowed";

        var (controller, serviceMock, _, _, _) = CreateController(userId: 70, roleName: "Admin", existing: existing);
        serviceMock
            .Setup(s => s.GetLatestTelemetryTimestampAsync(existing.BeaconID, existing.SubdivisionID))
            .ReturnsAsync(Now.AddMinutes(-2)); // telemetry just received

        var result = await controller.Update(existing.BeaconID, existing.SubdivisionID, dto);

        var badRequest = result as BadRequestObjectResult;
        Assert.IsNotNull(badRequest);
        var envelope = badRequest.Value as MessageEnvelope<BeaconRailroadDTO>;
        Assert.IsNotNull(envelope);
        Assert.IsTrue(envelope.Errors.Any(e => e.Contains("currently online", StringComparison.OrdinalIgnoreCase)));
        serviceMock.Verify(s => s.UpdateAsync(It.IsAny<BeaconRailroad>()), Times.Never);
    }

    [TestMethod]
    public async Task Update_AssignedCustodian_CanUpdateOnlyOfflineNote_WhenOffline()
    {
        var existing = SeedBeaconRailroad(custodianId: 50, lastUpdate: Now.AddMinutes(-30)); // offline
        var dto = ToUpdateDto(existing);
        dto.OfflineNote = "Track washed out";

        var (controller, serviceMock, _, mapperMock, _) = CreateController(userId: 50, roleName: "Custodian", existing: existing);

        var result = await controller.Update(existing.BeaconID, existing.SubdivisionID, dto);

        Assert.IsInstanceOfType(result, typeof(NoContentResult));
        serviceMock.Verify(s => s.UpdateAsync(It.Is<BeaconRailroad>(br =>
            br.OfflineNote == "Track washed out" &&
            br.Latitude == existing.Latitude &&
            br.Longitude == existing.Longitude &&
            br.Milepost == existing.Milepost &&
            br.MultipleTracks == existing.MultipleTracks &&
            br.Direction == existing.Direction)), Times.Once);
        mapperMock.Verify(m => m.Map<BeaconRailroad>(It.IsAny<UpdateBeaconRailroadDTO>()), Times.Never);
    }

    [TestMethod]
    public async Task Update_AssignedCustodian_CannotChangeOtherFields()
    {
        var existing = SeedBeaconRailroad(custodianId: 51, lastUpdate: Now.AddMinutes(-30)); // offline
        var dto = ToUpdateDto(existing);
        dto.Milepost = existing.Milepost + 5;

        var (controller, serviceMock, _, _, _) = CreateController(userId: 51, roleName: "Custodian", existing: existing);

        var result = await controller.Update(existing.BeaconID, existing.SubdivisionID, dto);

        var objectResult = result as ObjectResult;
        Assert.IsNotNull(objectResult);
        Assert.AreEqual(StatusCodes.Status403Forbidden, objectResult.StatusCode);

        var envelope = objectResult.Value as MessageEnvelope<BeaconRailroadDTO>;
        Assert.IsNotNull(envelope);
        Assert.IsTrue(envelope.Errors.Any(e => e.Contains("OfflineNote", StringComparison.OrdinalIgnoreCase)));
        serviceMock.Verify(s => s.UpdateAsync(It.IsAny<BeaconRailroad>()), Times.Never);
    }

    [TestMethod]
    public async Task Update_UnassignedCustodian_IsForbidden()
    {
        var existing = SeedBeaconRailroad(custodianId: 999, lastUpdate: Now.AddMinutes(-30)); // offline
        var dto = ToUpdateDto(existing);
        dto.OfflineNote = "Not my subdivision";

        var (controller, serviceMock, _, _, _) = CreateController(userId: 52, roleName: "Custodian", existing: existing);

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
            TelemetryStaleHoursOverride = existing.TelemetryStaleHoursOverride,
            OfflineNote = existing.OfflineNote
        };
    }

    private static (BeaconRailroadsController Controller, Mock<IBeaconRailroadService> ServiceMock, Mock<IUserService> UserServiceMock, Mock<IMapper> MapperMock, Mock<ITimeProvider> TimeProviderMock) CreateController(
        int userId, string roleName, BeaconRailroad? existing = null)
    {
        var serviceMock = new Mock<IBeaconRailroadService>();
        var userServiceMock = new Mock<IUserService>();
        var timeProviderMock = new Mock<ITimeProvider>();
        var loggerMock = new Mock<ILogger<BeaconRailroadsController>>();
        var mapperMock = new Mock<IMapper>();

        timeProviderMock.Setup(tp => tp.UtcNow).Returns(Now);

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
            userServiceMock.Object,
            timeProviderMock.Object,
            loggerMock.Object,
            mapperMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Items["UserId"] = userId;

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        return (controller, serviceMock, userServiceMock, mapperMock, timeProviderMock);
    }
}
