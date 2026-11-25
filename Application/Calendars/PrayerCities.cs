namespace Application.Calendars;

public sealed record PrayerCity(string Slug, string Name, string Latitude, string Longitude,
    string TimeZoneId = DanishTime.ZoneId, string CountryCode = "DK");

public static class PrayerCities
{
    public static IReadOnlyList<PrayerCity> All { get; } = Array.AsReadOnly(new[]
    {
        new PrayerCity("koebenhavn", "København", "55.6759142", "12.5691285"),
        new PrayerCity("odense", "Odense", "55.4037560", "10.4023700"),
        new PrayerCity("aarhus", "Aarhus", "56.1629390", "10.2039210"),
        new PrayerCity("aalborg", "Aalborg", "57.0488195", "9.9217470")
    });

    public static PrayerCity Default => All[0];

    public static PrayerCity? Find(string? slug) => All.FirstOrDefault(city =>
        string.Equals(city.Slug, slug, StringComparison.OrdinalIgnoreCase));
}
