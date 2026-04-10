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
            // Allow CORS preflight requests to pass through without API key validation.
            if (HttpMethods.IsOptions(context.Request.Method))
            {
                await _next(context);
                return;
            }

            // Auth bootstrap endpoints are public and should not require API key.
            if (context.Request.Path.StartsWithSegments("/api/v1/auth/send-code", StringComparison.OrdinalIgnoreCase) ||
                context.Request.Path.StartsWithSegments("/api/v1/auth/verify-code", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            // Bypass Swagger and notification hub endpoints
            if (context.Request.Path.StartsWithSegments("/api"))
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

            await _next(context);
        }
    }
}
