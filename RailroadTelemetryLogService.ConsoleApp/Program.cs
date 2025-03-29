using ConsoleApp.Deserializers;
using ConsoleApp.EventArgs;
using RailroadTelemetryLogService.ConsoleApp;

class Program
{
    public delegate void HotPacketEventHandler(object sender, HotPacketEventArgs e);

    public static event HotPacketEventHandler HotPacketReceived;

    static void Main()
    {
        HotEotPacketSubscriber subscriber = new HotEotPacketSubscriber();
        HotPacketReceived += OnHotPacketReceived;

        var logFilePath = $@"C:\TrainDetection\Logs\eot{GetTodayDateString()}.log";
        var lastPosition = 0L;

        while (true)
        {
            try
            {
                using var fileStream = new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                fileStream.Seek(lastPosition, SeekOrigin.Begin);

                using var reader = new StreamReader(fileStream);

                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();

                    Console.WriteLine(line);

                    if (line.StartsWith("#"))
                    {
                        continue;
                    }

                    var hotPacket = HotEotDeserializer.Deserialize(line);

                    // Raise the event
                    HotPacketReceived?.Invoke(null, new HotPacketEventArgs(hotPacket));
                }

                lastPosition = fileStream.Position;
            }
            catch (IOException ex)
            {
                Console.WriteLine("Error reading the file: " + ex.Message);
            }

            Thread.Sleep(1000); // Wait before checking the file again.
        }
    }

    static string GetTodayDateString()
    {
        return DateTime.Now.ToString("yyyyMMdd");
    }

    static void OnHotPacketReceived(object sender, HotPacketEventArgs e)
    {
        //Console.WriteLine($"HotPacket received: {e.HotPacket}");
    }
}