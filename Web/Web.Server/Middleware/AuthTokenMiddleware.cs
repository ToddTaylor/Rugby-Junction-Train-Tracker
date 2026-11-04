using Web.Server.Services;

namespace Web.Server.Middleware;

public class AuthTokenMiddleware
{
    /// <summary>
    /// HttpContext.Items key set to true when the sign-in token came from the access_token query string rather than
    /// a header. SignalR needs the query string, but actions marked [RequireHeaderAuthToken] refuse it.
    /// </summary>
    public const string TokenFromQueryItemKey = "AuthTokenFromQuery";

    private readonly RequestDelegate _next;

    public AuthTokenMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IAuthService authService)
    {
        // Check for auth token in Authorization header or X-Auth-Token header
        var headerToken = context.Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "")
                ?? context.Request.Headers["X-Auth-Token"].FirstOrDefault();
        var token = headerToken ?? context.Request.Query["access_token"].FirstOrDefault();
        var tokenFromQuery = headerToken == null && token != null;

        if (!string.IsNullOrWhiteSpace(token))
        {
            var (isValid, userId) = await authService.ValidateAndRefreshTokenAsync(token);
            if (isValid && userId.HasValue)
            {
                // Fetch user and check IsActive
                var userService = (IUserService)context.RequestServices.GetService(typeof(IUserService));
                if (userService != null)
                {
                    var user = await userService.GetUserByIdAsync(userId.Value);
                    if (user == null)
                    {
                        await authService.InvalidateTokenAsync(token);
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        await context.Response.CompleteAsync();
                        return;
                    }
                    if (!user.IsActive)
                    {
                        await authService.InvalidateTokenAsync(token);
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        await context.Response.CompleteAsync();
                        return;
                    }
                }
                context.Items["UserId"] = userId.Value;
                context.Items[TokenFromQueryItemKey] = tokenFromQuery;
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.CompleteAsync();
                return;
            }
        }

        await _next(context);
    }
}
