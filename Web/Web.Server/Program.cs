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
using Web.Server.Enums;
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

    // Add AutoMapper with license key.
    builder.Services.AddAutoMapper(cfg =>
    {
        // Annoying Community edition license key created on 03/29/2026. Needs to be renwed every year.
        cfg.LicenseKey = "eyJhbGciOiJSUzI1NiIsImtpZCI6Ikx1Y2t5UGVubnlTb2Z0d2FyZUxpY2Vuc2VLZXkvYmJiMTNhY2I1OTkwNGQ4OWI0Y2IxYzg1ZjA4OGNjZjkiLCJ0eXAiOiJKV1QifQ.eyJpc3MiOiJodHRwczovL2x1Y2t5cGVubnlzb2Z0d2FyZS5jb20iLCJhdWQiOiJMdWNreVBlbm55U29mdHdhcmUiLCJleHAiOiIxODA2Mjc4NDAwIiwiaWF0IjoiMTc3NDc5OTc5MyIsImFjY291bnRfaWQiOiIwMTlkM2E0ZWZjOWU3MTNhOTgxYzE3Yzg0MzIxNjUwOCIsImN1c3RvbWVyX2lkIjoiY3RtXzAxa214NHpiZW16NGp3NWhlbWRmNzkzMXRtIiwic3ViX2lkIjoiLSIsImVkaXRpb24iOiIwIiwidHlwZSI6IjIifQ.EUX1_Rkp6Rc2F-6OU-kaTI8Y6BdUTU-UiplALhYRo7t7eMJlbceUCpQxOMbn-GHq3MzqoODkBgRjsZL4BX4Kj76UQHZsYbtKrpaNNTq7a4Odq46jBP9hL6U9M3vucWnF-uh6CiLWmmSmn-BHad9fK6QrXdrCDWTE1glEdZuSII7vymgOGdpwC8bj0U2orofHwdz941tw2-hFarKlDHrFdA0DpBKmiAy8mZOo7aXdHsV-TjDtAL4i7TnphOuc7slxiSz54OCXorbCe-EHOzBO1UuV7YL1Ht9cFy9ArRla0s-QzLjsuZE9KOKIBhJXGmP8CFfoUIWPdArGG9kfIbpUjg";
        cfg.AddProfile<AutoMapperProfile>();
    });

    // Add services to the container.
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

    // Map pin processors - register each processor
    builder.Services.AddScoped<Web.Server.Services.Processors.DpuMapPinProcessor>();
    builder.Services.AddScoped<Web.Server.Services.Processors.HotEotMapPinProcessor>();

    // Custom services
    builder.Services.AddScoped<IBeaconService, BeaconService>();
    builder.Services.AddScoped<IBeaconRailroadService, BeaconRailroadService>();

    // Register MapPinService with processor map factory
    builder.Services.AddScoped<IMapPinService>(sp =>
    {
        var dpu = sp.GetRequiredService<Web.Server.Services.Processors.DpuMapPinProcessor>();
        var hotEot = sp.GetRequiredService<Web.Server.Services.Processors.HotEotMapPinProcessor>();

        var processorMap = new Dictionary<string, Web.Server.Services.Processors.IMapPinProcessor>
        {
            { SourceEnum.DPU, dpu },
            { SourceEnum.HOT, hotEot },
            { SourceEnum.EOT, hotEot },
        };

        return new MapPinService(
            sp.GetRequiredService<Web.Server.Services.IBeaconRailroadService>(),
            sp.GetRequiredService<Web.Server.Services.IMapPinHistoryService>(),
            sp.GetRequiredService<Web.Server.Repositories.IMapPinRepository>(),
            sp.GetRequiredService<Microsoft.AspNetCore.SignalR.IHubContext<Web.Server.Hubs.NotificationHub>>(),
            sp.GetRequiredService<AutoMapper.IMapper>(),
            sp.GetRequiredService<Web.Server.Providers.ITimeProvider>(),
            sp.GetRequiredService<Web.Server.Repositories.ITelemetryRepository>(),
            sp.GetRequiredService<Web.Server.Services.Rules.IMapPinRuleEngine>(),
            sp.GetRequiredService<Web.Server.Services.Rules.ITelemetryRuleEngine>(),
            sp.GetRequiredService<Web.Server.Repositories.ISubdivisionTrackageRightRepository>(),
            sp.GetRequiredService<Web.Server.Repositories.IUserTrackedPinRepository>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Web.Server.Services.MapPinService>>(),
            sp.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>(),
            processorMap);
    });

    builder.Services.AddScoped<IMapPinHistoryService, MapPinHistoryService>();
    builder.Services.AddScoped<IUserService, UserService>();
    builder.Services.AddScoped<IRailroadService, RailroadService>();
    builder.Services.AddScoped<ISubdivisionService, SubdivisionService>();
    builder.Services.AddScoped<ITelemetryService, TelemetryService>();
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<IUserTrackedPinService, UserTrackedPinService>();
    builder.Services.AddScoped<ISubdivisionTrackageRightService, SubdivisionTrackageRightService>();
    builder.Services.AddScoped<IEmailService, EmailService>();

    // Register telemetry rules - Order matters!
    builder.Services.AddScoped<ITelemetryRule, DpuAntiPingPongRule>();
    builder.Services.AddScoped<ITelemetryRule, EotHotAntiPingPongRule>();
    builder.Services.AddScoped<ITelemetryRule, TrainSpeedSanityCheckRule>();
    builder.Services.AddScoped<ITelemetryRuleEngine, TelemetryRuleEngine>();

    // Register map pin rules - Order matters!
    builder.Services.AddScoped<IMapPinRule, TrackageRightsRule>();
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
