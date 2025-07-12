using ConsoleApp;
using ConsoleApp.Deserializers;
using ConsoleApp.EventArgs;
using Microsoft.Extensions.Configuration;
using System.Reflection;

class Program
{
    private const int TIME_RECEIVED_OFFSET_MINUTES = 5;

    private const string FILE_NAME_PREFIX_EOT = "eot";
    private const string FILE_NAME_PREFIX_DPU = "dpu";

    public delegate void DpuPacketEventHandler(object? sender, DpuPacketEventArgs e);
    public delegate void HotEotPacketEventHandler(object? sender, HotEotPacketEventArgs e);

    public static event DpuPacketEventHandler DpuPacketReceived;
    public static event HotEotPacketEventHandler HotEotPacketReceived;

    static void Main()
    {
        var configuration = ConfigurationHelper.LoadConfiguration();

        var logDirectoryPath = configuration.GetValue<string>("LogDirectoryPath");

        if (string.IsNullOrEmpty(logDirectoryPath) || !Directory.Exists(logDirectoryPath))
        {
            LogError($"Telemetry log file directory path is not valid: {logDirectoryPath}");
            return;
        }

        DeleteOldLogFiles(configuration);

        AddHotEotEventSubscribers(configuration);

        AddDotEventSubscribers(configuration);

        Console.WriteLine($"Telemetry Log Service started.  Processing messages posted with then last {TIME_RECEIVED_OFFSET_MINUTES} minutes...");

        StartBeaconHealthServices(configuration);

        ProcessTelemetryLogFiles(logDirectoryPath);

        // Keep the app running...
        Console.ReadLine();
    }

    private static void ProcessTelemetryLogFiles(string logDirectoryPath)
    {
        var lastPositions = new Dictionary<string, long>();

        while (true)
        {
            try
            {
                // Get log files that weren't deleted due to age
                var todaysLogFilePaths = Directory.GetFiles(logDirectoryPath, "*.log");

                ProcessLogFiles(lastPositions, todaysLogFilePaths);
            }
            catch (IOException ex)
            {
                LogError("Error reading the file: " + ex.Message);
            }

            Thread.Sleep(1000); // Wait before checking the files again.
        }
    }

    private static void AddDotEventSubscribers(IConfiguration configuration)
    {
        var dotSubscriberTypes = configuration.GetSection("DpuPacketSubscription:Subscribers").Get<string[]>();

        if (dotSubscriberTypes == null)
        {
            LogError("No DPU subscribers found in the configuration.");
        }

        foreach (var subscriberType in dotSubscriberTypes)
        {
            SubscribeToPacketEvent(subscriberType, "DpuPacketReceived", "OnDpuPacketReceived");
        }
    }

    private static void AddHotEotEventSubscribers(IConfiguration configuration)
    {
        var hotEotSubscriberTypes = configuration.GetSection("HotEotPacketSubscription:Subscribers").Get<string[]>();

        if (hotEotSubscriberTypes == null)
        {
            LogError("No HOT / EOT subscribers found in the configuration.");
        }

        foreach (var subscriberType in hotEotSubscriberTypes)
        {
            SubscribeToPacketEvent(subscriberType, "HotEotPacketReceived", "OnHotEotPacketReceived");
        }
    }

    private static void DeleteOldLogFiles(IConfiguration configuration)
    {
        var logDirectoryPath = configuration.GetValue<string>("LogDirectoryPath");

        if (string.IsNullOrEmpty(logDirectoryPath) || !Directory.Exists(logDirectoryPath))
        {
            LogError("Log directory path is not valid.");
            return;
        }

        var logFiles = Directory.GetFiles(logDirectoryPath, "*.log");

        // Find the most recent eot*.log file
        var latestEotFile = logFiles
            .Where(f => Path.GetFileName(f).ToLower().StartsWith("eot"))
            .OrderByDescending(f => File.GetLastWriteTime(f))
            .FirstOrDefault();

        // Find the most recent dpu*.log file
        var latestDpuFile = logFiles
            .Where(f => Path.GetFileName(f).ToLower().StartsWith("dpu"))
            .OrderByDescending(f => File.GetLastWriteTime(f))
            .FirstOrDefault();

        foreach (var file in logFiles)
        {
            // Skip deletion if it's the most recent eot or dpu file
            if (string.Equals(file, latestEotFile, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(file, latestDpuFile, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                File.Delete(file);
                Console.WriteLine($"Deleted file: {Path.GetFileName(file)}");
            }
            catch (Exception ex)
            {
                LogError($"Error deleting {Path.GetFileName(file)}: {ex.Message}");
            }
        }
    }

    private static void LogError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine(message);
        Console.ResetColor();
    }

    private static void ProcessLogFiles(Dictionary<string, long> lastPositions, string[] logFilePaths)
    {
        foreach (var logFilePath in logFilePaths)
        {
            ProcessLogFile(lastPositions, logFilePath);
        }
    }

    private static void ProcessLogFile(Dictionary<string, long> lastPositions, string logFilePath)
    {
        if (!lastPositions.ContainsKey(logFilePath))
        {
            lastPositions[logFilePath] = 0L;
        }

        using FileStream fileStream = new(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        fileStream.Seek(lastPositions[logFilePath], SeekOrigin.Begin);

        using StreamReader reader = new(fileStream);

        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();

            if (IsHeaderRow(line))
            {
                continue;
            }

            if (Path.GetFileName(logFilePath).StartsWith(FILE_NAME_PREFIX_EOT, StringComparison.OrdinalIgnoreCase))
            {
                var hotPacket = HotEotDeserializer.Deserialize(line);

                if (hotPacket.TimeReceived.ToLocalTime() <= DateTime.Now.AddMinutes(-TIME_RECEIVED_OFFSET_MINUTES))
                {
                    continue; // Ignore packets older than 5 minutes
                }

                Console.WriteLine(line);

                HotEotPacketReceived?.Invoke(null, new HotEotPacketEventArgs(hotPacket));
            }

            if (Path.GetFileName(logFilePath).StartsWith(FILE_NAME_PREFIX_DPU, StringComparison.OrdinalIgnoreCase))
            {
                var dpuPacket = DpuDeserializer.Deserialize(line);

                if (dpuPacket.TimeReceived.ToLocalTime() <= DateTime.Now.AddMinutes(-TIME_RECEIVED_OFFSET_MINUTES))
                {
                    continue; // Ignore packets older than 5 minutes
                }

                Console.WriteLine(line);

                DpuPacketReceived?.Invoke(null, new DpuPacketEventArgs(dpuPacket));
            }

            lastPositions[logFilePath] = fileStream.Position;
        }
    }

    private static bool IsHeaderRow(string? line)
    {
        return line.StartsWith("#");
    }

    private static void OnHotEotPacketReceived(object sender, HotEotPacketEventArgs e)
    {
        // Event subscribers will implement this method.
    }

    private static void OnDpuPacketReceived(object sender, DpuPacketEventArgs e)
    {
        // Event subscribers will implement this method.
    }

    private static void StartBeaconHealthServices(IConfiguration configuration)
    {
        var beaconHealthServiceTypes = configuration.GetSection("BeaconHealthServices:Services").Get<string[]>();

        if (beaconHealthServiceTypes == null)
        {
            LogError("No beacon health services in the configuration.");
        }

        foreach (var serviceType in beaconHealthServiceTypes)
        {
            StartBeaconHealthService(serviceType, "Start");
        }
    }

    private static void StartBeaconHealthService(string serviceTypeName, string methodName)
    {
        var type = Type.GetType(serviceTypeName);
        if (type != null)
        {
            var service = Activator.CreateInstance(type);
            if (service != null)
            {
                var methodInfo = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
                if (methodInfo != null)
                {
                    methodInfo.Invoke(service, null);
                }
                else
                {
                    LogError($"Method {methodName} not found in {serviceTypeName}.");
                }
            }
        }
    }

    private static void SubscribeToPacketEvent(string subscriberTypeName, string eventName, string methodName)
    {
        var type = Type.GetType(subscriberTypeName);
        if (type != null)
        {
            var subscriber = Activator.CreateInstance(type);
            if (subscriber != null)
            {
                var eventInfo = typeof(Program).GetEvent(eventName);
                var methodInfo = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
                if (eventInfo != null && methodInfo != null)
                {
                    var handler = Delegate.CreateDelegate(eventInfo.EventHandlerType, subscriber, methodInfo);
                    eventInfo.AddEventHandler(null, handler);
                }
            }
        }
    }
}
