using Mapster;
using Web.Server.DTOs;
using Web.Server.Entities;

namespace Web.Server.Mappers
{
    public class MapsterProfile : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            config.NewConfig<CreateBeaconDTO, Beacon>()
                .Map(dest => dest.OwnerID, src => src.OwnerID)
                .Ignore(dest => dest.Owner)
                .Ignore(dest => dest.BeaconRailroads);

            config.NewConfig<UpdateBeaconDTO, Beacon>()
                .Map(dest => dest.OwnerID, src => src.OwnerID)
                .Ignore(dest => dest.Owner)
                .Ignore(dest => dest.BeaconRailroads);

            // TODO: Added [Required] to entity properties as alternative to having to add dummy values here.
            config.NewConfig<Beacon, BeaconDTO>();

            config.NewConfig<CreateBeaconRailroadDTO, BeaconRailroad>()
                .Ignore(dest => dest.Beacon)
                .Ignore(dest => dest.Subdivision);

            config.NewConfig<UpdateBeaconRailroadDTO, BeaconRailroad>()
                .Ignore(dest => dest.Beacon)
                .Ignore(dest => dest.Subdivision);

            config.NewConfig<BeaconRailroad, BeaconRailroadDTO>()
                .Map(dest => dest.BeaconName, src => src.Beacon != null ? src.Beacon.Name : string.Empty)
                .Map(dest => dest.RailroadID, src => src.Subdivision != null ? src.Subdivision.RailroadID : 0)
                .Map(dest => dest.RailroadName, src => src.Subdivision != null && src.Subdivision.Railroad != null ? src.Subdivision.Railroad.Name : string.Empty)
                .Map(dest => dest.SubdivisionID, src => src.SubdivisionID)
                .Map(dest => dest.SubdivisionName, src => src.Subdivision != null ? src.Subdivision.Name : string.Empty);

            config.NewConfig<MapPin, MapPinDTO>()
                .Map(dest => dest.BeaconID, src => src.BeaconID)
                .Map(dest => dest.ShareCode, src => src.ShareCode)
                .Map(dest => dest.BeaconName, src => src.BeaconRailroad.Beacon.Name)
                .Map(dest => dest.Direction, src => src.Direction)
                .Map(dest => dest.Latitude, src => src.BeaconRailroad.Latitude)
                .Map(dest => dest.Longitude, src => src.BeaconRailroad.Longitude)
                .Map(dest => dest.Milepost, src => src.BeaconRailroad.Milepost)
                .Map(dest => dest.Railroad, src => src.BeaconRailroad.Subdivision.Railroad.Name)
                .Map(dest => dest.Subdivision, src => src.BeaconRailroad.Subdivision.Name)
                .Map(dest => dest.SubdivisionID, src => src.BeaconRailroad.Subdivision.ID)
                .Map(dest => dest.Addresses, src => src.Addresses != null
                    && src.Addresses.Count != 0
                    ? src.Addresses.Select(a => new AddressDTO
                    {
                        Source = a.Source,
                        AddressID = a.AddressID,
                        IsActive = a.LastUpdate == src.Addresses.Max(x => x.LastUpdate)
                    }).ToList()
                    : new List<AddressDTO>());

            config.NewConfig<MapPin, MapPinLatestDTO>()
                .Map(dest => dest.SubdivisionID, src => src.SubdivisionId)
                .Map(dest => dest.Railroad, src => src.BeaconRailroad.Subdivision.Railroad.Name)
                .Map(dest => dest.Subdivision, src => src.BeaconRailroad.Subdivision.Name);

            config.NewConfig<CreateUserDTO, User>()
                .Ignore(dest => dest.ID)
                .Ignore(dest => dest.UserRoles);

            config.NewConfig<UpdateUserDTO, User>()
                .Ignore(dest => dest.UserRoles);

            config.NewConfig<User, UserDTO>()
                .Map(dest => dest.Roles, src => src.UserRoles != null
                    ? src.UserRoles.Select(ur => ur.Role.RoleName).ToList()
                    : new List<string>())
                .Map(dest => dest.LastActive, src => src.LastActive);

            config.NewConfig<CreateRailroadDTO, Railroad>()
                .Ignore(dest => dest.ID);

            config.NewConfig<UpdateRailroadDTO, Railroad>();

            config.NewConfig<Railroad, RailroadDTO>();

            config.NewConfig<CreateSubdivisionDTO, Subdivision>()
                .Ignore(dest => dest.ID)
                .Ignore(dest => dest.Railroad)
                .Map(dest => dest.CustodianId, src => src.CustodianId);

            config.NewConfig<UpdateSubdivisionDTO, Subdivision>()
                .Ignore(dest => dest.Railroad)
                .Map(dest => dest.CustodianId, src => src.CustodianId);

            config.NewConfig<Subdivision, SubdivisionDTO>()
                .Map(dest => dest.Railroad, src => src.Railroad.Name)
                .Map(dest => dest.CustodianId, src => src.CustodianId);

            config.NewConfig<CreateTelemetryDTO, Telemetry>()
                .Map(dest => dest.BeaconID, src => src.BeaconID)
                .Map(dest => dest.CreatedAt, src => src.Timestamp)
                .Map(dest => dest.LastUpdate, src => src.Timestamp);

            config.NewConfig<Telemetry, CreateTelemetryDTO>()
                .Map(dest => dest.BeaconID, src => src.Beacon.ID)
                .Map(dest => dest.Timestamp, src => src.CreatedAt);

            config.NewConfig<Telemetry, TelemetryDTO>()
                .Map(dest => dest.BeaconID, src => src.Beacon.ID)
                .Map(dest => dest.BeaconName, src => src.Beacon.Name);

            config.NewConfig<Telemetry, MapPin>();

            config.NewConfig<UserTrackedPin, UserTrackedPinDTO>();
            config.NewConfig<UserTrackedPinDTO, UserTrackedPin>();
        }
    }
}


