using Application.Calendars;
using Calculation;
using Web.Services;
using Xunit;

namespace PrayerTimes.Tests;

public sealed class LocalPlacesTests
{
    private readonly PrayerPlaceDirectory places = PrayerPlaceDirectory.Load(Path.Combine(AppContext.BaseDirectory, "prayer-places.json"));

    [Theory]
    [InlineData("Esbjerg", "sted-2622447", "Esbjerg")]
    [InlineData("6700", "post-6700", "6700 Esbjerg")]
    [InlineData("Århus", "aarhus", "Aarhus")]
    [InlineData("koebenhavn", "koebenhavn", "København")]
    [InlineData("Kobenhavn", "koebenhavn", "København")]
    [InlineData("Kbh", "koebenhavn", "København")]
    [InlineData("Viby Aarhus", "sted-2610310", "Viby")]
    public void FindsDanishPlacesAndPostalCodes(string query, string slug, string label)
    {
        var place = places.Search(query).First();

        Assert.Equal(slug, place.Id);
        Assert.Equal(label, places.Find(slug)?.Name);
    }

    [Fact]
    public void FindsTheNearestNamedPlaceIgnoringPostalCentres()
    {
        using var catalog = new MemoryStream("""
            [
              { "id": "post-9000", "name": "Aalborg", "area": "Aalborg Kommune", "latitude": 57, "longitude": 10, "postalCode": "9000" },
              { "id": "naer-by", "name": "Nær by", "area": "Aalborg Kommune", "latitude": 57, "longitude": 10.01 },
              { "id": "fjernere-by", "name": "Fjernere by", "area": "Aalborg Kommune", "latitude": 57.02, "longitude": 10 }
            ]
            """u8.ToArray());
        var directory = new PrayerPlaceDirectory(catalog);

        Assert.Equal("naer-by", directory.FindNearestCity(57, 10)?.Slug);
    }

    [Theory]
    [InlineData(48.86, 2.35)]
    [InlineData(double.NaN, 10)]
    public void DoesNotGuessAPlaceForDistantOrInvalidCoordinates(double latitude, double longitude)
    {
        Assert.Null(places.FindNearestCity(latitude, longitude));
    }

    [Theory]
    [InlineData("Købenahvn")]
    [InlineData("Koebenahvn")]
    [InlineData("Købenavn")]
    [InlineData("Købennhavn")]
    [InlineData("Købenxavn")]
    public void SuggestsCopenhagenForOneSpellingError(string query)
    {
        Assert.Empty(places.Search(query));
        Assert.Contains(places.SuggestSpelling(query), place => place.Id == "koebenhavn");
    }

    [Theory]
    [InlineData("80000")]
    [InlineData("Vbx")]
    [InlineData("zzzzzzzzzzzzzz")]
    public void DoesNotGuessPostalCodesShortNamesOrUnrelatedNames(string query)
    {
        Assert.Empty(places.SuggestSpelling(query));
    }

    [Fact]
    public async Task LocalProviderReturnsTheRequestedRangeAcrossMonthBoundaries()
    {
        using var provider = new LocalPrayerTimeService(new DanishPrayerTimeCalculator());
        var start = new DateOnly(2026, 6, 30);
        var esbjerg = places.Find("sted-2622447")!;

        var days = await provider.GetDaysAsync(esbjerg, start, start.AddDays(3), default);

        Assert.Equal(Enumerable.Range(0, 3).Select(index => start.AddDays(index)),
            days.Select(day => DateOnly.FromDateTime(day.Date)));
        Assert.All(days, day => Assert.True(PrayerDay.IsUsable(day)));
    }

    [Fact]
    public async Task ConcurrentRangesShareTheSameCachedPlaceAndDay()
    {
        using var provider = new LocalPrayerTimeService(new DanishPrayerTimeCalculator());
        var city = PrayerCities.Default;
        var date = new DateOnly(2026, 6, 1);
        var requests = Enumerable.Range(0, 12)
            .Select(_ => Task.Run(() => provider.GetDaysAsync(city, date, date.AddDays(1), default)));

        var results = await Task.WhenAll(requests);

        Assert.All(results, result => Assert.Same(results[0][0], Assert.Single(result)));
    }

    [Fact]
    public async Task CacheSeparatesDefaultAndCustomAnglesForTheSamePlaceAndDay()
    {
        using var provider = new LocalPrayerTimeService(new DanishPrayerTimeCalculator());
        var date = new DateOnly(2026, 6, 21);
        var city = PrayerCities.Default;
        Assert.True(TwilightAngles.TryParse("5.5", "6.5", out var custom));

        var standard = Assert.Single(await provider.GetDaysAsync(city, date, date.AddDays(1), default));
        var adjusted = Assert.Single(await provider.GetDaysAsync(city, date, date.AddDays(1), default, custom));
        Assert.True(standard.FajrTimeUnavailable);
        Assert.True(standard.IshaTimeUnavailable);
        Assert.False(adjusted.FajrTimeUnavailable);
        Assert.False(adjusted.IshaTimeUnavailable);
        Assert.Same(standard, Assert.Single(await provider.GetDaysAsync(city, date, date.AddDays(1), default)));
    }
}
