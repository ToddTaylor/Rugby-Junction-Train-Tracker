using Services.Models;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Services.Diagnostics
{
    public class WindowsSystemProbe : ISystemProbe
    {
        public string? Hostname => Environment.MachineName;

        public string? OsVersion => RuntimeInformation.OSDescription;

        public string? ClientVersion =>
            // ClickOnce installs set this variable to the published version; fall back to the assembly version otherwise.
            Environment.GetEnvironmentVariable("ClickOnce_CurrentVersion")
            ?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString();

        public DateTime? GetLastBootUtc(DateTime utcNow)
        {
            return utcNow.AddMilliseconds(-Environment.TickCount64);
        }

        public long? GetFreeBytes(string path)
        {
            try
            {
                var root = Path.GetPathRoot(path);
                return string.IsNullOrEmpty(root) ? null : new DriveInfo(root).AvailableFreeSpace;
            }
            catch
            {
                return null;
            }
        }

        public IReadOnlyList<bool> GetProcessRespondingStates(string processName)
        {
            var prefix = processName.EndsWith('*') ? processName[..^1] : null;
            var processes = prefix == null ? Process.GetProcessesByName(processName) : Process.GetProcesses();

            var states = new List<bool>();
            foreach (var process in processes)
            {
                using (process)
                {
                    if (prefix != null && !process.ProcessName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    try
                    {
                        states.Add(process.Responding);
                    }
                    catch
                    {
                        // The process exited or cannot be inspected; count it as running.
                        states.Add(true);
                    }
                }
            }
            return states;
        }

        public DateTime? GetNewestFileWriteUtc(string directory, string fileNamePrefix)
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                return null;
            }

            var newest = Directory.GetFiles(directory, "*.log")
                .Where(f => Path.GetFileName(f).StartsWith(fileNamePrefix, StringComparison.OrdinalIgnoreCase))
                .Select(File.GetLastWriteTimeUtc)
                .DefaultIfEmpty()
                .Max();

            return newest == default ? null : DateTime.SpecifyKind(newest, DateTimeKind.Utc);
        }

        public bool FileExists(string path) => File.Exists(path);

        public ScheduledTaskStatus GetScheduledTask(string taskName)
        {
            try
            {
                var startInfo = new ProcessStartInfo("schtasks.exe")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                startInfo.ArgumentList.Add("/query");
                startInfo.ArgumentList.Add("/tn");
                startInfo.ArgumentList.Add(taskName);
                startInfo.ArgumentList.Add("/v");
                startInfo.ArgumentList.Add("/fo");
                startInfo.ArgumentList.Add("csv");

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    return new ScheduledTaskStatus { Name = taskName };
                }

                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(10000);

                return process.ExitCode == 0
                    ? ScheduledTaskCsvParser.Parse(taskName, output)
                    : new ScheduledTaskStatus { Name = taskName };
            }
            catch
            {
                return new ScheduledTaskStatus { Name = taskName };
            }
        }
    }
}
