using Services.Deserializers;
using Services.EventArgs;
using Services.Models;
using System.Reflection;

namespace Services
{
    public class SoftEOTLogService
    {
        // Packets received longer ago than this are skipped: injecting old messages out of order can upset the
        // rules processing in the web app.
        private const int TIME_RECEIVED_OFFSET_MINUTES = 5;

        private const string FILE_NAME_PREFIX_HOT = "hot";
        private const string FILE_NAME_PREFIX_EOT = "eot";
        private const string FILE_NAME_PREFIX_DPU = "dpu";

        /// <summary>
        /// Log file name prefixes written by SoftEOT (eot, hot) and the SoftDPU instances (dpu1 to dpu4).
        /// </summary>
        public static readonly IReadOnlyList<string> LogFilePrefixes = new[] { "eot", "hot", "dpu1", "dpu2", "dpu3", "dpu4" };

        public delegate void DpuPacketEventHandler(object? sender, DpuPacketEventArgs e);
        public delegate void HotEotPacketEventHandler(object? sender, HotEotPacketEventArgs e);

        public static event DpuPacketEventHandler DpuPacketReceived;
        public static event HotEotPacketEventHandler HotEotPacketReceived;

        /// <summary>
        /// Wires up the subscribers, starts the health checks and then reads the log files for as long as the app
        /// runs. A log directory that is missing at startup does not stop it: the health checks still run, so
        /// Server Health shows the PC, and reading starts once the directory appears.
        /// </summary>
        public static void ProcessLogs(AppSettings appSettings)
        {
            DeleteOldLogFiles(appSettings.LogDirectoryPath);

            AddHotEotEventSubscribers(appSettings);

            AddDpuEventSubscribers(appSettings);

            Console.WriteLine($"Telemetry Log Service started.  Processing messages posted within the last {TIME_RECEIVED_OFFSET_MINUTES} minutes...");

            StartBeaconHealthServices(appSettings);

            ProcessTelemetryLogFiles(appSettings);
        }

        private static void ProcessTelemetryLogFiles(AppSettings appSettings)
        {
            var lastPositions = new Dictionary<string, long>();
            string? lastDirectory = null;

            while (true)
            {
                try
                {
                    var logDirectoryPath = appSettings.LogDirectoryPath;

                    if (string.IsNullOrEmpty(logDirectoryPath) || !Directory.Exists(logDirectoryPath))
                    {
                        LogError($"Telemetry log file directory path is not valid: {logDirectoryPath}");
                        Thread.Sleep(1000);
                        continue;
                    }

                    // If directory changed, clear the lastPositions dictionary to start fresh
                    // This ensures we don't skip files when switching to a new directory
                    if (lastDirectory != logDirectoryPath)
                    {
                        lastDirectory = logDirectoryPath;
                        lastPositions.Clear();
                        Console.WriteLine($"Log directory changed to: {logDirectoryPath}");
                    }

                    // Get log files that weren't deleted due to age
                    var todaysLogFilePaths = Directory.GetFiles(logDirectoryPath, "*.log");

                    if (todaysLogFilePaths.Length == 0)
                    {
                        // No files found - just wait and try again
                        Thread.Sleep(1000);
                        continue;
                    }

                    ProcessLogFiles(lastPositions, todaysLogFilePaths);
                }
                catch (IOException ex)
                {
                    LogError("Error reading the file: " + ex.Message);
                }
                catch (Exception ex)
                {
                    LogError($"Unexpected error in log processing loop: {ex.GetType().Name}: {ex.Message}");
                }

                Thread.Sleep(1000); // Wait before checking the files again.
            }
        }

        private static void AddDpuEventSubscribers(AppSettings appsettings)
        {
            var subscribersWithDpu = appsettings.Subscribers
                .Where(s => !string.IsNullOrEmpty(s.DpuPacketSubscriber))
                .ToList();

            if (subscribersWithDpu.Count == 0)
            {
                LogError("No DPU subscribers found in the configuration.");
                return;
            }

            foreach (var subscriber in subscribersWithDpu)
            {
                SubscribeToPacketEvent(appsettings, subscriber.DpuPacketSubscriber!, "DpuPacketReceived", "OnDpuPacketReceived");
            }
        }

        private static void AddHotEotEventSubscribers(AppSettings appsettings)
        {
            var subscribersWithHotEot = appsettings.Subscribers
                .Where(s => !string.IsNullOrEmpty(s.HotEotPacketSubscriber))
                .ToList();

            if (subscribersWithHotEot.Count == 0)
            {
                LogError("No HOT / EOT subscribers found in the configuration.");
                return;
            }

            foreach (var subscriber in subscribersWithHotEot)
            {
                SubscribeToPacketEvent(appsettings, subscriber.HotEotPacketSubscriber!, "HotEotPacketReceived", "OnHotEotPacketReceived");
            }
        }

        private static void DeleteOldLogFiles(String logDirectoryPath)
        {
            if (string.IsNullOrEmpty(logDirectoryPath) || !Directory.Exists(logDirectoryPath))
            {
                LogError("Log directory path is not valid.");
                return;
            }

            var logFiles = Directory.GetFiles(logDirectoryPath, "*.log");

            // Preserve the latest file for each prefix
            var latestFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var prefix in LogFilePrefixes)
            {
                var latestFile = logFiles
                    .Where(f => Path.GetFileName(f).ToLower().StartsWith(prefix))
                    .OrderByDescending(f => File.GetLastWriteTime(f))
                    .FirstOrDefault();
                if (!string.IsNullOrEmpty(latestFile))
                {
                    latestFiles.Add(latestFile);
                }
            }

            foreach (var file in logFiles)
            {
                // Skip deletion if it's the most recent file for any of the prefixes
                if (latestFiles.Contains(file))
                {
                    continue;
                }

                try
                {
                    File.Delete(file);
                    Console.WriteLine($"Deleted old log file: {Path.GetFileName(file)}");
                }
                catch (Exception ex)
                {
                    LogError($"Failed to delete {Path.GetFileName(file)}: {ex.Message}");
                }
            }
        }

        private static void ProcessLogFiles(Dictionary<string, long> lastPositions, string[] logFilePaths)
        {
            foreach (var logFilePath in logFilePaths)
            {
                long lastPosition = lastPositions.ContainsKey(logFilePath) ? lastPositions[logFilePath] : 0;
                ProcessLogFile(lastPositions, logFilePath, lastPosition);
            }
        }

        private static void ProcessLogFile(Dictionary<string, long> lastPositions, string logFilePath, long lastPosition)
        {
            try
            {
                using (var fileStream = new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    fileStream.Seek(lastPosition, SeekOrigin.Begin);

                    using (var reader = new StreamReader(fileStream))
                    {
                        string? line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (!string.IsNullOrWhiteSpace(line) && !IsHeaderRow(line))
                            {
                                ProcessLogLine(line, logFilePath);
                            }
                        }
                        // Update last position after reading all lines, while stream is still open
                        lastPositions[logFilePath] = fileStream.Position;
                    }
                }
            }
            catch (FileNotFoundException ex)
            {
                LogError($"Log file not found: {Path.GetFileName(logFilePath)} - {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                LogError($"Permission denied reading: {Path.GetFileName(logFilePath)} - {ex.Message}");
            }
            catch (Exception ex)
            {
                LogError($"Error processing {Path.GetFileName(logFilePath)}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static bool IsHeaderRow(string line)
        {
            return line.StartsWith('#');
        }

        private static void OnHotEotPacketReceived(object sender, HotEotPacketEventArgs e)
        {
            // Event subscribers will implement this method.
        }

        private static void OnDpuPacketReceived(object sender, DpuPacketEventArgs e)
        {
            // Event subscribers will implement this method.
        }

        private static void StartBeaconHealthServices(AppSettings appSettings)
        {
            var subscribersWithHealthService = appSettings.Subscribers
                .Where(s => !string.IsNullOrEmpty(s.HealthService))
                .ToList();

            if (subscribersWithHealthService.Count == 0)
            {
                LogError("No beacon health services in the configuration.");
                return;
            }

            foreach (var subscriber in subscribersWithHealthService)
            {
                StartBeaconHealthService(appSettings, subscriber.HealthService!, "Start");
            }
        }

        // A subscriber or health service that is misnamed in the settings, or that fails to start, is reported
        // and skipped. Before, it was skipped without a word, or it stopped every subscriber after it.

        private static void StartBeaconHealthService(AppSettings appsettings, string serviceTypeName, string methodName)
        {
            try
            {
                var type = Type.GetType(serviceTypeName);
                if (type == null)
                {
                    LogError($"Beacon health service type not found: {serviceTypeName}. Check the settings.");
                    return;
                }

                var service = Activator.CreateInstance(type, appsettings);
                var methodInfo = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
                if (service == null || methodInfo == null)
                {
                    LogError($"Method {methodName} not found in {serviceTypeName}.");
                    return;
                }

                methodInfo.Invoke(service, null);
            }
            catch (Exception ex)
            {
                var cause = ex is TargetInvocationException { InnerException: not null } ? ex.InnerException : ex;
                LogError($"Could not start beacon health service {serviceTypeName}: {cause.GetType().Name}: {cause.Message}");
            }
        }

        private static void SubscribeToPacketEvent(AppSettings appsettings, string subscriberTypeName, string eventName, string methodName)
        {
            try
            {
                var type = Type.GetType(subscriberTypeName);
                if (type == null)
                {
                    LogError($"Subscriber type not found: {subscriberTypeName}. Check the settings.");
                    return;
                }

                var subscriber = Activator.CreateInstance(type, appsettings);
                var eventInfo = typeof(SoftEOTLogService).GetEvent(eventName);
                var methodInfo = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
                if (subscriber == null || eventInfo?.EventHandlerType == null || methodInfo == null)
                {
                    LogError($"Method {methodName} not found in {subscriberTypeName}.");
                    return;
                }

                var handler = Delegate.CreateDelegate(eventInfo.EventHandlerType, subscriber, methodInfo);
                eventInfo.AddEventHandler(null, handler);
            }
            catch (Exception ex)
            {
                var cause = ex is TargetInvocationException { InnerException: not null } ? ex.InnerException : ex;
                LogError($"Could not start subscriber {subscriberTypeName}: {cause.GetType().Name}: {cause.Message}");
            }
        }

        /// <summary>
        /// Calls each handler of an event in turn. An exception in one handler is logged and the rest still run;
        /// a plain multicast call stops at the first exception, so one faulty subscriber hid packets from the
        /// subscribers after it.
        /// </summary>
        internal static void InvokeEachHandler<TArgs>(Delegate? handlers, TArgs args, Action<string>? logError = null)
        {
            if (handlers == null)
            {
                return;
            }

            foreach (var handler in handlers.GetInvocationList())
            {
                try
                {
                    handler.DynamicInvoke(null, args);
                }
                catch (Exception ex)
                {
                    var cause = ex is TargetInvocationException { InnerException: not null } ? ex.InnerException : ex;
                    (logError ?? LogError)($"{handler.Target?.GetType().FullName ?? handler.Method.Name} failed on a packet: {cause.GetType().Name}: {cause.Message}");
                }
            }
        }

        private static void ProcessLogLine(string line, string logFilePath)
        {
            if (Path.GetFileName(logFilePath).Contains(FILE_NAME_PREFIX_HOT, StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(logFilePath).Contains(FILE_NAME_PREFIX_EOT, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var hotPacket = HotEotDeserializer.Deserialize(line);

                    // Only apply time filter if offset is configured; offset=0 means accept all packets
                    if (TIME_RECEIVED_OFFSET_MINUTES > 0 && hotPacket.TimeReceived.ToLocalTime() <= DateTime.Now.AddMinutes(-TIME_RECEIVED_OFFSET_MINUTES))
                    {
                        return; // Ignore packets older than TIME_RECEIVED_OFFSET_MINUTES minutes
                    }

                    Console.WriteLine(line);

                    InvokeEachHandler(HotEotPacketReceived, new HotEotPacketEventArgs(hotPacket));
                }
                catch (Exception ex)
                {
                    LogError($"Error deserializing HOT/EOT packet from {Path.GetFileName(logFilePath)}: {ex.GetType().Name}: {ex.Message}");
                }
            }
            else if (Path.GetFileName(logFilePath).StartsWith(FILE_NAME_PREFIX_DPU, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var dpuPacket = DpuDeserializer.Deserialize(line);

                    // Only apply time filter if offset is configured; offset=0 means accept all packets
                    if (TIME_RECEIVED_OFFSET_MINUTES > 0 && dpuPacket.TimeReceived.ToLocalTime() <= DateTime.Now.AddMinutes(-TIME_RECEIVED_OFFSET_MINUTES))
                    {
                        return; // Ignore packets older than TIME_RECEIVED_OFFSET_MINUTES minutes
                    }

                    Console.WriteLine(line);

                    InvokeEachHandler(DpuPacketReceived, new DpuPacketEventArgs(dpuPacket));
                }
                catch (Exception ex)
                {
                    LogError($"Error deserializing DPU packet from {Path.GetFileName(logFilePath)}: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        private static void LogError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERROR] {message}");
            Console.ResetColor();
        }
    }
}
