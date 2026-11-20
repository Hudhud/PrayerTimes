namespace Application.Calendars;

public sealed record PrayerCity(string Slug, string Name, string DataKey, string Latitude, string Longitude,
    string SummerFajr, string SummerIsha);

public static class PrayerCities
{
    public static IReadOnlyList<PrayerCity> All { get; } = Array.AsReadOnly(new[]
    {
        new PrayerCity("koebenhavn", "København", "cph", "55.6759142", "12.5691285", "01:57:00", "00:38:00"),
        new PrayerCity("odense", "Odense", "odense", "55.4037560", "10.4023700", "01:57:00", "00:46:00"),
        new PrayerCity("aarhus", "Aarhus", "aarhus", "56.1629390", "10.2039210", "01:57:00", "00:41:00"),
        new PrayerCity("aalborg", "Aalborg", "aalborg", "57.0488195", "9.9217470", "01:57:00", "00:46:00")
    });

    public static PrayerCity Default => All[0];

    public static PrayerCity? Find(string? slug) => All.FirstOrDefault(city =>
        string.Equals(city.Slug, slug, StringComparison.OrdinalIgnoreCase));
}
