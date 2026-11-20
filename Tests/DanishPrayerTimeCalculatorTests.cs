using System.Globalization;
using Application.Calendars;
using Calculation;
using Xunit;

namespace PrayerTimes.Tests;

public sealed class DanishPrayerTimeCalculatorTests
{
    private readonly DanishPrayerTimeCalculator calculator = new();

    [Theory]
    [InlineData("koebenhavn")]
    [InlineData("odense")]
    [InlineData("aarhus")]
    [InlineData("aalborg")]
    public void CalculatesAFullYearOfUsablePrayerDays(string citySlug)
    {
        var city = PrayerCities.Find(citySlug)!;
        var latitude = double.Parse(city.Latitude, CultureInfo.InvariantCulture);
        var longitude = double.Parse(city.Longitude, CultureInfo.InvariantCulture);
        var first = new DateOnly(2026, 1, 1);
        var days = Enumerable.Range(0, 365).Select(index => calculator.Calculate(first.AddDays(index),
            latitude, longitude)).ToArray();

        Assert.All(days, day => Assert.True(PrayerDay.IsUsable(day), day.Date.ToString("yyyy-MM-dd")));
        Assert.All(days, day => Assert.True(string.CompareOrdinal(day.AsrTime, day.AsrHanafiTime) < 0));
        Assert.Contains(days, day => day.FajrTimeUnavailable && day.IshaTimeUnavailable);
        Assert.Contains(days, day => day.FajrTimeUnavailable && !day.IshaTimeUnavailable);
        Assert.Contains(days, day => day.IshaDayOffset == 1 && !day.IshaTimeUnavailable);
    }

    [Theory]
    [InlineData(54.56832, 11.96767)] // Birkemose
    [InlineData(56.11667, 8.11667)] // Søndervig
    [InlineData(57.72819, 10.52275)] // Højen
    [InlineData(55.32006, 15.18662)] // Christiansø
    public void CalculatesUsablePrayerDaysAtTheExtremesOfTheDanishCatalog(double latitude, double longitude)
    {
        var first = new DateOnly(2026, 1, 1);

        for (var index = 0; index < 365; index++)
        {
            var date = first.AddDays(index);
            Assert.True(PrayerDay.IsUsable(calculator.Calculate(date, latitude, longitude)),
                date.ToString("yyyy-MM-dd"));
        }
    }

    [Theory]
    [InlineData("aalborg", 11, 6, "12:08", "14:35")]
    [InlineData("aarhus", 12, 8, "12:15", "14:00")]
    public void DhuhrAndHanafiAsrDoNotLeadTheEstablishedExamples(string citySlug, int month, int day,
        string referenceDhuhr, string referenceHanafi)
    {
        var city = PrayerCities.Find(citySlug)!;
        var calculated = calculator.Calculate(new DateOnly(2026, month, day),
            double.Parse(city.Latitude, CultureInfo.InvariantCulture),
            double.Parse(city.Longitude, CultureInfo.InvariantCulture));

        foreach (var (actual, reference) in new[]
        {
            (calculated.DhuhrTime, referenceDhuhr),
            (calculated.AsrHanafiTime, referenceHanafi)
        })
        {
            var difference = TimeOnly.ParseExact(actual, "HH:mm", CultureInfo.InvariantCulture).ToTimeSpan() -
                TimeOnly.ParseExact(reference, "HH:mm", CultureInfo.InvariantCulture).ToTimeSpan();
            Assert.InRange(difference.TotalMinutes, 0, 1);
        }
    }

    [Fact]
    public void BrightNightsHaveNoEstimatedFajrOrIsha()
    {
        var day = calculator.Calculate(new DateOnly(2026, 6, 21), 55.6759142, 12.5691285);

        Assert.True(day.FajrTimeUnavailable);
        Assert.True(day.IshaTimeUnavailable);
        Assert.Empty(day.FajrTime);
        Assert.Empty(day.IshaTime);
        Assert.Null(day.IshaDayOffset);
    }

    [Theory]
    [InlineData(3, 28, 57, 60)]
    [InlineData(10, 24, -61, -58)]
    public void SunriseUsesDanishDaylightSavingTime(int month, int day, int minDelta, int maxDelta)
    {
        var before = new DateOnly(2026, month, day);
        var first = calculator.Calculate(before, 55.6759142, 12.5691285);
        var second = calculator.Calculate(before.AddDays(1), 55.6759142, 12.5691285);
        var firstClock = TimeOnly.ParseExact(first.SunriseTime, "HH:mm", CultureInfo.InvariantCulture);
        var secondClock = TimeOnly.ParseExact(second.SunriseTime, "HH:mm", CultureInfo.InvariantCulture);
        var change = secondClock.ToTimeSpan() - firstClock.ToTimeSpan();

        Assert.InRange(change.TotalMinutes, minDelta, maxDelta);
    }
}
