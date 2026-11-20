using Application.Calendars;
using Calculation;
using Web.Services;
using Xunit;

namespace PrayerTimes.Tests;

public sealed class LocalPlacesTests
{
    private readonly PrayerPlaceDirectory places = new(Path.Combine(AppContext.BaseDirectory, "prayer-places.json"));

    [Theory]
    [InlineData("Esbjerg", "sted-2622447", "Esbjerg")]
    [InlineData("6700", "post-6700", "6700 Esbjerg")]
    [InlineData("Århus", "aarhus", "Aarhus")]
    [InlineData("koebenhavn", "koebenhavn", "København")]
    public void FindsDanishPlacesAndPostalCodes(string query, string slug, string label)
    {
        var place = places.Search(query).First();

        Assert.Equal(slug, place.Id);
        Assert.Equal(label, places.Find(slug)?.Name);
    }

    [Fact]
    public void LegacyModeStillResolvesEstablishedCities()
    {
        var legacy = new PrayerPlaceDirectory(null);

        Assert.False(legacy.IsEnabled);
        Assert.Equal(PrayerCities.Default, legacy.Find("koebenhavn"));
        Assert.Null(legacy.Find("post-6700"));
        Assert.Empty(legacy.Search("Esbjerg"));
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
}
