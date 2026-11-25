using Android.Content;
using Application.Calendars;

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
