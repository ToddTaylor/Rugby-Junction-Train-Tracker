using System.Diagnostics;
using System.Globalization;

namespace Services.RemoteCommands
{
    /// <summary>
    /// Carries out remote commands on Windows. Abstracted so the handler can be unit tested.
    /// </summary>
    public interface IRemoteCommandRunner
    {
        DateTime? GetLastBootUtc(DateTime utcNow);

        bool FileExists(string path);

        /// <summary>
        /// Schedules a Windows restart. Throws when it cannot be scheduled.
        /// </summary>
        void ScheduleReboot(TimeSpan delay, string message);

        /// <summary>
        /// Runs the program and returns its exit code, or null if it did not finish within the timeout.
        /// </summary>
        Task<int?> RunAsync(string path, TimeSpan timeout);
    }

    public class WindowsRemoteCommandRunner : IRemoteCommandRunner
    {
        public DateTime? GetLastBootUtc(DateTime utcNow) => utcNow.AddMilliseconds(-Environment.TickCount64);

        public bool FileExists(string path) => File.Exists(path);

        public void ScheduleReboot(TimeSpan delay, string message)
        {
            // Windows limits the shutdown comment to 512 characters.
            var comment = message.Length > 500 ? message[..500] : message;

            var startInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.SystemDirectory, "shutdown.exe"),
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("/r");
            startInfo.ArgumentList.Add("/t");
            startInfo.ArgumentList.Add(((int)delay.TotalSeconds).ToString(CultureInfo.InvariantCulture));
            startInfo.ArgumentList.Add("/c");
            startInfo.ArgumentList.Add(comment);

            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("shutdown.exe did not start.");
            if (!process.WaitForExit(TimeSpan.FromSeconds(30)))
            {
                throw new InvalidOperationException("shutdown.exe did not finish.");
            }

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"shutdown.exe failed with exit code {process.ExitCode}.");
            }
        }

        public async Task<int?> RunAsync(string path, TimeSpan timeout)
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(path) ?? string.Empty,
            }) ?? throw new InvalidOperationException($"{path} did not start.");

            using var cancellation = new CancellationTokenSource(timeout);
            try
            {
                await process.WaitForExitAsync(cancellation.Token);
                return process.ExitCode;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }
    }
}
