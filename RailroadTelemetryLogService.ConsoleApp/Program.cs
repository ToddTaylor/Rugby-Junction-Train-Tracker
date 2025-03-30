using ConsoleApp.Deserializers;
using ConsoleApp.EventArgs;
using Microsoft.Extensions.Configuration;
using System.Reflection;

class Program
{
    public delegate void DpuPacketEventHandler(object sender, DpuPacketEventArgs e);
    public delegate void HotEotPacketEventHandler(object sender, HotEotPacketEventArgs e);
    public static event DpuPacketEventHandler DpuPacketReceived;
    public static event HotEotPacketEventHandler HotEotPacketReceived;

    static void Main()
    {
        var configuration = LoadConfiguration();

        // Add subscribers to the events
        var hotEotSubscriberTypes = configuration.GetSection("HotEotPacketSubscription:Subscribers").Get<string[]>();
        var dotSubscriberTypes = configuration.GetSection("DpuPacketSubscription:Subscribers").Get<string[]>();

        foreach (var subscriberType in hotEotSubscriberTypes)
        {
            SubscribeToPacketEvent(subscriberType, "HotEotPacketReceived", "OnHotEotPacketReceived");
        }

        foreach (var subscriberType in dotSubscriberTypes)
        {
            SubscribeToPacketEvent(subscriberType, "DpuPacketReceived", "OnDpuPacketReceived");
        }

        var logDirectoryPath = configuration.GetValue<string>("LogDirectoryPath");
        var lastPositions = new Dictionary<string, long>();

        while (true)
        {
            try
            {
                var logFilePaths = Directory.GetFiles(logDirectoryPath, $"*{GetTodayDateString()}.log");

                foreach (var logFilePath in logFilePaths)
                {
                    if (!lastPositions.ContainsKey(logFilePath))
                    {
                        lastPositions[logFilePath] = 0L;
                    }

                    using var fileStream = new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    fileStream.Seek(lastPositions[logFilePath], SeekOrigin.Begin);

                    using var reader = new StreamReader(fileStream);

                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine();

                        if (IsHeaderRow(line))
                        {
                            continue;
                        }

                        Console.WriteLine(line);

                        if (Path.GetFileName(logFilePath).StartsWith("eot", StringComparison.OrdinalIgnoreCase))
                        {
                            var hotPacket = HotEotDeserializer.Deserialize(line);
                            HotEotPacketReceived?.Invoke(null, new HotEotPacketEventArgs(hotPacket));
                        }

                        if (Path.GetFileName(logFilePath).StartsWith("dpu", StringComparison.OrdinalIgnoreCase))
                        {
                            var dpuPacket = DpuDeserializer.Deserialize(line);
                            DpuPacketReceived?.Invoke(null, new DpuPacketEventArgs(dpuPacket));
                        }

                        lastPositions[logFilePath] = fileStream.Position;
                    }
                }
            }
            catch (IOException ex)
            {
                Console.WriteLine("Error reading the file: " + ex.Message);
            }

            Thread.Sleep(1000); // Wait before checking the files again.
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
