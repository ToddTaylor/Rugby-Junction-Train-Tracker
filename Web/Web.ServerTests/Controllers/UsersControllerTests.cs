using MapsterMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Logging;
using Moq;
using Web.Server.Controllers.v1;
using Web.Server.DTOs;
using Web.Server.Entities;
using Web.Server.Filters;
using Web.Server.Services;

namespace Web.ServerTests.Controllers;

/// <summary>
/// RequireRole has already let an Admin or Custodian in by the time these actions run; the tests cover what a
/// Custodian is still refused.
/// </summary>
[TestClass]
public class UsersControllerTests
{
    private const int TargetId = 20;

    private readonly Mock<IUserService> _userService = new();

    [TestMethod]
    public async Task Create_CustodianAssigningAdmin_IsForbidden()
    {
        var controller = CreateController(actorRole: "Custodian");

        var result = await controller.PostUser(NewUser("Admin"));

        AssertStatus(StatusCodes.Status403Forbidden, result);
        _userService.Verify(s => s.CreateUserAsync(It.IsAny<User>()), Times.Never);
    }

    [TestMethod]
    public async Task Create_CustodianAssigningCustodian_CreatesUser()
    {
        var controller = CreateController(actorRole: "Custodian");

        var result = await controller.PostUser(NewUser("Custodian"));

        AssertStatus(StatusCodes.Status201Created, result);
        _userService.Verify(s => s.CreateUserAsync(It.IsAny<User>()), Times.Once);
    }

    [TestMethod]
    public async Task Create_AdminAssigningAdmin_CreatesUser()
    {
        var controller = CreateController(actorRole: "Admin");

        var result = await controller.PostUser(NewUser("Admin"));

        AssertStatus(StatusCodes.Status201Created, result);
    }

    [TestMethod]
    public async Task Create_WithoutSignedInUser_IsForbidden()
    {
        // Cannot happen behind RequireRole; the action still refuses rather than trust that.
        var controller = CreateController(actorRole: null);

        var result = await controller.PostUser(NewUser());

        AssertStatus(StatusCodes.Status403Forbidden, result);
        _userService.Verify(s => s.CreateUserAsync(It.IsAny<User>()), Times.Never);
    }

    [TestMethod]
    public async Task Update_CustodianChangingAnAdmin_IsForbidden()
    {
        var controller = CreateController(actorRole: "Custodian", targetRole: "Admin");

        var result = await controller.PutUser(TargetId, ExistingUser("Admin"));

        AssertStatus(StatusCodes.Status403Forbidden, result);
        _userService.Verify(s => s.UpdateUserAsync(It.IsAny<int>(), It.IsAny<User>()), Times.Never);
    }

    [TestMethod]
    public async Task Update_CustodianPromotingAUserToAdmin_IsForbidden()
    {
        var controller = CreateController(actorRole: "Custodian", targetRole: "Custodian");

        var result = await controller.PutUser(TargetId, ExistingUser("Custodian", "Admin"));

        AssertStatus(StatusCodes.Status403Forbidden, result);
        _userService.Verify(s => s.UpdateUserAsync(It.IsAny<int>(), It.IsAny<User>()), Times.Never);
    }

    [TestMethod]
    public async Task Update_CustodianChangingACustodian_UpdatesUser()
    {
        var controller = CreateController(actorRole: "Custodian", targetRole: "Custodian");

        var result = await controller.PutUser(TargetId, ExistingUser("Custodian"));

        AssertStatus(StatusCodes.Status200OK, result);
        _userService.Verify(s => s.UpdateUserAsync(TargetId, It.IsAny<User>()), Times.Once);
    }

    [TestMethod]
    public async Task Update_UnknownUser_IsNotFound()
    {
        var controller = CreateController(actorRole: "Admin", targetRole: null);

        var result = await controller.PutUser(TargetId, ExistingUser());

        AssertStatus(StatusCodes.Status404NotFound, result);
    }

    [TestMethod]
    public async Task Delete_CustodianDeletingAnAdmin_IsForbidden()
    {
        var controller = CreateController(actorRole: "Custodian", targetRole: "Admin");

        var result = await controller.DeleteUser(TargetId);

        AssertStatus(StatusCodes.Status403Forbidden, result);
        _userService.Verify(s => s.DeleteUserAsync(It.IsAny<int>()), Times.Never);
    }

    [TestMethod]
    public async Task Delete_AdminDeletingAnAdmin_DeletesUser()
    {
        var controller = CreateController(actorRole: "Admin", targetRole: "Admin");

        var result = await controller.DeleteUser(TargetId);

        AssertStatus(StatusCodes.Status204NoContent, result);
        _userService.Verify(s => s.DeleteUserAsync(TargetId), Times.Once);
    }

    private static void AssertStatus(int expected, object result)
    {
        var statusResult = result as IStatusCodeActionResult;
        Assert.IsNotNull(statusResult);
        Assert.AreEqual(expected, statusResult.StatusCode);
    }

    private static CreateUserDTO NewUser(params string[] roles) => new()
    {
        FirstName = "New",
        LastName = "User",
        Email = "new@example.com",
        IsActive = true,
        Roles = [.. roles],
    };

    private static UpdateUserDTO ExistingUser(params string[] roles) => new()
    {
        ID = TargetId,
        FirstName = "Existing",
        LastName = "User",
        Email = "existing@example.com",
        IsActive = true,
        Roles = [.. roles],
    };

    private static User UserWith(int id, string? roleName) => new()
    {
        ID = id,
        Email = "existing@example.com",
        IsActive = true,
        UserRoles = roleName == null
            ? []
            : [new UserRole { Role = new Role { RoleName = roleName }, User = null!, AssignedAt = DateTime.UtcNow }],
    };

    /// <param name="actorRole">Role of the signed-in user, or null for nobody signed in.</param>
    /// <param name="targetRole">Role of the stored user with <see cref="TargetId"/>, or null when there is none.</param>
    private UsersController CreateController(string? actorRole, string? targetRole = null)
    {
        var mapper = new Mock<IMapper>();
        mapper.Setup(m => m.Map<User>(It.IsAny<object>())).Returns(() => new User { Email = "new@example.com" });
        mapper.Setup(m => m.Map<UserDTO>(It.IsAny<object>())).Returns(() => new UserDTO
        {
            ID = TargetId,
            FirstName = "Any",
            LastName = "User",
            Email = "any@example.com",
            Roles = [],
        });

        if (targetRole != null)
        {
            var target = UserWith(TargetId, targetRole);
            _userService.Setup(s => s.GetUserByIdAsync(TargetId)).ReturnsAsync(target);
            _userService.Setup(s => s.GetUserByEmailAsync("existing@example.com")).ReturnsAsync(target);
        }

        _userService.Setup(s => s.GetRoleByNameAsync(It.IsAny<string>()))
            .ReturnsAsync((string name) => new Role { RoleId = 1, RoleName = name });
        _userService.Setup(s => s.CreateUserAsync(It.IsAny<User>())).ReturnsAsync((User user) => user);
        _userService.Setup(s => s.UpdateUserAsync(TargetId, It.IsAny<User>())).ReturnsAsync((int _, User user) => user);
        _userService.Setup(s => s.DeleteUserAsync(TargetId)).ReturnsAsync(true);

        var httpContext = new DefaultHttpContext();
        if (actorRole != null)
        {
            httpContext.Items[RequireRoleAttribute.CurrentUserItemKey] = UserWith(1, actorRole);
        }

        return new UsersController(new Mock<ILogger<UsersController>>().Object, mapper.Object, _userService.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
        };
    }
}
