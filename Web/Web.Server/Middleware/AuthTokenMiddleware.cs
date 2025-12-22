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
                    ?? context.Request.Headers["X-Auth-Token"].FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(token))
        {
            var (isValid, userId) = await authService.ValidateAndRefreshTokenAsync(token);
            if (isValid && userId.HasValue)
            {
                // Store userId in HttpContext items for potential use by controllers
                context.Items["UserId"] = userId.Value;
            }
        }

        await _next(context);
    }
}
