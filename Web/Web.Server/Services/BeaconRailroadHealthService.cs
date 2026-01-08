using AutoMapper;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Web.Server.Data;
using Web.Server.DTOs;
using Web.Server.Hubs;

namespace Web.Server.Services
{
    public class BeaconRailroadHealthService : BackgroundService
    {
        private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(1);

        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly IMapper _mapper;
        private readonly IServiceScopeFactory _scopeFactory;

        public BeaconRailroadHealthService(
            IHubContext<NotificationHub> hubContext,
            IMapper mapper,
            IServiceScopeFactory scopeFactory)
        {
            _hubContext = hubContext;
            _mapper = mapper;
            _scopeFactory = scopeFactory;
        }

        /// <summary>
        /// Executes the background service to deliver updated beacon data to the UI every 15 minutes.
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _scopeFactory.CreateScope();

                var dbContext = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();

                var cutoff = DateTime.UtcNow.AddMinutes(-15);

                var beaconRailroads = dbContext.BeaconRailroads
                    .Include(br => br.Beacon)
                    .Include(br => br.Subdivision)
                        .ThenInclude(s => s.Railroad)
                    .ToList();

                var beaconRailroadDTOs = _mapper.Map<IEnumerable<BeaconRailroadDTO>>(beaconRailroads);

                var updatedBeacons = new List<BeaconRailroadDTO>();

                foreach (var beaconRailroadDTO in beaconRailroadDTOs)
                {
                    var isOffline = beaconRailroadDTO.LastUpdate != default && beaconRailroadDTO.LastUpdate <= cutoff;

                    beaconRailroadDTO.Online = !isOffline;

                    updatedBeacons.Add(beaconRailroadDTO);
                }

                // Send all beacons as a single batch to match frontend expectation of Beacon[]
                if (updatedBeacons.Any())
                {
                    await _hubContext.Clients.All.SendAsync(NotificationMethods.BeaconUpdate, updatedBeacons, cancellationToken: stoppingToken);
                }

                await Task.Delay(_cleanupInterval, stoppingToken);
            }
        }
    }
}
