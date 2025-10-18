using Web.Server.Enums;

namespace Web.Server.DTOs
{
    public class CreateBeaconRailroadDTO : IEquatable<CreateBeaconRailroadDTO?>
    {
        /// <summary>
        /// The beacon unique identifier.
        /// </summary>
        public int BeaconID { get; set; }

        /// <summary>
        /// The railroad subdivision unique identifier associated with the beacon.  I.e., which railroad subdivision the beacon monitors.
        /// </summary>
        public int SubdivisionID { get; set; }

        /// <summary>
        /// The latitude coordinate of the beacon.
        /// </summary>
        /// <example>43.294944</example>
        public double Latitude { get; set; }

        /// <summary>
        /// The longitude coordinate of the beacon.
        /// </summary>
        /// <example>-88.253118</example>
        public double Longitude { get; set; }

        /// <summary>
        /// Approximate railroad milepost closest to the beacon
        /// </summary>
        /// <remarks>
        /// One decimal place of precision for milepost values.
        /// </remarks>
        public double Milepost { get; set; }

        /// <summary>
        /// Indicates if the beacon is in proximity to multiple tracks at the same 
        /// location for the same railroad.
        /// </summary>
        public bool MultipleTracks { get; set; }

        /// <summary>
        /// Reports the online status of the beacon. If the beacon's last update
        /// is older than 15 minutes, it is considered offline.
        /// </summary>
        public bool Online { get; set; }

        /// <summary>
        /// The direction in which telemetry data is moving.
        /// </summary>
        /// <example>NorthSouth</example>
        public Direction Direction { get; set; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as CreateBeaconRailroadDTO);
        }

        public bool Equals(CreateBeaconRailroadDTO? other)
        {
            return other is not null &&
                   BeaconID == other.BeaconID &&
                   SubdivisionID == other.SubdivisionID &&
                   Latitude == other.Latitude &&
                   Longitude == other.Longitude &&
                   Milepost == other.Milepost &&
                   MultipleTracks == other.MultipleTracks &&
                   Online == other.Online &&
                   Direction == other.Direction;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(BeaconID, SubdivisionID, Latitude, Longitude, Milepost, MultipleTracks, Online, Direction);
        }

        public static bool operator ==(CreateBeaconRailroadDTO? left, CreateBeaconRailroadDTO? right)
        {
            return EqualityComparer<CreateBeaconRailroadDTO>.Default.Equals(left, right);
        }

        public static bool operator !=(CreateBeaconRailroadDTO? left, CreateBeaconRailroadDTO? right)
        {
            return !(left == right);
        }
    }
}