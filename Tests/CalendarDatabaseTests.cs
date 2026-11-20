using Domain.Models;
using Infrastructure.Data;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace PrayerTimes.Tests;

public sealed class CalendarDatabaseTests
{
    [MariaDbFact]
    public async Task ConcurrentRefreshAndFailedReplacementPreserveTheTimetable()
    {
        var connection = Environment.GetEnvironmentVariable("PRAYERTIMES_TEST_DATABASE")!;
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(connection, ServerVersion.AutoDetect(connection)).Options;
        await using var database = new ApplicationDbContext(options);
        await database.Database.MigrateAsync();
        var city = $"test-{Guid.NewGuid():N}";
        var previous = new DateOnly(2026, 6, 1);
        var month = previous.AddMonths(1);

        async Task ReplaceAsync(DateOnly date, string? asr)
        {
            await using var context = new ApplicationDbContext(options);
            await new CityPrayerTimesRepository(context).ReplaceMonthAsync(city, date,
                [Row(date, asr)], default);
        }

        try
        {
            await ReplaceAsync(previous, "17:30");

            await Task.WhenAll(ReplaceAsync(month, "17:31"), ReplaceAsync(month, "17:32"));
            var repository = new CityPrayerTimesRepository(database);
            var saved = await repository.GetDaysAsync(city, previous, month.AddMonths(1), default);
            Assert.Equal(2, saved.Count);

            await Assert.ThrowsAsync<DbUpdateException>(() => ReplaceAsync(month, null));
            var afterFailure = await repository.GetDaysAsync(city, previous, month.AddMonths(1), default);
            Assert.Equal(saved.Select(day => (day.Id, day.Date, day.AsrTime)),
                afterFailure.Select(day => (day.Id, day.Date, day.AsrTime)));
        }
        finally
        {
            await database.CityPrayerTimes.Where(row => row.City == city).ExecuteDeleteAsync();
        }
    }

    private static DailyPrayerTimes Row(DateOnly date, string? asr) => new()
    {
        Date = date.ToDateTime(TimeOnly.MinValue),
        FajrTime = "02:00",
        SunriseTime = "04:40",
        DhuhrTime = "13:10",
        AsrTime = asr!,
        AsrHanafiTime = "18:30",
        MaghribTime = "22:00",
        IshaTime = "00:41",
        IshaDayOffset = 1,
        UsesPredefinedFajrTime = true,
        UsesPredefinedIshaTime = true
    };

    private sealed class MariaDbFactAttribute : FactAttribute
    {
        public MariaDbFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PRAYERTIMES_TEST_DATABASE")))
            {
                Skip = "Set PRAYERTIMES_TEST_DATABASE to run the MariaDB integration test.";
            }
        }
    }
}
