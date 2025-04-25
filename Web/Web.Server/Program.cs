using Microsoft.EntityFrameworkCore;
using Web.Server.Data;
using Web.Server.Hubs;
using Web.Server.Mappers;
using Web.Server.Services;

var builder = WebApplication.CreateBuilder(args);

var corsPolicyName = "AllowFrontend";

// Register services
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: corsPolicyName, policy =>
    {
        policy.WithOrigins("https://localhost:53848") // Frontend origin
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // needed if you're using SignalR or cookies
    });
});

// Add services to the container.
builder.Services.AddAutoMapper(typeof(AutoMapperProfile));
builder.Services.AddControllers();
builder.Services.AddSignalR();

// Add DbContext with SQLite connection string
builder.Services.AddDbContext<TelemetryDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("TelemetryDatabase")));

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.EnableAnnotations();
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Train Telemetry API",
        Version = "v1",
        Description = "API for reporting train telemetry data for train tracking."
    });
});

// Custom services
builder.Services.AddScoped<ITelemetryService, TelemetryService>();

var app = builder.Build();

// CORS must come early in the pipeline
app.UseCors(corsPolicyName);

app.UseDefaultFiles();
app.UseStaticFiles();

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
