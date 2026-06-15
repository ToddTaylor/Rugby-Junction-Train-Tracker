using Moq;
using Web.Server.Entities;
using Web.Server.Repositories;
using Web.Server.Services;

namespace Web.ServerTests.Services;

[TestClass]
public class SubdivisionServiceTests
{
    [TestMethod]
    public async Task CreateSubdivisionAsync_WithInactiveCustodian_ThrowsArgumentException()
    {
        var subdivisionRepositoryMock = new Mock<ISubdivisionRepository>();
        var userServiceMock = new Mock<IUserService>();

        userServiceMock
            .Setup(s => s.GetUserByIdAsync(7))
            .ReturnsAsync(new User
            {
                ID = 7,
                IsActive = false,
                UserRoles =
                [
                    new UserRole { Role = new Role { RoleName = "Custodian" }, User = null!, AssignedAt = DateTime.UtcNow }
                ]
            });

        var service = new SubdivisionService(subdivisionRepositoryMock.Object, userServiceMock.Object);

        await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
            service.CreateSubdivisionAsync(new Subdivision { CustodianId = 7 }));
    }

    [TestMethod]
    public async Task CreateSubdivisionAsync_WithNonCustodianRole_ThrowsArgumentException()
    {
        var subdivisionRepositoryMock = new Mock<ISubdivisionRepository>();
        var userServiceMock = new Mock<IUserService>();

        userServiceMock
            .Setup(s => s.GetUserByIdAsync(8))
            .ReturnsAsync(new User
            {
                ID = 8,
                IsActive = true,
                UserRoles =
                [
                    new UserRole { Role = new Role { RoleName = "Viewer" }, User = null!, AssignedAt = DateTime.UtcNow }
                ]
            });

        var service = new SubdivisionService(subdivisionRepositoryMock.Object, userServiceMock.Object);

        await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
            service.CreateSubdivisionAsync(new Subdivision { CustodianId = 8 }));
    }

    [TestMethod]
    public async Task CreateSubdivisionAsync_WithActiveCustodianRole_CreatesSubdivision()
    {
        var subdivisionRepositoryMock = new Mock<ISubdivisionRepository>();
        var userServiceMock = new Mock<IUserService>();

        var input = new Subdivision { ID = 42, CustodianId = 9, Name = "Test Sub" };

        userServiceMock
            .Setup(s => s.GetUserByIdAsync(9))
            .ReturnsAsync(new User
            {
                ID = 9,
                IsActive = true,
                UserRoles =
                [
                    new UserRole { Role = new Role { RoleName = "Custodian" }, User = null!, AssignedAt = DateTime.UtcNow }
                ]
            });

        subdivisionRepositoryMock
            .Setup(r => r.AddAsync(input))
            .ReturnsAsync(input);

        var service = new SubdivisionService(subdivisionRepositoryMock.Object, userServiceMock.Object);

        var created = await service.CreateSubdivisionAsync(input);

        Assert.AreEqual(42, created.ID);
        subdivisionRepositoryMock.Verify(r => r.AddAsync(input), Times.Once);
    }

    [TestMethod]
    public async Task UpdateSubdivisionAsync_WhenCustodianUnchanged_DoesNotRevalidateCustodian()
    {
        var subdivisionRepositoryMock = new Mock<ISubdivisionRepository>();
        var userServiceMock = new Mock<IUserService>();

        var existing = new Subdivision { ID = 15, CustodianId = 101 };
        var update = new Subdivision { ID = 15, CustodianId = 101 };

        subdivisionRepositoryMock
            .Setup(r => r.GetByIdAsync(15))
            .ReturnsAsync(existing);

        subdivisionRepositoryMock
            .Setup(r => r.UpdateAsync(update))
            .ReturnsAsync(update);

        var service = new SubdivisionService(subdivisionRepositoryMock.Object, userServiceMock.Object);

        var result = await service.UpdateSubdivisionAsync(update);

        Assert.AreEqual(15, result.ID);
        userServiceMock.Verify(s => s.GetUserByIdAsync(It.IsAny<int>()), Times.Never);
        subdivisionRepositoryMock.Verify(r => r.UpdateAsync(update), Times.Once);
    }

    [TestMethod]
    public async Task UpdateSubdivisionAsync_WhenCustodianChangedToInvalid_ThrowsArgumentException()
    {
        var subdivisionRepositoryMock = new Mock<ISubdivisionRepository>();
        var userServiceMock = new Mock<IUserService>();

        var existing = new Subdivision { ID = 22, CustodianId = null };
        var update = new Subdivision { ID = 22, CustodianId = 33 };

        subdivisionRepositoryMock
            .Setup(r => r.GetByIdAsync(22))
            .ReturnsAsync(existing);

        userServiceMock
            .Setup(s => s.GetUserByIdAsync(33))
            .ReturnsAsync(new User
            {
                ID = 33,
                IsActive = true,
                UserRoles =
                [
                    new UserRole { Role = new Role { RoleName = "Viewer" }, User = null!, AssignedAt = DateTime.UtcNow }
                ]
            });

        var service = new SubdivisionService(subdivisionRepositoryMock.Object, userServiceMock.Object);

        await Assert.ThrowsExceptionAsync<ArgumentException>(() => service.UpdateSubdivisionAsync(update));
        subdivisionRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Subdivision>()), Times.Never);
    }
}
