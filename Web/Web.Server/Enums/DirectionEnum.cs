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

    //public static class Directions
    //{
    //    public static readonly string[] NorthSouth = ["N", "S"];
    //    public static readonly string[] EastWest = ["E", "W"];
    //    public static readonly string[] NortheastSouthwest = ["NE", "SW"];
    //    public static readonly string[] NorthwestSoutheast = ["NW", "SE"];
    //    public static readonly string[] All = [.. NorthSouth, .. EastWest, .. NorthwestSoutheast, .. NorthwestSoutheast];
    //}
}
