using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Web.Server.DTOs;
using Web.Server.Entities;
using Web.Server.Enums;
using Web.Server.Filters;
using Web.Server.Services;

namespace Web.ServerTests.Filters;

/// <summary>
/// The API key ships in the web client, so it proves nothing about who is calling. RequireRole is what keeps
/// admin endpoints to signed-in Admins and Custodians.
/// </summary>
[TestClass]
public class RequireRoleAttributeTests
{
    private const int UserId = 7;

    [TestMethod]
    public async Task NoSignedInUser_IsUnauthorized()
    {
        var context = CreateContext(signedIn: false, roleName: null);

        await new RequireRoleAttribute(RoleNames.Admin).OnAuthorizationAsync(context);

        AssertRefused(StatusCodes.Status401Unauthorized, context);
    }

    [TestMethod]
    [DataRow("Viewer")]
    [DataRow(null)]
    public async Task UserWithoutTheRole_IsForbidden(string? roleName)
    {
        var context = CreateContext(signedIn: true, roleName);

        await new RequireRoleAttribute(RoleNames.Admin, RoleNames.Custodian).OnAuthorizationAsync(context);

        AssertRefused(StatusCodes.Status403Forbidden, context);
    }

    [TestMethod]
    public async Task CustodianOnAdminOnlyAction_IsForbidden()
    {
        var context = CreateContext(signedIn: true, RoleNames.Custodian);

        await new RequireRoleAttribute(RoleNames.Admin).OnAuthorizationAsync(context);

        AssertRefused(StatusCodes.Status403Forbidden, context);
    }

    [TestMethod]
    public async Task SignedInUserNoLongerExists_IsForbidden()
    {
        var context = CreateContext(signedIn: true, roleName: null, userExists: false);

        await new RequireRoleAttribute(RoleNames.Admin).OnAuthorizationAsync(context);

        AssertRefused(StatusCodes.Status403Forbidden, context);
    }

    [TestMethod]
    [DataRow("Admin")]
    [DataRow("Custodian")]
    [DataRow("admin")]
    public async Task UserWithOneOfTheRoles_IsAllowed_AndIsKeptForTheAction(string roleName)
    {
        var context = CreateContext(signedIn: true, roleName);

        await new RequireRoleAttribute(RoleNames.Admin, RoleNames.Custodian).OnAuthorizationAsync(context);

        Assert.IsNull(context.Result);
        Assert.AreEqual(UserId, RequireRoleAttribute.GetCurrentUser(context.HttpContext)?.ID);
    }

    [TestMethod]
    public void WithoutRoles_Throws()
    {
        Assert.ThrowsException<ArgumentException>(() => new RequireRoleAttribute());
    }

    private static void AssertRefused(int expectedStatus, AuthorizationFilterContext context)
    {
        var result = context.Result as ObjectResult;
        Assert.IsNotNull(result);
        Assert.AreEqual(expectedStatus, result.StatusCode);
        Assert.AreEqual(1, (result.Value as MessageEnvelope<object?>)?.Errors.Count);
        Assert.IsNull(RequireRoleAttribute.GetCurrentUser(context.HttpContext));
    }

    private static AuthorizationFilterContext CreateContext(bool signedIn, string? roleName, bool userExists = true)
    {
        var userService = new Mock<IUserService>();
        if (userExists)
        {
            userService.Setup(s => s.GetUserByIdAsync(UserId)).ReturnsAsync(new User
            {
                ID = UserId,
                IsActive = true,
                UserRoles = roleName == null
                    ? []
                    : [new UserRole { Role = new Role { RoleName = roleName }, User = null!, AssignedAt = DateTime.UtcNow }],
            });
        }

        var services = new ServiceCollection();
        services.AddSingleton(userService.Object);

        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        if (signedIn)
        {
            httpContext.Items["UserId"] = UserId;
        }

        return new AuthorizationFilterContext(
            new ActionContext(httpContext, new RouteData(), new ActionDescriptor()),
            new List<IFilterMetadata>());
    }
}
