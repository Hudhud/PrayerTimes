using System.Globalization;
using Application.Calendars;
using Calculation;
using Domain.Models;
using Xunit;

namespace PrayerTimes.Tests;

public sealed class PrayerCalendarTests
{
    private readonly DanishPrayerTimeCalculator calculator = new();

    [Theory]
    [InlineData("2026-11-30T23:30:00Z", "2026-12-01 00:30 +01:00")]
    [InlineData("2026-03-29T00:30:00Z", "2026-03-29 01:30 +01:00")]
    [InlineData("2026-03-29T01:30:00Z", "2026-03-29 03:30 +02:00")]
    [InlineData("2026-10-25T00:30:00Z", "2026-10-25 02:30 +02:00")]
    [InlineData("2026-10-25T01:30:00Z", "2026-10-25 02:30 +01:00")]
    public void ClockUsesDanishTime(string utc, string expected)
    {
        var clock = new DanishTime(new TestClock(DateTimeOffset.Parse(utc, CultureInfo.InvariantCulture)));

        Assert.Equal(expected, clock.Now.ToString("yyyy-MM-dd HH:mm zzz", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void NextPrayerDoesNotSkipFajrOrIshaWhenTheirTimeIsUnavailable()
    {
        var date = new DateOnly(2026, 7, 1);
        var days = Around(date);

        Assert.Null(PrayerDay.FindNextPrayer(days, date, DanishTime.At(date, new TimeOnly(0, 15))));
        Assert.Equal("Dhuhr", PrayerDay.FindNextPrayer(days, date, DanishTime.At(date, new TimeOnly(8, 0)))?.Name);
        Assert.Equal("Maghrib", PrayerDay.FindNextPrayer(days, date, DanishTime.At(date, new TimeOnly(18, 0)))?.Name);
        Assert.Null(PrayerDay.FindNextPrayer(days, date, DanishTime.At(date, new TimeOnly(23, 0))));
    }

    [Fact]
    public void NextPrayerUsesIshaAfterMidnightBeforeTodaysFajr()
    {
        var date = new DateOnly(2026, 5, 4);
        var days = Around(date);

        var isha = PrayerDay.FindNextPrayer(days, date, DanishTime.At(date, TimeOnly.MinValue));
        Assert.Equal("Isha", isha?.Name);
        Assert.Equal(DanishTime.At(date, new TimeOnly(0, 1)), isha?.StartsAt);

        var fajr = PrayerDay.FindNextPrayer(days, date, DanishTime.At(date, new TimeOnly(0, 2)));
        Assert.Equal("Fajr", fajr?.Name);
    }

    [Fact]
    public void NextPrayerFindsFajrAcrossTheMonthBoundary()
    {
        var date = new DateOnly(2026, 11, 30);
        var days = Around(date);

        var next = PrayerDay.FindNextPrayer(days, date, DanishTime.At(date, new TimeOnly(23, 0)));

        Assert.Equal("Fajr", next?.Name);
        Assert.Equal(new DateOnly(2026, 12, 1), DateOnly.FromDateTime(next!.StartsAt!.Value.Date));
    }

    [Fact]
    public void CurrentPrayerEndsFajrAtSunriseAndFollowsTheAsrMethod()
    {
        var date = new DateOnly(2026, 11, 23);
        var days = Around(date);
        var prayers = PrayerDay.Events(days[date]);

        Assert.Equal("Fajr", PrayerDay.FindCurrentPrayer(days, date, prayers[0].StartsAt!.Value)?.Name);
        Assert.Null(PrayerDay.FindCurrentPrayer(days, date, PrayerDay.Sunrise(days[date])));

        var standardAsr = prayers[2].StartsAt!.Value;
        Assert.Equal("Asr", PrayerDay.FindCurrentPrayer(days, date, standardAsr)?.Name);
        Assert.Equal("Dhuhr", PrayerDay.FindCurrentPrayer(days, date, standardAsr, hanafi: true)?.Name);
    }

    [Fact]
    public void CurrentPrayerUsesThePreviousTimetableForOvernightIsha()
    {
        var date = new DateOnly(2026, 5, 4);
        var days = Around(date);

        Assert.Equal("Maghrib", PrayerDay.FindCurrentPrayer(days, date, DanishTime.At(date, TimeOnly.MinValue))?.Name);
        Assert.Equal("Isha", PrayerDay.FindCurrentPrayer(days, date, DanishTime.At(date, new TimeOnly(0, 1)))?.Name);
    }

    [Fact]
    public void CurrentPrayerDoesNotExtendMaghribAcrossAnUnknownIsha()
    {
        var date = new DateOnly(2026, 7, 1);
        var days = Around(date);

        Assert.Null(PrayerDay.FindCurrentPrayer(days, date, DanishTime.At(date, new TimeOnly(23, 0))));
    }

    private Dictionary<DateOnly, DailyPrayerTimes> Around(DateOnly date) => new[] { date.AddDays(-1), date, date.AddDays(1) }
        .ToDictionary(day => day, day => calculator.Calculate(day, 55.6759142, 12.5691285));

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
