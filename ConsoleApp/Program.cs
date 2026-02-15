using Services;
using Services.Models;
using System.Text.Json;

var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";

// Store config and flag in AppData (persists across ClickOnce updates)
var appDataPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "RugbyJunctionTrainTracker");

// Ensure the directory exists
Directory.CreateDirectory(appDataPath);

string configPath = Path.Combine(appDataPath, $"appsettings.{environment}.json");
string firstRunFlagPath = Path.Combine(appDataPath, "firstrun.flag");

// For ClickOnce compatibility, resolve bundled config from the application directory
// instead of using a relative path that might not work in different deployment scenarios
string bundledConfigPath = Path.Combine(
    AppContext.BaseDirectory,  // Gets the application's base directory (works correctly with ClickOnce)
    $"appsettings.{environment}.json"
);

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
        string logDirInput = string.Empty;
        while (true)
        {
            Console.Write($"Enter Log Directory Path [{appSettings.LogDirectoryPath}]: ");
            logDirInput = Console.ReadLine() ?? string.Empty;
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
            string apiKeyInput = string.Empty;
            while (true)
            {
                Console.Write($"Enter the API Key [{appSettings.Subscribers[0].ApiSettings.ApiKey}]: ");
                apiKeyInput = Console.ReadLine() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(apiKeyInput))
                {
                    apiKeyInput = appSettings.Subscribers[0].ApiSettings.ApiKey;
                }
                if (!string.IsNullOrWhiteSpace(apiKeyInput) && Guid.TryParse(apiKeyInput, out _))
                {
                    appSettings.Subscribers[0].ApiSettings.ApiKey = apiKeyInput;
                    break;
                }
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Invalid API Key. Please enter a valid value.");
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

// Load configuration - prefer user config, fallback to bundled config
AppSettings? appSettings;
if (File.Exists(configPath))
{
    // User has previously configured the app
    var json = File.ReadAllText(configPath);
    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    appSettings = JsonSerializer.Deserialize<AppSettings>(json, options);
}
else
{
    // First time or bundled config
    var json = File.ReadAllText(bundledConfigPath);
    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    appSettings = JsonSerializer.Deserialize<AppSettings>(json, options);
}

if (appSettings == null)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("Failed to load application settings.");
    Console.ResetColor();
    return;
}

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

// Start processing telemetry logs on a background task
_ = Task.Run(() => SoftEOTLogService.ProcessLogs(appSettings));

// Listen for Ctrl+U in the main thread
while (true)
{
    var keyInfo = Console.ReadKey(true);
    if (keyInfo.Key == ConsoleKey.U && keyInfo.Modifiers.HasFlag(ConsoleModifiers.Control))
    {
        Console.WriteLine("\nCtrl+U detected. Update configuration:");
        PromptAndUpdateSettings(appSettings);
    }
}