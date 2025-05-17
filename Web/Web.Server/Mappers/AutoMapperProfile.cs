using AutoMapper;
using Web.Server.DTOs;
using Web.Server.Entities;

namespace Web.Server.Mappers
{
    public class AutoMapperProfile : Profile
    {
        public AutoMapperProfile()
        {
            CreateMap<CreateBeaconDTO, Beacon>()
                .ForPath(dest => dest.Owner.ID, opt => opt.MapFrom(src => src.OwnerID))
                .ForMember(dest => dest.Owner, opt => opt.Ignore())
                .ForMember(dest => dest.BeaconRailroads, opt => opt.Ignore());
            CreateMap<UpdateBeaconDTO, Beacon>()
                .ForPath(dest => dest.Owner.ID, opt => opt.MapFrom(src => src.OwnerID))
                .ForMember(dest => dest.Owner, opt => opt.Ignore())
                .ForMember(dest => dest.BeaconRailroads, opt => opt.Ignore());
            // TODO: Added [Required] to entity properties as alternative to having to add dummy values here.
            CreateMap<Beacon, BeaconDTO>();

            CreateMap<CreateBeaconRailroadDTO, BeaconRailroad>();
            CreateMap<UpdateBeaconRailroadDTO, BeaconRailroad>();
            CreateMap<BeaconRailroad, BeaconRailroadDTO>();

            CreateMap<MapPin, MapPinDTO>();

            CreateMap<CreateOwnerDTO, Owner>()
                .ForMember(dest => dest.ID, opt => opt.Ignore())
                .ForMember(dest => dest.Beacons, opt => opt.Ignore());
            CreateMap<UpdateOwnerDTO, Owner>();

            CreateMap<Owner, OwnerDTO>();
            CreateMap<Owner, UpdateOwnerDTO>();

            CreateMap<CreateRailroadDTO, Railroad>()
                .ForMember(dest => dest.ID, opt => opt.Ignore());
            CreateMap<UpdateRailroadDTO, Railroad>();

            CreateMap<Railroad, RailroadDTO>();

            CreateMap<CreateTelemetryDTO, Telemetry>()
                .ForPath(dest => dest.Beacon.ID, opt => opt.MapFrom(src => src.BeaconID))
                .ReverseMap()
                .ForPath(dest => dest.BeaconID, opt => opt.MapFrom(src => src.Beacon.ID));

            CreateMap<Telemetry, TelemetryDTO>()
                .ForPath(dest => dest.Beacon.ID, opt => opt.MapFrom(src => src.Beacon.ID));

            CreateMap<Telemetry, MapPin>()
                .ForMember(dest => dest.Latitude, opt => opt.MapFrom(src => src.Beacon.BeaconRailroads.First().Latitude))
                .ForMember(dest => dest.Longitude, opt => opt.MapFrom(src => src.Beacon.BeaconRailroads.First().Longitude));
        }
    }
}


