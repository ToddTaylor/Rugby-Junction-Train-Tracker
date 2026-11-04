using System.Text.Json.Serialization;

namespace Web.Server.Enums
{
    /// <summary>
    /// Health of a beacon PC as shown on the Server Health dashboard.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ServerStatus
    {
        /// <summary>
        /// Checking in and no problems found.
        /// </summary>
        Online,

        /// <summary>
        /// Checking in, but the diagnostics report shows a problem.
        /// </summary>
        Degraded,

        /// <summary>
        /// No health check received within the offline threshold.
        /// </summary>
        Offline
    }
}
