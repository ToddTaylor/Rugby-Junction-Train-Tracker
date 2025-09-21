using Services;
using Services.Models;
using System.Text.Json;

var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
string configPath = $"appsettings.{environment}.json";
const string firstRunFlagPath = "firstrun.flag";

void ShowSettingsSummary(AppSettings? appSettings)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("\nCurrent configuration settings:");
    if (appSettings != null)
    {
        Console.WriteLine($"  Log Directory Path: {appSettings.LogDirectoryPath}");
        if (appSettings.Subscribers != null && appSettings.Subscribers.Count > 0)
        {
            Console.WriteLine($"  Beacon ID: {appSettings.Subscribers[0].Beacon.BeaconID}");
            Console.WriteLine($"  API Key: {appSettings.Subscribers[0].ApiSettings.ApiKey}");
        }
    }
    Console.ResetColor();
    Console.WriteLine("\nPress Ctrl+U to update these settings at any time while the application is running.");
    Console.WriteLine("\nPress Ctrl+C to quit.\n");
}

void PromptAndUpdateSettings(AppSettings? appSettings)
{
    if (appSettings != null)
    {
        // Validate LogDirectoryPath
        string logDirInput;
        while (true)
        {
            Console.Write($"Enter Log Directory Path [{appSettings.LogDirectoryPath}]: ");
            logDirInput = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(logDirInput))
            {
                // Use existing value if nothing entered
                logDirInput = appSettings.LogDirectoryPath;
            }
            if (Directory.Exists(logDirInput))
            {
                appSettings.LogDirectoryPath = logDirInput;
                break;
            }
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Invalid directory path. Please enter a valid, existing directory.");
            Console.ResetColor();
        }

        if (appSettings.Subscribers != null && appSettings.Subscribers.Count > 0)
        {
            Console.Write($"Enter Beacon ID for the Subscriber (Integer): ");
            var beaconIdInput = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(beaconIdInput))
                appSettings.Subscribers[0].Beacon.BeaconID = beaconIdInput;

            // Validate API Key as GUID
            string apiKeyInput;
            while (true)
            {
                Console.Write($"Enter the API Key (GUID): ");
                apiKeyInput = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(apiKeyInput) && Guid.TryParse(apiKeyInput, out _))
                {
                    appSettings.Subscribers[0].ApiSettings.ApiKey = apiKeyInput;
                    break;
                }
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Invalid API Key. Please enter a valid GUID.");
                Console.ResetColor();
            }
        }
    }

    var updatedJson = JsonSerializer.Serialize(appSettings, new JsonSerializerOptions { WriteIndented = true });
    File.WriteAllText(configPath, updatedJson);
    File.WriteAllText(firstRunFlagPath, "initialized");

    // Show summary after update
    ShowSettingsSummary(appSettings);
}

var json = File.ReadAllText(configPath);
var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
var appSettings = JsonSerializer.Deserialize<AppSettings>(json, options);

// Only prompt and update config on first run
if (!File.Exists(firstRunFlagPath))
{
    PromptAndUpdateSettings(appSettings);
}
else
{
    // Show summary and instructions even if not first run
    ShowSettingsSummary(appSettings);
}

// Listen for Ctrl+U in a background task
Task.Run(() =>
{
    while (true)
    {
        var keyInfo = Console.ReadKey(true);
        if (keyInfo.Key == ConsoleKey.U && keyInfo.Modifiers.HasFlag(ConsoleModifiers.Control))
        {
            Console.WriteLine("\nCtrl+U detected. Update configuration:");
            PromptAndUpdateSettings(appSettings);
        }
    }
});

// Start processing telemetry logs
SoftEOTLogService.ProcessLogs(appSettings);