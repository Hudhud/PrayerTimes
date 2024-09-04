using Application.DTOs;

namespace Application.Interfaces
{
    public interface IPrayerTimeService
    {
        Task<CityPrayerTimesDTO> GetPrayerTimesAsync(string city);
        Task AddOrUpdatePrayerTimesAsync(CityPrayerTimesDTO cityPrayerTimesDTO);
    }
}
