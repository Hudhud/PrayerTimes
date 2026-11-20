using Domain.Models;

namespace Domain.Repositories;

public interface ICityPrayerTimesRepository
{
    Task<IReadOnlyList<DailyPrayerTimes>> GetDaysAsync(string city, DateOnly from, DateOnly until,
        CancellationToken cancellationToken);

    Task ReplaceMonthAsync(string city, DateOnly month, IReadOnlyList<DailyPrayerTimes> days,
        CancellationToken cancellationToken);
}
