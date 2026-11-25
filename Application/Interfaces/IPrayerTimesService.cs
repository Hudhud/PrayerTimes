using Application.Calendars;
using Domain.Models;

namespace Application.Interfaces;

public interface IPrayerTimeService
{
    Task<IReadOnlyList<DailyPrayerTimes>> GetDaysAsync(PrayerCity city, DateOnly from, DateOnly until,
        CancellationToken cancellationToken, TwilightAngles? angles = null);
}
