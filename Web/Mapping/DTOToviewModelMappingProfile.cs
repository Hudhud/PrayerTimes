using Application.DTOs;
using AutoMapper;
using Web.ViewModels;

namespace Web.Mapping
{
    public class DTOToviewModelMappingProfile : Profile
    {
        public DTOToviewModelMappingProfile()
        {
            CreateMap<CityPrayerTimesDTO, CityPrayerTimesViewModel>().ReverseMap();
            CreateMap<DailyPrayerTimesDTO, DailyPrayerTimesViewModel>().ReverseMap();
        }
    }
}
