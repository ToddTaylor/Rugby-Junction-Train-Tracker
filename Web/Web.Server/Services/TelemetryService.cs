using Microsoft.AspNetCore.SignalR;
using Web.Server.Data;
using Web.Server.Entities;
using Web.Server.Hubs;

namespace Web.Server.Services
{
    public class TelemetryService : ITelemetryService
    {
        private TelemetryDbContext _dbContext;
        private readonly IHubContext<NotificationHub> _hubContext;
        private static readonly Random _random = new Random();

        public TelemetryService(
            IHubContext<NotificationHub> hubContext,
            TelemetryDbContext dbContext)
        {
            _dbContext = dbContext;
            _hubContext = hubContext;
        }

        public Task<IEnumerable<Telemetry>> GetTelemetries()
        {
            throw new NotImplementedException();
        }

        public async void CreateTelemetry(Telemetry telemetry)
        {
            // Look-up existing alerts based on Address ID and most recent timestamp.  
            var existingTelemetry = _dbContext.Telemetries
                .OrderByDescending(x => x.Timestamp)
                .FirstOrDefault();

            var beaconIsMultiRailroad = existingTelemetry?.Beacon.Railroads.Count > 1;

            if (existingTelemetry == null && beaconIsMultiRailroad)
            {
                // There's no way to know which railroad the alert belongs to yet.
                return;
            }

            // Insert telemetry into the database which will get the ID assigned.
            _dbContext.Set<Telemetry>().Add(telemetry);

            // Update Beacon timestamp to let the system know the beacon is still alive.
            var beacon = _dbContext.Beacons.FirstOrDefault(b => b.ID == telemetry.Beacon.ID);
            if (beacon == null)
            {
                // Handle not found (e.g., log or throw an exception)
                throw new InvalidOperationException("Beacon not found.");
            }

            // Update only the Timestamp
            beacon.Timestamp = DateTime.UtcNow;

            _dbContext.SaveChanges();

            // Calculate direction (N, S, E, W, etc.) based on the last alert's location.
            var fromGeoCoordinate = new GeoCoordinate(existingTelemetry.Beacon.Latitude, existingTelemetry.Beacon.Longitude);
            var toGeoCoordinate = new GeoCoordinate(telemetry.Beacon.Latitude, telemetry.Beacon.Longitude);

            // TODO: Getting direction from difference between two beacons can be misleading. For example,
            // Waukesha is SW of Rugby, therefore the resulting direction of the train will be NE which is odd.
            var direction = GetDirection(fromGeoCoordinate, toGeoCoordinate);

            // Add direction to outgoing alert.
            var mapAlert = _mapper.Map<MapAlert>(telemetry);
            mapAlert.Direction = direction;

            // Send real-time notification
            await _hubContext.Clients.All.SendAsync("MapAlert", mapAlert);
        }

        //private static string GetDirection(GeoCoordinate from, GeoCoordinate to)
        //{
        //    string latDirection = "";
        //    string lonDirection = "";

        //    if (to.Latitude > from.Latitude)
        //        latDirection = "N";
        //    else if (to.Latitude < from.Latitude)
        //        latDirection = "S";

        //    if (to.Longitude > from.Longitude)
        //        lonDirection = "E";
        //    else if (to.Longitude < from.Longitude)
        //        lonDirection = "W";

        //    return latDirection + lonDirection;
        //}

        private static string GetDirection(GeoCoordinate from, GeoCoordinate to)
        {
            double latDiff = to.Latitude - from.Latitude;
            double lonDiff = to.Longitude - from.Longitude;

            if (Math.Abs(latDiff) > Math.Abs(lonDiff))
            {
                return latDiff > 0 ? "N" : "S";
            }
            else
            {
                return lonDiff > 0 ? "E" : "W";
            }
        }
        private MapAlert GetRandomObject()
        {
            var array = new MapAlert[]
            {
                new() {
                    AddressID = 329042,
                    Direction = "N",
                    Latitude = 43.162032,
                    Longitude = -88.200269,
                    Moving = true,
                    Source = "HOT",
                    Timestamp = DateTime.UtcNow
                },
                new() {
                    AddressID = 902342,
                    Direction = "S",
                    Latitude = 43.336070,
                    Longitude = -88.290659,
                    Moving = false,
                    Source = "EOT",
                    Timestamp = DateTime.UtcNow
                },
                new() {
                    AddressID = 903242,
                    Direction = "E",
                    Latitude = 43.419284,
                    Longitude = -88.340736,
                    Moving = false,
                    Source = "DPU",
                    Timestamp = DateTime.UtcNow
                },
                new() {
                    AddressID = 093242,
                    Direction = "W",
                    Latitude = 42.960318,
                    Longitude = -88.240389,
                    Moving = false,
                    Source = "DPU",
                    Timestamp = DateTime.UtcNow
                }
            };

            int index = _random.Next(array.Length);
            return array[index];
        }

    }
}
