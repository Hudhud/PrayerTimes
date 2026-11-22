using System.Globalization;
using Android.Content;
using Application.Calendars;
using Calculation;
using Domain.Models;

namespace SalahTimes.AndroidApp;

internal static class PrayerPreferences
{
    public const string PlaceKey = "place";
    public const string HanafiKey = "hanafi";

    private static PrayerPlaceDirectory? directory;

    public static ISharedPreferences Open(Context context) =>
        context.GetSharedPreferences("salah-times", FileCreationMode.Private)!;

    public static PrayerPlaceDirectory Places(Context context)
    {
        if (directory is null)
        {
            using var catalog = context.Assets!.Open("prayer-places.json");
            directory = new PrayerPlaceDirectory(catalog);
        }
        return directory;
    }

    public static PrayerCity SelectedPlace(Context context, ISharedPreferences preferences)
    {
        var slug = preferences.GetString(PlaceKey, null);
        return slug is null ? PrayerCities.Default :
            PrayerCities.Find(slug) ?? Places(context).Find(slug) ?? PrayerCities.Default;
    }
}

internal sealed class LocalPrayerSchedule(PrayerCity place)
{
    private readonly DanishPrayerTimeCalculator calculator = new();
    private readonly Dictionary<DateOnly, DailyPrayerTimes> days = new();
    private readonly double latitude = double.Parse(place.Latitude, CultureInfo.InvariantCulture);
    private readonly double longitude = double.Parse(place.Longitude, CultureInfo.InvariantCulture);

    public DailyPrayerTimes Day(DateOnly date)
    {
        if (!days.TryGetValue(date, out var day))
        {
            day = calculator.Calculate(date, latitude, longitude);
            days.Add(date, day);
        }
        return day;
    }

    public (PrayerEvent? Prayer, DateTimeOffset RefreshAt) Next(DateTimeOffset now, bool hanafi)
    {
        var date = DateOnly.FromDateTime(now.Date);
        var today = Day(date);
        var dates = new Dictionary<DateOnly, DailyPrayerTimes>
        {
            [date.AddDays(-1)] = Day(date.AddDays(-1)),
            [date] = today,
            [date.AddDays(1)] = Day(date.AddDays(1))
        };
        var next = PrayerDay.FindNextPrayer(dates, date, now, hanafi);
        var midnight = DanishTime.At(date.AddDays(1), TimeOnly.MinValue);
        var sunrise = PrayerDay.Sunrise(today);
        var refresh = next?.StartsAt is { } start && start < midnight ? start :
            next is null && now < sunrise ? sunrise : midnight;
        return (next, refresh);
    }
}
