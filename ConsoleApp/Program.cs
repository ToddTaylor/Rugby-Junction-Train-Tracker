using Services;
using Services.Models;
using System.Text.Json;

string configPath = "appsettings.Production.json";
string firstRunFlagPath = "firstrun.flag";

// Only prompt and update config on first run
if (!File.Exists(firstRunFlagPath))
{
    var json = File.ReadAllText(configPath);
    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    var settings = JsonSerializer.Deserialize<AppSettings>(json, options);

    if (settings?.Subscribers.Count > 0)
    {
        Console.Write("Enter Beacon ID for the Subscribers: ");
        settings.Subscribers[0].Beacon.BeaconID = int.Parse(Console.ReadLine());

        Console.Write("Enter the API Key: ");
        settings.Subscribers[0].ApiSettings.ApiKey = Console.ReadLine();
    }

    var updatedJson = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
    File.WriteAllText(configPath, updatedJson);

    File.WriteAllText(firstRunFlagPath, "initialized");
}

// Start processing telemetry logs
var configuration = ConfigurationHelper.LoadConfiguration();
SoftEOTLogService.ProcessLogs(configuration);