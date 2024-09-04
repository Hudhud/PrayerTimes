using Application.DTOs;
using AutoMapper;
using Domain.Models;

namespace Application.Mapping
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<CityPrayerTimes, CityPrayerTimesDTO>().ReverseMap();
            CreateMap<DailyPrayerTimes, DailyPrayerTimesDTO>().ReverseMap();
        }
    }
}
