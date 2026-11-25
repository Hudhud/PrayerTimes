using System.Globalization;
using Application.Calendars;
using Domain.Models;

namespace Calculation;

public sealed class LocalPrayerSchedule(PrayerCity place, TwilightAngles? angles = null)
{
    private readonly DanishPrayerTimeCalculator calculator = new();
    private readonly Dictionary<DateOnly, DailyPrayerTimes> days = new();
    private readonly double latitude = double.Parse(place.Latitude, CultureInfo.InvariantCulture);
    private readonly double longitude = double.Parse(place.Longitude, CultureInfo.InvariantCulture);

    public DailyPrayerTimes Day(DateOnly date)
    {
        if (!days.TryGetValue(date, out var day))
        {
            day = calculator.Calculate(date, latitude, longitude, angles ?? TwilightAngles.ForPlace(place), place.TimeZoneId);
            days.Add(date, day);
        }
        return day;
    }

    public (PrayerEvent? Current, PrayerEvent? Next, DateTimeOffset RefreshAt) Status(DateTimeOffset now, bool hanafi)
    {
        var date = DateOnly.FromDateTime(now.Date);
        var today = Day(date);
        var dates = new Dictionary<DateOnly, DailyPrayerTimes>
        {
            [date.AddDays(-1)] = Day(date.AddDays(-1)),
            [date] = today,
            [date.AddDays(1)] = Day(date.AddDays(1))
        };
        var current = PrayerDay.FindCurrentPrayer(dates, date, now, hanafi);
        var next = PrayerDay.FindNextPrayer(dates, date, now, hanafi);
        var midnight = LocalTime.At(date.AddDays(1), TimeOnly.MinValue, place.TimeZoneId);
        var sunrise = PrayerDay.AvailableSunrise(today);
        var refresh = next?.StartsAt is { } start && start < midnight ? start : midnight;
        if (sunrise is { } rising && now < rising && rising < refresh && (current?.Name == "Fajr" || next is null))
        {
            refresh = rising;
        }
        return (current, next, refresh);
    }
}
