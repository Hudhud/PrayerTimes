using Application.Calendars;
using Calculation;
using Xunit;

namespace PrayerTimes.Tests;

public sealed class WorldwidePrayerTimesTests
{
    private readonly DanishPrayerTimeCalculator calculator = new();

    [Fact]
    public void PrayerInstantsFollowTheSelectedCityThroughDaylightSaving()
    {
        const string zone = "America/New_York";
        var saturday = calculator.Calculate(new DateOnly(2026, 3, 7), 40.7128, -74.0060, timeZoneId: zone);
        var sunday = calculator.Calculate(new DateOnly(2026, 3, 8), 40.7128, -74.0060, timeZoneId: zone);

        var before = PrayerDay.Events(saturday).Single(prayer => prayer.Name == "Dhuhr").StartsAt!.Value;
        var after = PrayerDay.Events(sunday).Single(prayer => prayer.Name == "Dhuhr").StartsAt!.Value;

        Assert.Equal(TimeSpan.FromHours(-5), before.Offset);
        Assert.Equal(TimeSpan.FromHours(-4), after.Offset);
        Assert.Equal(7, before.Day);
        Assert.Equal(8, after.Day);
        Assert.Equal(12, before.Hour);
        Assert.Equal(13, after.Hour);
    }

    [Theory]
    [InlineData(6, 21)]
    [InlineData(12, 21)]
    public void PolarDaysKeepAvailablePrayersAndLeaveMissingSolarEventsBlank(int month, int day)
    {
        var date = new DateOnly(2026, month, day);
        var timetable = calculator.Calculate(date, 69.6492, 18.9553, timeZoneId: "Europe/Oslo");

        Assert.True(PrayerDay.IsUsable(timetable));
        Assert.True(timetable.HasUnavailableTimes);
        Assert.Null(PrayerDay.AvailableSunrise(timetable));
        var events = PrayerDay.Events(timetable);
        Assert.NotNull(events.Single(prayer => prayer.Name == "Dhuhr").StartsAt);
        Assert.Null(events.Single(prayer => prayer.Name == "Maghrib").StartsAt);
    }

    [Fact]
    public void TheSelectedLocationCanBeOnADifferentDate()
    {
        var instant = new DateTimeOffset(2026, 8, 30, 23, 30, 0, TimeSpan.Zero);
        var tokyo = LocalTime.FromUtc(instant, "Asia/Tokyo");
        var losAngeles = LocalTime.FromUtc(instant, "America/Los_Angeles");

        Assert.Equal(new DateOnly(2026, 8, 31), DateOnly.FromDateTime(tokyo.Date));
        Assert.Equal(new DateOnly(2026, 8, 30), DateOnly.FromDateTime(losAngeles.Date));
    }

    [Fact]
    public void ExistingAndWorldPlaceIndexesUseTheirOwnTimeZones()
    {
        var data = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Web/wwwroot/data"));
        var directory = PrayerPlaceDirectory.Load(Path.Combine(data, "prayer-places.json"),
            Path.Combine(data, "prayer-cities-world.json"));

        var tokyo = directory.Search("Tokyo").First(city => city.Name == "Tokyo").ToPrayerCity();
        Assert.Equal("Asia/Tokyo", tokyo.TimeZoneId);
        Assert.Equal("JP", tokyo.CountryCode);
        Assert.Equal(DanishTime.ZoneId, directory.Find("post-8000")!.TimeZoneId);
        Assert.Equal("Asia/Tokyo", directory.FindNearestCity(35.6762, 139.6503)!.TimeZoneId);
    }
}
