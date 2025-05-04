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
                .ForMember(dest => dest.Railroads,
                    opt => opt.MapFrom(src => src.RailroadIDs.Select(id => new Railroad { ID = id, Name = "dummy", Subdivision = "dummy" })));

            CreateMap<Beacon, BeaconDTO>();

            // Owner mappings
            CreateMap<CreateOwnerDTO, Owner>()
                .ForMember(dest => dest.ID, opt => opt.Ignore())
                .ForMember(dest => dest.Beacons, opt => opt.Ignore());

            CreateMap<Owner, OwnerDTO>();

            // Railroad mappings
            CreateMap<CreateRailroadDTO, Railroad>()
                .ForMember(dest => dest.ID, opt => opt.Ignore());

            CreateMap<Railroad, RailroadDTO>();

            // Telemetry mappings
            CreateMap<CreateTelemetryDTO, Telemetry>()
                .ForPath(dest => dest.Beacon.ID, opt => opt.MapFrom(src => src.BeaconID));

            CreateMap<Telemetry, MapAlert>()
                .ForMember(dest => dest.Latitude, opt => opt.MapFrom(src => src.Beacon.Latitude))
                .ForMember(dest => dest.Longitude, opt => opt.MapFrom(src => src.Beacon.Longitude));
        }
    }
}


