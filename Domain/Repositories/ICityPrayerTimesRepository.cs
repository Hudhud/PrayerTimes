using Domain.Models;

namespace Domain.Repositories
{
    public interface ICityPrayerTimesRepository
    {
        Task<IEnumerable<CityPrayerTimes>> GetAllAsync();
        Task<CityPrayerTimes> GetByCityAsync(string city);
        Task AddAsync(CityPrayerTimes cityPrayerTimes);
        Task TruncateTablesAsync();
    }
}
