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

// If firstRunFlagPath is missing, delete all files in appDataPath before loading config
if (!File.Exists(firstRunFlagPath))
{
    try
    {
        var filesToDelete = Directory.GetFiles(appDataPath);
        foreach (var file in filesToDelete)
        {
            try { File.Delete(file); } catch { /* Ignore errors */ }
        }
    }
    catch { /* Ignore errors */ }
}

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
            foreach (var subscriber in appSettings.Subscribers)
            {
                Console.WriteLine($"  Subscriber {subscriber.ID} Beacon ID: {subscriber.Beacon.BeaconID}");
            }
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
            foreach (var subscriber in appSettings.Subscribers)
            {
                Console.Write($"Enter Beacon ID for Subscriber {subscriber.ID} [{subscriber.Beacon.BeaconID}]: ");
                var beaconIdInput = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(beaconIdInput))
                {
                    subscriber.Beacon.BeaconID = beaconIdInput;
                }
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

// Load additional Subscriber configs from C:\\TrainTracker\\Subscribers
try
{
    string? optionalSubscribersPath = null;
    if (appSettings != null)
    {
        var type = appSettings.GetType();
        var prop = type.GetProperty("OptionalSubscribersPath");
        if (prop != null)
        {
            optionalSubscribersPath = prop.GetValue(appSettings) as string;
        }
        else
        {
            // Try to get from JSON if not mapped in class
            var json = JsonSerializer.Serialize(appSettings);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("OptionalSubscribersPath", out var pathElement))
            {
                optionalSubscribersPath = pathElement.GetString();
            }
        }
    }

    if (!string.IsNullOrWhiteSpace(optionalSubscribersPath) && Directory.Exists(optionalSubscribersPath))
    {
        var subscriberFiles = Directory.GetFiles(optionalSubscribersPath, "*.json");
        foreach (var file in subscriberFiles)
        {
            try
            {
                var extJson = File.ReadAllText(file);
                using var doc = JsonDocument.Parse(extJson);
                if (doc.RootElement.TryGetProperty("Subscriber", out var subscriberElement))
                {
                    var subscriber = subscriberElement.Deserialize<Services.Models.Subscriber>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (subscriber != null && appSettings != null)
                    {
                        // Avoid duplicate IDs
                        if (!appSettings.Subscribers.Any(s => s.ID == subscriber.ID))
                        {
                            appSettings.Subscribers.Add(subscriber);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Warning: Failed to load subscriber from {file}: {ex.Message}");
                Console.ResetColor();
            }
        }
    }
}
catch { /* Ignore errors, continue if path doesn't exist or files are invalid */ }

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
    // Delete all files in the appDataPath directory (including previous appsettings files)
    try
    {
        var filesToDelete = Directory.GetFiles(appDataPath);
        foreach (var file in filesToDelete)
        {
            try { File.Delete(file); } catch { /* Ignore errors */ }
        }
    }
    catch { /* Ignore errors */ }

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