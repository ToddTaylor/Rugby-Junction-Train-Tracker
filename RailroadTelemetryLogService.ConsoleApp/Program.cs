using ConsoleApp.Deserializers;
using ConsoleApp.EventArgs;
using Microsoft.Extensions.Configuration;
using System.Reflection;

class Program
{
    public delegate void HotPacketEventHandler(object sender, HotPacketEventArgs e);

    public static event HotPacketEventHandler HotPacketReceived;

    static void Main()
    {
        var configuration = LoadConfiguration();

        // Add subscribers to the HotPacketReceived event
        var subscriberTypes = configuration.GetSection("HotPacketSubscription:Subscribers").Get<string[]>();

        foreach (var subscriberType in subscriberTypes)
        {
            SubscribeToHotPacketEvent(subscriberType);
        }

        var logDirectoryPath = @"C:\TrainDetection\Logs";
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

                        if (Path.GetFileName(logFilePath).StartsWith("eot", StringComparison.OrdinalIgnoreCase))
                        {
                            Console.WriteLine(line);
                            var hotPacket = HotEotDeserializer.Deserialize(line);
                            HotPacketReceived?.Invoke(null, new HotPacketEventArgs(hotPacket));
                            lastPositions[logFilePath] = fileStream.Position;
                        }

                        if (Path.GetFileName(logFilePath).StartsWith("dpu", StringComparison.OrdinalIgnoreCase))
                        {
                            // TODO: Deserialize DPU packet
                        }
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

    private static void SubscribeToHotPacketEvent(string subscriberTypeName)
    {
        var type = Type.GetType(subscriberTypeName);
        if (type != null)
        {
            var subscriber = Activator.CreateInstance(type);
            if (subscriber != null)
            {
                var eventInfo = typeof(Program).GetEvent("HotPacketReceived");
                var methodInfo = type.GetMethod("OnHotPacketReceived", BindingFlags.NonPublic | BindingFlags.Instance);
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

    private static void OnHotPacketReceived(object sender, HotPacketEventArgs e)
    {
        // Event subscribers will implement this method.
    }
}
