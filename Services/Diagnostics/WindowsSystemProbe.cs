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
            var states = new List<bool>();
            foreach (var process in GetMatchingProcesses(processName))
            {
                using (process)
                {
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

        public IReadOnlyList<bool> GetWindowRespondingStates(string processName, string titleText)
        {
            var processIds = new HashSet<uint>();
            foreach (var process in GetMatchingProcesses(processName))
            {
                using (process)
                {
                    processIds.Add((uint)process.Id);
                }
            }

            var states = new List<bool>();
            if (processIds.Count == 0 || !OperatingSystem.IsWindows())
            {
                return states;
            }

            // Reading another program's window titles works even when that program runs as administrator
            // and the ConsoleApp does not.
            NativeMethods.EnumWindows((hWnd, _) =>
            {
                NativeMethods.GetWindowThreadProcessId(hWnd, out var processId);
                if (processIds.Contains(processId) && NativeMethods.IsWindowVisible(hWnd))
                {
                    var title = GetWindowTitle(hWnd);
                    if (title.Contains(titleText, StringComparison.OrdinalIgnoreCase))
                    {
                        states.Add(!NativeMethods.IsHungAppWindow(hWnd));
                    }
                }
                return true;
            }, IntPtr.Zero);

            return states;
        }

        /// <summary>
        /// Processes named processName; a trailing "*" matches every process whose name starts with the text before it.
        /// </summary>
        private static IEnumerable<Process> GetMatchingProcesses(string processName)
        {
            if (!processName.EndsWith('*'))
            {
                return Process.GetProcessesByName(processName);
            }

            var prefix = processName[..^1];
            var matches = new List<Process>();
            foreach (var process in Process.GetProcesses())
            {
                if (process.ProcessName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(process);
                }
                else
                {
                    process.Dispose();
                }
            }
            return matches;
        }

        private static string GetWindowTitle(IntPtr hWnd)
        {
            var length = NativeMethods.GetWindowTextLength(hWnd);
            if (length <= 0)
            {
                return string.Empty;
            }

            var buffer = new char[length + 1];
            var copied = NativeMethods.GetWindowText(hWnd, buffer, buffer.Length);
            return new string(buffer, 0, copied);
        }

        private static class NativeMethods
        {
            public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

            [DllImport("user32.dll")]
            public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool IsWindowVisible(IntPtr hWnd);

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool IsHungAppWindow(IntPtr hWnd);

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            public static extern int GetWindowTextLength(IntPtr hWnd);

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            public static extern int GetWindowText(IntPtr hWnd, [Out] char[] lpString, int nMaxCount);
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
