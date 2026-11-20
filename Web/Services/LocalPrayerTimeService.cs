using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Application.Calendars;
using Application.Interfaces;
using Calculation;
using Domain.Models;
using Microsoft.Extensions.Caching.Memory;

namespace Web.Services;

public sealed class LocalPrayerTimeService(DanishPrayerTimeCalculator calculator) : IPrayerTimeService, IDisposable
{
    private readonly MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = 128 });

    public Task<IReadOnlyList<DailyPrayerTimes>> GetDaysAsync(PrayerCity city, DateOnly from, DateOnly until,
        CancellationToken cancellationToken)
    {
        var result = new List<DailyPrayerTimes>();
        var month = new DateOnly(from.Year, from.Month, 1);
        while (month < until)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var days = cache.GetOrCreate((city, month), entry =>
            {
                entry.SetSize(1).SetSlidingExpiration(TimeSpan.FromDays(1));
                return CalculateMonth(city, month, cancellationToken);
            })!;

            foreach (var day in days)
            {
                var date = DateOnly.FromDateTime(day.Date);
                if (date >= from && date < until)
                {
                    result.Add(day);
                }
            }

            month = month.AddMonths(1);
        }

        return Task.FromResult<IReadOnlyList<DailyPrayerTimes>>(result);
    }

    private DailyPrayerTimes[] CalculateMonth(PrayerCity city, DateOnly month, CancellationToken cancellationToken)
    {
        var latitude = double.Parse(city.Latitude, CultureInfo.InvariantCulture);
        var longitude = double.Parse(city.Longitude, CultureInfo.InvariantCulture);
        var days = new DailyPrayerTimes[DateTime.DaysInMonth(month.Year, month.Month)];
        for (var index = 0; index < days.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            days[index] = calculator.Calculate(month.AddDays(index), latitude, longitude);
        }

        return days;
    }

    public void Dispose() => cache.Dispose();
}
