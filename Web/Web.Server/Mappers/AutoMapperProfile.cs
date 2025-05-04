using AutoMapper;
using Web.Server.DTOs;
using Web.Server.Entities;

namespace Web.Server.Mappers
{
    public class AutoMapperProfile : Profile
    {
        public AutoMapperProfile()
        {
            CreateMap<Telemetry, MapAlert>();
            //.ForMember(dest => dest.AddressID, opt => opt.MapFrom(src => src.AddressID));

            CreateMap<CreateTelemetryDTO, Telemetry>()
                .ForMember(dest => dest.Beacon.ID, opt => opt.MapFrom(src => src.BeaconID));
        }
    }
}
