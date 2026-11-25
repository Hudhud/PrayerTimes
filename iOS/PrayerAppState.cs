using System.Globalization;
using Application.Calendars;
using Calculation;
using Foundation;

namespace SalahTimes.iOS;

internal sealed class PrayerAppState
{
    private readonly NSUserDefaults preferences = NSUserDefaults.StandardUserDefaults;

    public PrayerAppState()
    {
        var bundle = NSBundle.MainBundle;
        var danishPlaces = bundle.PathForResource("prayer-places", "json")
            ?? throw new FileNotFoundException("Danish city catalog is missing from the app.");
        var cities = bundle.PathForResource("prayer-cities-world", "json")
            ?? throw new FileNotFoundException("Worldwide city catalog is missing from the app.");
        Places = PrayerPlaceDirectory.Load(danishPlaces, cities);
        Place = Places.Find(preferences.StringForKey("place")) ?? PrayerCities.Default;
        Schedule = new LocalPrayerSchedule(Place, SavedAngles);
    }

    public event Action? Changed;
    public PrayerPlaceDirectory Places { get; }
    public PrayerCity Place { get; private set; }
    public LocalPrayerSchedule Schedule { get; private set; }
    public bool English => preferences.StringForKey("language") == "en";
    public bool Hanafi => preferences.BoolForKey("hanafi");
    public bool EarlyReminder => preferences.BoolForKey("reminder-early");
    public bool PrayerReminder => preferences.BoolForKey("reminder-start");
    public DateTimeOffset Now => LocalTime.FromUtc(TimeProvider.System.GetUtcNow(), Place.TimeZoneId);
    public DateOnly Today => DateOnly.FromDateTime(Now.Date);
    public CultureInfo Culture => CultureInfo.GetCultureInfo(English ? "en-GB" : "da-DK");
    public TwilightAngles Angles => SavedAngles ?? TwilightAngles.ForPlace(Place);
    private TwilightAngles? SavedAngles =>
        TwilightAngles.TryParsePreference(preferences.StringForKey("angles"), out var angles) ? angles : null;

    public string Text(string danish, string english) => English ? english : danish;

    public void SelectPlace(PrayerCity place)
    {
        preferences.SetString(place.Slug, "place");
        Place = place;
        UpdateSchedule();
    }

    public void SetLanguage(bool english)
    {
        preferences.SetString(english ? "en" : "da", "language");
        Changed?.Invoke();
    }

    public void SetHanafi(bool hanafi)
    {
        preferences.SetBool(hanafi, "hanafi");
        Changed?.Invoke();
    }

    public void SetAngles(TwilightAngles? angles)
    {
        if (angles is null)
        {
            preferences.RemoveObject("angles");
        }
        else
        {
            preferences.SetString(angles.PreferenceValue, "angles");
        }
        UpdateSchedule();
    }

    public void SetReminder(bool early, bool enabled)
    {
        preferences.SetBool(enabled, early ? "reminder-early" : "reminder-start");
        Changed?.Invoke();
    }

    private void UpdateSchedule()
    {
        Schedule = new LocalPrayerSchedule(Place, SavedAngles);
        Changed?.Invoke();
    }
}
