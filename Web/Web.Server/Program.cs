using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using System.Reflection;
using System.Text.Json.Serialization;
using Web.Server.Data;
using Web.Server.Hubs;
using Web.Server.Mappers;
using Web.Server.Providers;
using Web.Server.Repositories;
using Web.Server.Services;

var builder = WebApplication.CreateBuilder(args);

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
        policy.WithOrigins("https://localhost:53848", "https://traintelemetry20250416082903-h9f5efhzf0hzbcda.centralus-01.azurewebsites.net") // Frontend origin
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // needed if you're using SignalR or cookies
    });
});

// Add services to the container.
builder.Services.AddAutoMapper(typeof(AutoMapperProfile));
builder.Services.AddControllers().AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddSignalR();

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
builder.Services.AddScoped<IOwnerRepository, OwnerRepository>();
builder.Services.AddScoped<IRailroadRepository, RailroadRepository>();
builder.Services.AddScoped<ITelemetryRepository, TelemetryRepository>();

// Custom services
builder.Services.AddScoped<IBeaconService, BeaconService>();
builder.Services.AddScoped<IBeaconRailroadService, BeaconRailroadService>();
builder.Services.AddScoped<IOwnerService, OwnerService>();
builder.Services.AddScoped<IRailroadService, RailroadService>();
builder.Services.AddScoped<ITelemetryService, TelemetryService>();

builder.Services.AddScoped<ITimeProvider, SystemTimeProvider>();

var app = builder.Build();

// Automatically creates DB and applies migrations.
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();
    dbContext.Database.Migrate();
}

// Use the API Key Middleware
app.UseMiddleware<Web.Server.Middleware.ApiKeyMiddleware>();

// CORS must come early in the pipeline
app.UseCors(corsPolicyName);

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
