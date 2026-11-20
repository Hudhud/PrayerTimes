using Application.Calendars;
using Domain.Models;

namespace Application.Interfaces;

public interface IPrayerTimeService
{
    Task<IReadOnlyList<DailyPrayerTimes>> GetDaysAsync(PrayerCity city, DateOnly from, DateOnly until,
        CancellationToken cancellationToken);

    Task RefreshMonthIfNeededAsync(PrayerCity city, DateOnly month, CancellationToken cancellationToken);
}
