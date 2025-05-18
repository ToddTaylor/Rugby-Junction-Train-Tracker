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
        var configuration = LoadConfiguration();

        // Add subscribers to the events
        var hotEotSubscriberTypes = configuration.GetSection("HotEotPacketSubscription:Subscribers").Get<string[]>();
        var dotSubscriberTypes = configuration.GetSection("DpuPacketSubscription:Subscribers").Get<string[]>();

        if (hotEotSubscriberTypes == null || dotSubscriberTypes == null)
        {
            LogError("No subscribers found in the configuration.");
            return;
        }

        foreach (var subscriberType in hotEotSubscriberTypes)
        {
            SubscribeToPacketEvent(subscriberType, "HotEotPacketReceived", "OnHotEotPacketReceived");
        }

        foreach (var subscriberType in dotSubscriberTypes)
        {
            SubscribeToPacketEvent(subscriberType, "DpuPacketReceived", "OnDpuPacketReceived");
        }

        var logDirectoryPath = configuration.GetValue<string>("LogDirectoryPath");

        if (string.IsNullOrEmpty(logDirectoryPath) || !Directory.Exists(logDirectoryPath))
        {
            LogError("Log directory path is not valid.");
            return;
        }

        var lastPositions = new Dictionary<string, long>();

        Console.WriteLine($"Telemetry Log Service started.  Processing messages posted with then last {TIME_RECEIVED_OFFSET_MINUTES} minutes...");

        while (true)
        {
            try
            {
                // Get EOT and DPU log files for today
                var todaysLogFilePaths = Directory.GetFiles(logDirectoryPath, $"*{GetTodayDateString()}.log");

                ProcessLogFiles(lastPositions, todaysLogFilePaths);
            }
            catch (IOException ex)
            {
                LogError("Error reading the file: " + ex.Message);
            }

            Thread.Sleep(1000); // Wait before checking the files again.
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

                if (hotPacket.TimeReceived <= DateTime.Now.AddMinutes(-TIME_RECEIVED_OFFSET_MINUTES))
                {
                    continue; // Ignore packets older than 5 minutes
                }

                Console.WriteLine(line);

                HotEotPacketReceived?.Invoke(null, new HotEotPacketEventArgs(hotPacket));
            }

            if (Path.GetFileName(logFilePath).StartsWith(FILE_NAME_PREFIX_DPU, StringComparison.OrdinalIgnoreCase))
            {
                var dpuPacket = DpuDeserializer.Deserialize(line);

                if (dpuPacket.TimeReceived <= DateTime.Now.AddMinutes(-TIME_RECEIVED_OFFSET_MINUTES))
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

    private static IConfiguration LoadConfiguration()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

        return builder.Build();
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

    private static string GetTodayDateString()
    {
        return DateTime.Now.ToString("yyyyMMdd");
    }

    private static void OnHotEotPacketReceived(object sender, HotEotPacketEventArgs e)
    {
        // Event subscribers will implement this method.
    }

    private static void OnDpuPacketReceived(object sender, DpuPacketEventArgs e)
    {
        // Event subscribers will implement this method.
    }
}
