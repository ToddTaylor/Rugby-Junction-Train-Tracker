using System.Text.Json.Serialization;

namespace Web.Server.Enums
{
    /// <summary>
    /// An action a support user asks a beacon PC to carry out. The PC picks it up with its next health check.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum BeaconCommandType
    {
        /// <summary>
        /// Restart Windows. The startup tasks then start SDR#, SoftEOT, SoftDPU and the ConsoleApp again.
        /// </summary>
        Reboot,

        /// <summary>
        /// Run the PC's RestartAppsCommand (an AutoIt script that closes and reopens the radio programs).
        /// </summary>
        RestartApps
    }

    /// <summary>
    /// Where a command stands. Worked out from the command's timestamps; not stored.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum BeaconCommandStatus
    {
        /// <summary>
        /// Waiting for the PC to pick it up.
        /// </summary>
        Pending,

        /// <summary>
        /// The PC accepted it and is carrying it out. A reboot stays here until the PC checks in after restarting.
        /// </summary>
        Running,

        Succeeded,

        Failed,

        /// <summary>
        /// The PC refused it, for example because remote commands are turned off there or it restarted too recently.
        /// </summary>
        Rejected,

        Cancelled,

        /// <summary>
        /// The PC did not pick it up before it expired.
        /// </summary>
        Expired
    }

    /// <summary>
    /// What a beacon PC reports back about a command.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum BeaconCommandReportStatus
    {
        Started,
        Succeeded,
        Failed,
        Rejected
    }
}
