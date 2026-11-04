using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Web.Server.Entities;
using Web.Server.Filters;
using Web.Server.Middleware;
using Web.Server.Services;

namespace Web.ServerTests.Middleware;

/// <summary>
/// Reboot and device key actions accept the sign-in token only from a header, so a link, an image tag or a crawler
/// cannot trigger them with a token in the query string.
/// </summary>
[TestClass]
public class HeaderAuthTokenTests
{
    private const string Token = "valid-token";

    [TestMethod]
    [DataRow("Authorization", "Bearer " + Token)]
    [DataRow("X-Auth-Token", Token)]
    public async Task Middleware_TokenInHeader_IsNotMarkedAsFromQuery(string header, string value)
    {
        var context = CreateHttpContext();
        context.Request.Headers[header] = value;

        await RunMiddlewareAsync(context);

        Assert.AreEqual(7, context.Items["UserId"]);
        Assert.AreEqual(false, context.Items[AuthTokenMiddleware.TokenFromQueryItemKey]);
    }

    [TestMethod]
    public async Task Middleware_TokenInQueryString_IsMarkedAsFromQuery()
    {
        var context = CreateHttpContext();
        context.Request.QueryString = new QueryString($"?access_token={Token}");

        await RunMiddlewareAsync(context);

        Assert.AreEqual(7, context.Items["UserId"]);
        Assert.AreEqual(true, context.Items[AuthTokenMiddleware.TokenFromQueryItemKey]);
    }

    [TestMethod]
    public void Filter_RefusesQueryStringToken()
    {
        var context = CreateActionContext(fromQuery: true);

        new RequireHeaderAuthTokenAttribute().OnActionExecuting(context);

        Assert.AreEqual(StatusCodes.Status401Unauthorized, (context.Result as ObjectResult)?.StatusCode);
    }

    [TestMethod]
    public void Filter_AllowsHeaderToken()
    {
        var context = CreateActionContext(fromQuery: false);

        new RequireHeaderAuthTokenAttribute().OnActionExecuting(context);

        Assert.IsNull(context.Result);
    }

    private static async Task RunMiddlewareAsync(HttpContext context)
    {
        var authService = new Mock<IAuthService>();
        authService.Setup(s => s.ValidateAndRefreshTokenAsync(Token)).ReturnsAsync((true, 7));

        var middleware = new AuthTokenMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(context, authService.Object);
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        var userService = new Mock<IUserService>();
        userService.Setup(s => s.GetUserByIdAsync(7)).ReturnsAsync(new User { ID = 7, IsActive = true, UserRoles = [] });

        var services = new ServiceCollection();
        services.AddSingleton(userService.Object);

        return new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
    }

    private static ActionExecutingContext CreateActionContext(bool fromQuery)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Items["UserId"] = 7;
        httpContext.Items[AuthTokenMiddleware.TokenFromQueryItemKey] = fromQuery;

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new ActionExecutingContext(actionContext, [], new Dictionary<string, object?>(), controller: new object());
    }
}
