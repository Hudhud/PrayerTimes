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
    private readonly MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = 128 * 31 });
    private readonly object[] gates = Array.ConvertAll(new int[32], _ => new object());

    public Task<IReadOnlyList<DailyPrayerTimes>> GetDaysAsync(PrayerCity city, DateOnly from, DateOnly until,
        CancellationToken cancellationToken)
    {
        var result = new List<DailyPrayerTimes>();
        var latitude = double.Parse(city.Latitude, CultureInfo.InvariantCulture);
        var longitude = double.Parse(city.Longitude, CultureInfo.InvariantCulture);

        for (var date = from; date < until; date = date.AddDays(1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = (latitude, longitude, date);
            if (!cache.TryGetValue(key, out DailyPrayerTimes? day))
            {
                // An exact date is small enough to compute under a bounded set of locks.
                lock (gates[(uint)key.GetHashCode() % gates.Length])
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    day = cache.GetOrCreate(key, entry =>
                    {
                        entry.SetSize(1).SetSlidingExpiration(TimeSpan.FromDays(1));
                        return calculator.Calculate(date, latitude, longitude);
                    });
                }
            }

            result.Add(day!);
        }

        return Task.FromResult<IReadOnlyList<DailyPrayerTimes>>(result);
    }

    public void Dispose() => cache.Dispose();
}
