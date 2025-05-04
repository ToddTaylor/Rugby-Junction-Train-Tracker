using AutoMapper;
using Web.Server.DTOs;
using Web.Server.Entities;

namespace Web.Server.Mappers
{
    public class AutoMapperProfile : Profile
    {
        public AutoMapperProfile()
        {
            CreateMap<Telemetry, MapAlert>()
                .ForMember(dest => dest.Latitude, opt => opt.MapFrom(src => src.Beacon.Latitude))
                .ForMember(dest => dest.Longitude, opt => opt.MapFrom(src => src.Beacon.Longitude));

            CreateMap<CreateTelemetryDTO, Telemetry>()
                .ForPath(dest => dest.Beacon.ID, opt => opt.MapFrom(src => src.BeaconID));
        }
    }
}
