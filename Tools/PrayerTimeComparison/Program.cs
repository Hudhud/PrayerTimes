using System.Globalization;
using System.Text.Json;
using Application.Calendars;
using Calculation;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: PrayerTimeComparison <reference.json>");
    return 2;
}

try
{
    using var input = File.OpenRead(args[0]);
    var rows = JsonSerializer.Deserialize<ReferenceDay[]>(input, JsonSerializerOptions.Web);
    if (rows is not { Length: > 0 }
        || rows.Any(row => row is null || string.IsNullOrWhiteSpace(row.City) || row.Date == default)
        || rows.DistinctBy(row => (row.City.ToUpperInvariant(), row.Date)).Count() != rows.Length)
    {
        throw new InvalidDataException("The reference must contain unique city and date pairs.");
    }

    var calculator = new DanishPrayerTimeCalculator();
    var results = new List<Comparison>();
    foreach (var row in rows.OrderBy(row => row.City).ThenBy(row => row.Date))
    {
        var city = PrayerCities.Find(row.City) ?? throw new InvalidDataException($"Unknown city: {row.City}");
        if (row.Isha is not (null or "") && row.IshaDayOffset is not (0 or 1))
        {
            throw new InvalidDataException("A regular Isha must include a day offset of 0 or 1.");
        }

        var calculated = calculator.Calculate(row.Date, double.Parse(city.Latitude, CultureInfo.InvariantCulture),
            double.Parse(city.Longitude, CultureInfo.InvariantCulture));
        var pairs = new (string Name, string? Reference, string Calculated, int ReferenceOffset, int CalculatedOffset)[]
        {
            ("Fajr", row.Fajr, calculated.FajrTime, 0, 0),
            ("Sunrise", row.Sunrise, calculated.SunriseTime, 0, 0),
            ("Dhuhr", row.Dhuhr, calculated.DhuhrTime, 0, 0),
            ("Asr", row.Asr, calculated.AsrTime, 0, 0),
            ("Asr Hanafi", row.AsrHanafi, calculated.AsrHanafiTime, 0, 0),
            ("Maghrib", row.Maghrib, calculated.MaghribTime, 0, 0),
            ("Isha", row.Isha, calculated.IshaTime, row.IshaDayOffset ?? 0, calculated.IshaDayOffset ?? 0)
        };

        foreach (var pair in pairs)
        {
            var expected = MinuteOfDay(pair.Reference, pair.ReferenceOffset);
            var actual = MinuteOfDay(pair.Calculated, pair.CalculatedOffset);
            if (expected is null && pair.Name is not ("Fajr" or "Isha"))
            {
                throw new InvalidDataException($"{pair.Name} is missing for {city.Slug} on {row.Date:yyyy-MM-dd}.");
            }

            results.Add(new Comparison(city.Slug, row.Date, pair.Name, expected, actual));
        }
    }

    Console.WriteLine($"Reference: {rows.Length} city-days");
    foreach (var city in rows.GroupBy(row => PrayerCities.Find(row.City)!.Slug).OrderBy(group => group.Key))
    {
        var coverage = city.GroupBy(row => new DateOnly(row.Date.Year, row.Date.Month, 1))
            .OrderBy(month => month.Key).Select(month => $"{month.Key:yyyy-MM} ({month.Count()} days)");
        Console.WriteLine($"{city.Key}: {string.Join(", ", coverage)}");
    }
    Console.WriteLine("Prayer        Compared  Both absent  Availability differences  Over 1 min  Delta range");
    foreach (var group in results.GroupBy(result => result.Prayer))
    {
        var measured = group.Where(result => result.Delta is not null)
            .Select(result => result.Delta!.Value).ToArray();
        var range = measured.Length == 0 ? "—" : $"{measured.Min():+0;-0;0} to {measured.Max():+0;-0;0}";
        var bothAbsent = group.Count(result => result.Expected is null && result.Actual is null);
        Console.WriteLine($"{group.Key,-13} {measured.Length,8} {bothAbsent,12} "
            + $"{group.Count(result => result.AvailabilityDiffers),25} {measured.Count(delta => Math.Abs(delta) > 1),11} "
            + $"{range,12}");
    }

    var examples = results.Where(result => result.AvailabilityDiffers || Math.Abs(result.Delta ?? 0) > 1)
        .OrderByDescending(result => result.AvailabilityDiffers)
        .ThenByDescending(result => Math.Abs(result.Delta ?? 0))
        .DistinctBy(result => (result.City, result.Prayer, result.AvailabilityDiffers))
        .Take(10);
    foreach (var example in examples)
    {
        Console.WriteLine($"{example.City} {example.Date:yyyy-MM-dd} {example.Prayer}: "
            + $"{Display(example.Expected)} → {Display(example.Actual)}");
    }

    return 0;
}
catch (Exception error) when (error is IOException or JsonException or InvalidDataException
    or FormatException or ArgumentException)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}

static int? MinuteOfDay(string? time, int dayOffset)
{
    if (string.IsNullOrEmpty(time))
    {
        return null;
    }

    var clock = TimeOnly.ParseExact(time, "HH:mm", CultureInfo.InvariantCulture);
    return dayOffset * 1440 + clock.Hour * 60 + clock.Minute;
}

static string Display(int? minute) => minute is { } value
    ? $"{value % 1440 / 60:00}:{value % 60:00}{(value >= 1440 ? " (+1 day)" : "")}"
    : "- -";

internal sealed record ReferenceDay(string City, DateOnly Date, string? Fajr, string Sunrise, string Dhuhr,
    string Asr, string AsrHanafi, string Maghrib, string? Isha, int? IshaDayOffset);

internal sealed record Comparison(string City, DateOnly Date, string Prayer, int? Expected, int? Actual)
{
    public int? Delta => Actual - Expected;
    public bool AvailabilityDiffers => Expected.HasValue != Actual.HasValue;
}
