using Domain.Models;

namespace Domain.Services
{
    public interface IPrayerTimesService
    {
        Task<CityPrayerTimes> GetPrayerData(string city);
    }
}
