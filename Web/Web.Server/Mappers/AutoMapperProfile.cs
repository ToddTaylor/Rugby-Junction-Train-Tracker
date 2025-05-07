using AutoMapper;
using Web.Server.DTOs;
using Web.Server.Entities;

namespace Web.Server.Mappers
{
    public class AutoMapperProfile : Profile
    {
        public AutoMapperProfile()
        {
            // Beacon mappings
            CreateMap<CreateBeaconDTO, Beacon>()
                .ForPath(dest => dest.Owner.ID, opt => opt.MapFrom(src => src.OwnerID))
                .ForMember(dest => dest.Owner, opt => opt.Ignore())
                .ForMember(dest => dest.BeaconRailroads, opt => opt.Ignore());
            // TODO: Added [Required] to entity properties as alternative to having to add dummy values here.
            CreateMap<Beacon, BeaconDTO>();

            // Owner mappings
            CreateMap<CreateOwnerDTO, Owner>()
                .ForMember(dest => dest.ID, opt => opt.Ignore())
                .ForMember(dest => dest.Beacons, opt => opt.Ignore());

            CreateMap<Owner, OwnerDTO>();

            CreateMap<Owner, UpdateOwnerDTO>();

            // Railroad mappings
            CreateMap<CreateRailroadDTO, Railroad>()
                .ForMember(dest => dest.ID, opt => opt.Ignore());

            CreateMap<Railroad, RailroadDTO>();

            // Telemetry mappings
            CreateMap<CreateTelemetryDTO, Telemetry>()
                .ForPath(dest => dest.Beacon.ID, opt => opt.MapFrom(src => src.BeaconID))
                .ReverseMap()
                .ForPath(dest => dest.BeaconID, opt => opt.MapFrom(src => src.Beacon.ID));

            CreateMap<Telemetry, TelemetryDTO>()
                .ForPath(dest => dest.Beacon.ID, opt => opt.MapFrom(src => src.Beacon.ID));

            CreateMap<Telemetry, MapAlert>()
                .ForMember(dest => dest.Latitude, opt => opt.MapFrom(src => src.Beacon.BeaconRailroads.First().Latitude))
                .ForMember(dest => dest.Longitude, opt => opt.MapFrom(src => src.Beacon.BeaconRailroads.First().Longitude));
        }
    }
}


