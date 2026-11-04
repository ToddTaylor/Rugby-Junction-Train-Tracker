using Services.Models;

namespace Services.Diagnostics
{
    /// <summary>
    /// Reads machine state for the beacon diagnostics report. Abstracted so the collector can be unit tested.
    /// </summary>
    public interface ISystemProbe
    {
        string? Hostname { get; }
        string? OsVersion { get; }
        string? ClientVersion { get; }
        DateTime? GetLastBootUtc(DateTime utcNow);
        long? GetFreeBytes(string path);

        /// <summary>
        /// Returns one entry per running instance of the process: true when it is responding.
        /// A trailing "*" in processName matches every process whose name starts with the text before it.
        /// </summary>
        IReadOnlyList<bool> GetProcessRespondingStates(string processName);

        DateTime? GetNewestFileWriteUtc(string directory, string fileNamePrefix);
        ScheduledTaskStatus GetScheduledTask(string taskName);
        bool FileExists(string path);
    }
}
