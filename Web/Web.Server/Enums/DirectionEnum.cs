using System.Text.Json.Serialization;

namespace Web.Server.Enums
{
    /// <summary>
    /// Represents the direction of telemetry data.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum Direction
    {
        /// <summary>
        /// All directions.
        /// </summary>
        All,

        /// <summary>
        /// North to South direction.
        /// </summary>
        NorthSouth,

        /// <summary>
        /// East to West direction.
        /// </summary>
        EastWest,

        /// <summary>
        /// Northeast to Southwest diagonal direction.
        /// </summary>
        NortheastSouthwest,

        /// <summary>
        /// Northwest to Southeast diagonal direction.
        /// </summary>
        NorthwestSoutheast
    }
}
