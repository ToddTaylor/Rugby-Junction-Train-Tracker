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

        /// <summary>
        /// Returns one entry per visible top-level window of the process whose title contains titleText
        /// (ignoring case): true when the window is responding. processName matches as in GetProcessRespondingStates.
        /// </summary>
        IReadOnlyList<bool> GetWindowRespondingStates(string processName, string titleText);

        DateTime? GetNewestFileWriteUtc(string directory, string fileNamePrefix);
        ScheduledTaskStatus GetScheduledTask(string taskName);
        bool FileExists(string path);
    }
}
