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
        var date = new DateOnly(2026, 5, 5);
        var days = Around(date);
        var overnight = PrayerDay.Events(days[date.AddDays(-1)])[^1].StartsAt!.Value;

        var isha = PrayerDay.FindNextPrayer(days, date, DanishTime.At(date, TimeOnly.MinValue));
        Assert.Equal("Isha", isha?.Name);
        Assert.Equal(overnight, isha?.StartsAt);

        var fajr = PrayerDay.FindNextPrayer(days, date, overnight);
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
        var date = new DateOnly(2026, 5, 5);
        var days = Around(date);
        var overnight = PrayerDay.Events(days[date.AddDays(-1)])[^1].StartsAt!.Value;

        Assert.Equal("Maghrib", PrayerDay.FindCurrentPrayer(days, date, DanishTime.At(date, TimeOnly.MinValue))?.Name);
        Assert.Equal("Isha", PrayerDay.FindCurrentPrayer(days, date, overnight)?.Name);
    }

    [Theory]
    [InlineData(2026, 5, 5)]
    [InlineData(2026, 12, 1)]
    public void CurrentPrayerKeepsThePreviousDaysIshaUntilFajr(int year, int month, int day)
    {
        var date = new DateOnly(year, month, day);
        var days = Around(date);
        var previousIsha = PrayerDay.Events(days[date.AddDays(-1)])[^1];
        var fajr = PrayerDay.Events(days[date])[0];
        var midnight = DanishTime.At(date, TimeOnly.MinValue);
        var start = previousIsha.StartsAt!.Value > midnight ? previousIsha.StartsAt.Value : midnight;

        Assert.Equal(previousIsha, PrayerDay.FindCurrentPrayer(days, date, start));
        Assert.Equal(previousIsha, PrayerDay.FindCurrentPrayer(days, date, fajr.StartsAt!.Value.AddSeconds(-1)));
        Assert.Equal(fajr, PrayerDay.FindCurrentPrayer(days, date, fajr.StartsAt.Value));
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
