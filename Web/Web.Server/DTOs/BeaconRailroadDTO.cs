using Web.Server.Enums;

namespace Web.Server.DTOs
{
    public class BeaconRailroadDTO : IEquatable<BeaconRailroadDTO?>
    {
        /// <summary>
        /// The beacon unique identifier.
        /// </summary>
        public int BeaconID { get; set; }

        /// <summary>
        /// The railroad unique identifier associated with the beacon.  I.e., which railroad the beacon monitors.
        /// </summary>
        public int RailroadID { get; set; }

        /// <summary>
        /// The name of the railroad associated with the beacon.
        /// </summary>
        public string RailroadName { get; set; }

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

        /// <summary>
        /// The date and time when the beacon was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// The date and time when the beacon was last updated.
        /// </summary>
        public DateTime LastUpdate { get; set; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as BeaconRailroadDTO);
        }

        public bool Equals(BeaconRailroadDTO? other)
        {
            return other is not null &&
                   BeaconID == other.BeaconID &&
                   RailroadID == other.RailroadID &&
                   RailroadName == other.RailroadName &&
                   Latitude == other.Latitude &&
                   Longitude == other.Longitude &&
                   Milepost == other.Milepost &&
                   Online == other.Online &&
                   Direction == other.Direction &&
                   CreatedAt == other.CreatedAt &&
                   LastUpdate == other.LastUpdate;
        }

        public override int GetHashCode()
        {
            HashCode hash = new HashCode();
            hash.Add(BeaconID);
            hash.Add(RailroadID);
            hash.Add(RailroadName);
            hash.Add(Latitude);
            hash.Add(Longitude);
            hash.Add(Milepost);
            hash.Add(Online);
            hash.Add(Direction);
            hash.Add(CreatedAt);
            hash.Add(LastUpdate);
            return hash.ToHashCode();
        }

        public static bool operator ==(BeaconRailroadDTO? left, BeaconRailroadDTO? right)
        {
            return EqualityComparer<BeaconRailroadDTO>.Default.Equals(left, right);
        }

        public static bool operator !=(BeaconRailroadDTO? left, BeaconRailroadDTO? right)
        {
            return !(left == right);
        }
    }
}
