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

            CreateMap<CreateBeaconRailroadDTO, BeaconRailroad>()
                .ForMember(dest => dest.Beacon, opt => opt.Ignore())
                .ForMember(dest => dest.Subdivision, opt => opt.Ignore());
            CreateMap<UpdateBeaconRailroadDTO, BeaconRailroad>()
                .ForMember(dest => dest.Beacon, opt => opt.Ignore())
                .ForMember(dest => dest.Subdivision, opt => opt.Ignore());

            CreateMap<BeaconRailroad, BeaconRailroadDTO>()
                .ForMember(dest => dest.BeaconName,
                           opt => opt.MapFrom(src => src.Beacon.Name))
                .ForMember(dest => dest.RailroadID,
                           opt => opt.MapFrom(src => src.Subdivision.Railroad.ID))
                .ForMember(dest => dest.RailroadName,
                           opt => opt.MapFrom(src => src.Subdivision.Railroad.Name))
                .ForMember(dest => dest.SubdivisionID,
                           opt => opt.MapFrom(src => src.Subdivision.ID))
                .ForMember(dest => dest.SubdivisionName,
                           opt => opt.MapFrom(src => src.Subdivision.Name));

            CreateMap<MapPin, MapPinDTO>()
                .ForMember(dest => dest.BeaconID,
                           opt => opt.MapFrom(src => src.BeaconID))
                .ForMember(dest => dest.BeaconName,
                           opt => opt.MapFrom(src => src.BeaconRailroad.Beacon.Name))
                .ForMember(dest => dest.Direction,
                           opt => opt.MapFrom(src => src.Direction))
                .ForMember(dest => dest.Latitude,
                           opt => opt.MapFrom(src => src.BeaconRailroad.Latitude))
                .ForMember(dest => dest.Longitude,
                           opt => opt.MapFrom(src => src.BeaconRailroad.Longitude))
                .ForMember(dest => dest.Milepost,
                           opt => opt.MapFrom(src => src.BeaconRailroad.Milepost))
                .ForMember(dest => dest.Railroad,
                           opt => opt.MapFrom(src => src.BeaconRailroad.Subdivision.Railroad.Name))
                .ForMember(dest => dest.Subdivision,
                           opt => opt.MapFrom(src => src.BeaconRailroad.Subdivision.Name))
                .ForMember(dest => dest.SubdivisionID,
                           opt => opt.MapFrom(src => src.BeaconRailroad.Subdivision.ID))
                .ForMember(dest => dest.Addresses,
                           opt => opt.MapFrom(src => src.Addresses != null
                               ? src.Addresses.Select(a => new AddressDTO
                               {
                                   Source = a.Source,
                                   AddressID = a.AddressID
                               }).ToList()
                               : new List<AddressDTO>()));

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
                .ForPath(dest => dest.BeaconID, opt => opt.MapFrom(src => src.Beacon.ID));

            CreateMap<Telemetry, MapPin>();
        }
    }
}


