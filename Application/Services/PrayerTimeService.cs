using Application.DTOs;
using Application.Interfaces;
using AutoMapper;
using Domain.Models;
using Domain.Repositories;

namespace Application.Services
{
    public class PrayerTimeService : IPrayerTimeService
    {
        private readonly ICityPrayerTimesRepository _repository;
        private readonly IMapper _mapper;

        public PrayerTimeService(ICityPrayerTimesRepository repository, IMapper mapper)
        {
            _repository = repository;
            _mapper = mapper;
        }

        public async Task<CityPrayerTimesDTO> FetchAndCachePrayerTimesAsync(string city)
        {
            var cityPrayerTimes = await _repository.GetByCityAsync(city);
            return _mapper.Map<CityPrayerTimesDTO>(cityPrayerTimes);
        }

        public async Task AddOrUpdatePrayerTimesAsync(CityPrayerTimesDTO cityPrayerTimesDTO)
        {
            var cityPrayerTimes = _mapper.Map<CityPrayerTimes>(cityPrayerTimesDTO);
            await _repository.AddOrUpdateAsync(cityPrayerTimes);
        }
    }
}
