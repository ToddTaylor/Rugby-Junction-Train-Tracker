namespace Web.Server.Middleware
{
    public class ApiKeyMiddleware
    {
        private const string ApiKeyHeaderName = "X-Api-Key";
        private readonly RequestDelegate _next;
        private readonly string _apiKey;

        public ApiKeyMiddleware(RequestDelegate next, IConfiguration configuration)
        {
            _next = next;
            _apiKey = configuration["ApplicationSettings:ApiKey"];
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Apply API key validation only for "/swagger" and "api/v1/[controller]" endpoints
            if (context.Request.Path.StartsWithSegments("/swagger") ||
                context.Request.Path.StartsWithSegments("/api/v1"))
            {
                if (!context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var extractedApiKey))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsync("API Key is missing.");
                    return;
                }

                if (!_apiKey.Equals(extractedApiKey))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsync("Invalid API Key.");
                    return;
                }
            }

            // Allow other requests to proceed without API key validation
            await _next(context);
        }
    }
}
