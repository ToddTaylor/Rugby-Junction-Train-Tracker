using Web.Server.Services;

namespace Web.Server.Middleware;

public class AuthTokenMiddleware
{
    private readonly RequestDelegate _next;

    public AuthTokenMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IAuthService authService)
    {
        // Check for auth token in Authorization header or X-Auth-Token header
        var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "")
                ?? context.Request.Headers["X-Auth-Token"].FirstOrDefault()
                ?? context.Request.Query["access_token"].FirstOrDefault();

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
