using AutoMapper;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Web.Server.Hubs;
using Web.Server.Models;

namespace Web.Server.Services
{
    public class TelemetryService : ITelemetryService
    {
        private DbContext _dbContext;
        private readonly IMapper _mapper;
        private readonly IHubContext<NotificationHub> _hubContext;
        private static readonly Random _random = new Random();

        public TelemetryService(IHubContext<NotificationHub> hubContext, IMapper mapper, DbContext dbContext)
        {
            _dbContext = dbContext;
            _hubContext = hubContext;
            _mapper = mapper;
        }

        public async void ProcessTelemetry(Alert alert)
        {
            // TODO: Temporary lines just for unit testing.
            //_dbContext.Set<Alert>().Add(alert);
            //_dbContext.SaveChanges();

            // Look-up existing alerts based on AlertID.  
            var existingAlert = GetRandomObject(); // Fake data.

            // Calculate direction (N, S, E, W, etc.) based on the last alert's location.
            var fromGeoCoordinate = new GeoCoordinate(existingAlert.Latitude, existingAlert.Longitude);
            var toGeoCoordinate = new GeoCoordinate(alert.Latitude, alert.Longitude);

            // TODO: Getting direction from difference between two beacons can be misleading. For example,
            // Waukesha is SW of Rugby, therefore the resulting direction of the train will be NE which is odd.
            var direction = GetDirection(fromGeoCoordinate, toGeoCoordinate);

            // Add direction to outgoing alert.
            var mapAlert = _mapper.Map<MapAlert>(alert);
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
        private Alert GetRandomObject()
        {
            var array = new Alert[]
            {
                new() {
                    AddressID = 329042,
                    BeaconID = "North Sussex",
                    Latitude = 43.162032,
                    Longitude = -88.200269,
                    Moving = true,
                    Source = "HOT",
                    Timestamp = DateTime.UtcNow
                },
                new() {
                    AddressID = 902342,
                    BeaconID = "Slinger",
                    Latitude = 43.336070,
                    Longitude = -88.290659,
                    Moving = false,
                    Source = "EOT",
                    Timestamp = DateTime.UtcNow
                },
                new() {
                    AddressID = 903242,
                    BeaconID = "Allenton",
                    Latitude = 43.419284,
                    Longitude = -88.340736,
                    Moving = false,
                    Source = "DPU",
                    Timestamp = DateTime.UtcNow
                },
                new() {
                    AddressID = 093242,
                    BeaconID = "Waukesha",
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
