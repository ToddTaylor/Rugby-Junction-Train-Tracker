using AutoMapper;
using Web.Server.Models;

namespace Web.Server.Mappers
{
    public class AutoMapperProfile : Profile
    {
        public AutoMapperProfile()
        {
            CreateMap<Telemetry, MapAlert>();
            //.ForMember(dest => dest.AddressID, opt => opt.MapFrom(src => src.AddressID));
        }
    }
}
