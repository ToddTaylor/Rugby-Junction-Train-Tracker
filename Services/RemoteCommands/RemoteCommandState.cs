using System.Text.Json;

namespace Services.RemoteCommands
{
    /// <summary>
    /// What this PC remembers about remote commands, so it never runs the same command twice and can enforce its own
    /// reboot limit even if the server asks for more. Kept in %LOCALAPPDATA%\RugbyJunctionTrainTracker.
    /// </summary>
    public class RemoteCommandState
    {
        /// <summary>
        /// "{subscriber ID}:{command ID}" of each command already handled, with when it was handled (UTC).
        /// </summary>
        public Dictionary<string, DateTime> HandledCommands { get; set; } = new();

        /// <summary>
        /// When this PC started each remote reboot (UTC).
        /// </summary>
        public List<DateTime> RebootTimes { get; set; } = new();

        public static string CommandKey(string subscriberId, int commandId) => $"{subscriberId}:{commandId}";

        /// <summary>
        /// Drops entries older than the retention period so the file stays small.
        /// </summary>
        public void Prune(DateTime utcNow, TimeSpan retention)
        {
            var cutoff = utcNow - retention;
            foreach (var key in HandledCommands.Where(kv => kv.Value < cutoff).Select(kv => kv.Key).ToList())
            {
                HandledCommands.Remove(key);
            }

            RebootTimes.RemoveAll(t => t < cutoff);
        }
    }

    public interface IRemoteCommandStateStore
    {
        RemoteCommandState Load();
        void Save(RemoteCommandState state);
    }

    public class FileRemoteCommandStateStore : IRemoteCommandStateStore
    {
        private readonly string _path;

        public FileRemoteCommandStateStore(string path)
        {
            _path = path;
        }

        public static FileRemoteCommandStateStore CreateDefault() => new(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RugbyJunctionTrainTracker",
            "remote-commands.json"));

        public RemoteCommandState Load()
        {
            try
            {
                return File.Exists(_path)
                    ? JsonSerializer.Deserialize<RemoteCommandState>(File.ReadAllText(_path)) ?? new RemoteCommandState()
                    : new RemoteCommandState();
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                return new RemoteCommandState();
            }
        }

        public void Save(RemoteCommandState state)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
