using Services;
using Services.Models;
using System.Text.Json;

var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
string configPath = $"appsettings.{environment}.json";

var json = File.ReadAllText(configPath);
var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
var appSettings = JsonSerializer.Deserialize<AppSettings>(json, options);

// Pass appSettings to SoftEOTLogService as before
SoftEOTLogService.ProcessLogs(appSettings);