namespace Services.RemoteCommands
{
    /// <summary>
    /// A command from the Server Health page, received in a beacon health check response.
    /// </summary>
    public class RemoteCommand
    {
        public int ID { get; set; }

        /// <summary>
        /// "Reboot" or "RestartApps".
        /// </summary>
        public string Type { get; set; } = string.Empty;

        public string RequestedBy { get; set; } = string.Empty;

        public DateTime ExpiresAt { get; set; }
    }

    /// <summary>
    /// Body of the health check response when a command is waiting.
    /// </summary>
    public class BeaconHealthResponse
    {
        public RemoteCommand? Command { get; set; }
    }

    public static class RemoteCommandTypes
    {
        public const string Reboot = "Reboot";
        public const string RestartApps = "RestartApps";
    }

    /// <summary>
    /// What the PC reports back. Matches the server's BeaconCommandReportStatus names.
    /// </summary>
    public static class RemoteCommandReportStatus
    {
        public const string Started = "Started";
        public const string Succeeded = "Succeeded";
        public const string Failed = "Failed";
        public const string Rejected = "Rejected";
    }

    /// <summary>
    /// The server's answer to a report.
    /// </summary>
    public enum RemoteCommandReportResult
    {
        Accepted,

        /// <summary>
        /// The server refused the report (409): the command was cancelled, expired or already handled. Do not run it.
        /// </summary>
        Refused
    }
}
