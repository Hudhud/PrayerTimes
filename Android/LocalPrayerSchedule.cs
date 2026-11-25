using System.Globalization;
using Android.Content;
using Application.Calendars;
using Calculation;
using Domain.Models;

namespace SalahTimes.AndroidApp;

internal static class PrayerPreferences
{
    public const string PlaceKey = "place";
    public const string AngleKey = "twilight-angles";
    public static TwilightAngles? Angles(ISharedPreferences preferences) =>
        TwilightAngles.TryParsePreference(preferences.GetString(AngleKey, null), out var angles) ? angles : null;

    public const string HanafiKey = "hanafi";
    public const string ManualLocationKey = "manual-location";
    public const string FoundByLocationKey = "found-by-location";

    private static PrayerPlaceDirectory? directory;

    public static ISharedPreferences Open(Context context) =>
        context.GetSharedPreferences("salah-times", FileCreationMode.Private)!;

    public static PrayerPlaceDirectory Places(Context context)
    {
        if (directory is null)
        {
            using var catalog = context.Assets!.Open("prayer-places.json");
            using var worldwide = context.Assets!.Open("prayer-cities-world.json");
            directory = new PrayerPlaceDirectory(catalog, worldwide);
        }
        return directory;
    }

    public static PrayerCity SelectedPlace(Context context, ISharedPreferences preferences)
    {
        var slug = preferences.GetString(PlaceKey, null);
        return slug is null ? PrayerCities.Default :
            PrayerCities.Find(slug) ?? Places(context).Find(slug) ?? PrayerCities.Default;
    }

    public static void SavePlace(ISharedPreferences preferences, PrayerCity place, bool fromDevice, string? recent = null)
    {
        using var edit = preferences.Edit();
        edit?.PutString(PlaceKey, place.Slug)?.PutBoolean(ManualLocationKey, !fromDevice)?
            .PutBoolean(FoundByLocationKey, fromDevice);
        if (recent is not null)
        {
            edit?.PutString("recent-places", recent);
        }
        edit?.Apply();
    }
}

internal sealed class LocalPrayerSchedule(PrayerCity place, TwilightAngles? angles = null)
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
