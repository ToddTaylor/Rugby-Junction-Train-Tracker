using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Web.Server.BackgroundServices;
using Web.Server.Data;
using Web.Server.Hubs;
using Web.Server.Mappers;
using Web.Server.Providers;
using Web.Server.Repositories;
using Web.Server.Services;
using Web.Server.Services.Rules;

// Configure Serilog from appsettings.json (with fallback to code-based configuration)
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .CreateLogger();

try
{
    Log.Information("Starting Web Server application");

    var builder = WebApplication.CreateBuilder(args);

    // Use Serilog for logging
    builder.Host.UseSerilog();

    // Add configuration files
    builder.Configuration
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables();

    var corsPolicyName = "AllowFrontend";

    // Register services
    builder.Services.AddCors(options =>
    {
        options.AddPolicy(name: corsPolicyName, policy =>
        {
            policy.WithOrigins("https://localhost:53848", "https://dev.rugbyjunction.us", "https://rugbyjunction.us") // Frontend origin
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials(); // needed if you're using SignalR or cookies
        });
    });

    // Add services to the container.
    builder.Services.AddAutoMapper(typeof(AutoMapperProfile));
    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });
    builder.Services.AddSignalR(options =>
    {
        // Azure free tier web app does not support WebSockets, so we need to use Server-Sent Events
        options.SupportedProtocols.Add(HttpTransportType.WebSockets.ToString());
        options.EnableDetailedErrors = true;
    });

    // Add DbContext with SQLite connection string
    builder.Services.AddDbContext<TelemetryDbContext>(options =>
        options.UseSqlite(builder.Configuration.GetConnectionString("TelemetryDatabase")));

    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
        options.EnableAnnotations();
        options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "Train Telemetry API",
            Version = "v1",
            Description = "API for reporting train telemetry data for train tracking."
        });
        options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
        {
            Description = "API Key needed to access the endpoints. Add it to the request headers using the key 'X-Api-Key'.",
            Type = SecuritySchemeType.ApiKey,
            Name = "X-Api-Key",
            In = ParameterLocation.Header,
            Scheme = "ApiKeyScheme"
        });

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "ApiKey"
                },
                Scheme = "ApiKeyScheme",
                Name = "ApiKey",
                In = ParameterLocation.Header
            },
            new List<string>()
        }
        });
    });

    // Custom repositories
    builder.Services.AddScoped<IBeaconRepository, BeaconRepository>();
    builder.Services.AddScoped<IBeaconRailroadRepository, BeaconRailroadRepository>();
    builder.Services.AddScoped<IMapPinRepository, MapPinRepository>();
    builder.Services.AddScoped<IMapPinHistoryRepository, MapPinHistoryRepository>();
    builder.Services.AddScoped<IUserRepository, UserRepository>();
    builder.Services.AddScoped<IRailroadRepository, RailroadRepository>();
    builder.Services.AddScoped<ISubdivisionRepository, SubdivisionRepository>();
    builder.Services.AddScoped<ITelemetryRepository, TelemetryRepository>();
    builder.Services.AddScoped<IUserTrackedPinRepository, UserTrackedPinRepository>();
    builder.Services.AddScoped<ISubdivisionTrackageRightRepository, SubdivisionTrackageRightRepository>();

    // Custom services
    builder.Services.AddScoped<IBeaconService, BeaconService>();
    builder.Services.AddScoped<IBeaconRailroadService, BeaconRailroadService>();
    builder.Services.AddScoped<IMapPinService, MapPinService>();
    builder.Services.AddScoped<IMapPinHistoryService, MapPinHistoryService>();
    builder.Services.AddScoped<IUserService, UserService>();
    builder.Services.AddScoped<IRailroadService, RailroadService>();
    builder.Services.AddScoped<ISubdivisionService, SubdivisionService>();
    builder.Services.AddScoped<ITelemetryService, TelemetryService>();
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<IUserTrackedPinService, UserTrackedPinService>();
    builder.Services.AddScoped<ISubdivisionTrackageRightService, SubdivisionTrackageRightService>();
    builder.Services.AddScoped<IEmailService, EmailService>();

    // Register telemetry rules
    builder.Services.AddScoped<ITelemetryRule, DpuAntiPingPongRule>();
    builder.Services.AddScoped<ITelemetryRule, EotHotAntiPingPongRule>();
    builder.Services.AddScoped<ITelemetryRuleEngine, TelemetryRuleEngine>();

    // Register map pin rules - Order matters!
    builder.Services.AddScoped<IMapPinRule, TrackageRightsRule>();
    builder.Services.AddScoped<IMapPinRule, TrainSpeedSanityCheckRule>();
    builder.Services.AddScoped<IMapPinRuleEngine, MapPinRuleEngine>();

    builder.Services.AddScoped<ITimeProvider, SystemTimeProvider>();

    builder.Services.AddHostedService<BeaconRailroadHealthService>();
    builder.Services.AddHostedService<RecordCleanupService>();
    builder.Services.AddHostedService<TelemetryConsumerService>();

    var app = builder.Build();

    // CORS must come early in the pipeline
    app.UseCors(corsPolicyName);

    // Automatically creates DB and applies migrations.
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();
        try
        {
            dbContext.Database.Migrate();
            SeedData.SeedDatabase(dbContext);
        }
        catch (Exception ex)
        {
            File.WriteAllText("db-migration-error.txt", ex.ToString());
            throw;
        }
    }

    // Use the API Key Middleware
    app.UseMiddleware<Web.Server.Middleware.ApiKeyMiddleware>();

    // Use the Auth Token Middleware (updates LastLogin)
    app.UseMiddleware<Web.Server.Middleware.AuthTokenMiddleware>();

    app.UseDefaultFiles();

    // Enable .geojson file serving
    var provider = new FileExtensionContentTypeProvider();
    provider.Mappings[".geojson"] = "application/json";

    app.UseStaticFiles(new StaticFileOptions
    {
        ContentTypeProvider = provider
    });

    // Configure the HTTP request pipeline.

    //if (app.Environment.IsDevelopment()) { }
    app.UseSwagger(options =>
    {
        options.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi2_0;
    });
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Telemetry API v1");
    });
    //}

    app.UseHttpsRedirection();

    app.UseAuthorization();

    app.MapControllers();
    app.MapHub<NotificationHub>("/hubs/notificationhub");
    app.MapFallbackToFile("/index.html");

    app.Run();

}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
