using Application.DTOs;

namespace Application.Interfaces
{
    public interface IPrayerTimeService
    {
        Task<CityPrayerTimesDTO> FetchAndCachePrayerTimesAsync(string city);
        Task AddOrUpdatePrayerTimesAsync(CityPrayerTimesDTO cityPrayerTimesDTO);
    }
}
