using RailroadTelemetryLogService.ConsoleApp.Deserializers;

class Program
{
    static void Main()
    {
        var logFilePath = @"C:\TrainDetection\Logs\eot20250329.log";
        var lastPosition = 0L;

        while (true)
        {
            try
            {
                using (var fileStream = new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    fileStream.Seek(lastPosition, SeekOrigin.Begin);
                    using (var reader = new StreamReader(fileStream))
                    {
                        while (!reader.EndOfStream)
                        {
                            var line = reader.ReadLine();
                            Console.WriteLine(line);

                            var hotPacket = HotDeserializer.Deserialize(line);
                        }

                        lastPosition = fileStream.Position;
                    }
                }
            }
            catch (IOException ex)
            {
                Console.WriteLine("Error reading the file: " + ex.Message);
            }

            Thread.Sleep(1000); // Wait before checking the file again.
        }
    }
}

