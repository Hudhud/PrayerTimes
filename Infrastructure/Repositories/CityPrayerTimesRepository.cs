using Domain.Models;
using Domain.Repositories;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public sealed class CityPrayerTimesRepository(ApplicationDbContext context) : ICityPrayerTimesRepository
{
    public async Task<IReadOnlyList<DailyPrayerTimes>> GetDaysAsync(string city, DateOnly from, DateOnly until,
        CancellationToken cancellationToken)
    {
        var start = from.ToDateTime(TimeOnly.MinValue);
        var end = until.ToDateTime(TimeOnly.MinValue);

        return await context.DailyPrayerTimes.AsNoTracking()
            .Where(day => day.CityPrayerTimes.City == city && day.Date >= start && day.Date < end)
            .OrderBy(day => day.Date)
            .ToListAsync(cancellationToken);
    }

    public async Task ReplaceMonthAsync(string city, DateOnly month, IReadOnlyList<DailyPrayerTimes> days,
        CancellationToken cancellationToken)
    {
        var start = month.ToDateTime(TimeOnly.MinValue);
        var end = month.AddMonths(1).ToDateTime(TimeOnly.MinValue);

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        // The unique city row serializes writes even when two app instances overlap during a deployment.
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO CityPrayerTimes (City) VALUES ({city}) ON DUPLICATE KEY UPDATE Id = Id",
            cancellationToken);

        var cityId = await context.CityPrayerTimes.Where(row => row.City == city)
            .Select(row => row.Id).SingleAsync(cancellationToken);

        await context.DailyPrayerTimes
            .Where(day => day.CityPrayerTimesId == cityId && day.Date >= start && day.Date < end)
            .ExecuteDeleteAsync(cancellationToken);

        foreach (var day in days)
        {
            day.CityPrayerTimesId = cityId;
        }

        context.DailyPrayerTimes.AddRange(days);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
