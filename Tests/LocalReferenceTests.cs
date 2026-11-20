using System.Text;
using System.Text.Json;
using Application.Calendars;
using Calculation;
using Web.Controllers;
using Web.Services;
using Xunit;

namespace PrayerTimes.Tests;

public sealed class LocalReferenceTests
{
    private readonly DanishPrayerTimeCalculator calculator = new();

    [Fact]
    public void KeepsEstablishedTimesAcrossSeasonalAndCalendarBoundaries()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "local-provider-2026.json");
        var rows = JsonSerializer.Deserialize<ReferenceDay[]>(File.ReadAllText(path), JsonSerializerOptions.Web)!;
        Assert.Equal(6, rows.Select(row => row.City).Distinct().Count());

        foreach (var expected in rows)
        {
            var day = calculator.Calculate(expected.Date, expected.Latitude, expected.Longitude);
            var actual = expected with
            {
                Fajr = day.FajrTimeUnavailable ? null : day.FajrTime,
                Sunrise = day.SunriseTime,
                Dhuhr = day.DhuhrTime,
                Asr = day.AsrTime,
                AsrHanafi = day.AsrHanafiTime,
                Maghrib = day.MaghribTime,
                Isha = day.UsesPredefinedIshaTime ? null : day.IshaTime,
                IshaDayOffset = day.IshaDayOffset
            };
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void ExportsHanafiAsrAndOvernightIshaAsInstantsAndOmitsUnknownTimes()
    {
        var city = PrayerCities.Default;
        var may = new DateOnly(2026, 5, 3);
        var june = new DateOnly(2026, 6, 21);
        var rows = new[] { calculator.Calculate(may, 55.6759142, 12.5691285), calculator.Calculate(june, 55.6759142, 12.5691285) };

        string Export(bool hanafi) => Encoding.UTF8.GetString(PrayerCalendarExport.Create(city, rows, hanafi,
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));

        var ics = Export(hanafi: true);
        Assert.Contains("DTSTART:20260503T162400Z\r\nSUMMARY:Asr\r\n", ics);
        Assert.Contains("DTSTART:20260503T151800Z\r\nSUMMARY:Asr\r\n", Export(hanafi: false));
        Assert.Contains("UID:koebenhavn-2026-05-03-isha@salahtimes.dk\r\nDTSTAMP:20260101T000000Z\r\nDTSTART:20260503T220100Z", ics);
        Assert.DoesNotContain("2026-06-21-fajr", ics, StringComparison.Ordinal);
        Assert.DoesNotContain("2026-06-21-isha", ics, StringComparison.Ordinal);
    }

    [Fact]
    public void NextPrayerUsesTheSelectedAsrConvention()
    {
        var date = new DateOnly(2026, 5, 3);
        var day = calculator.Calculate(date, 55.6759142, 12.5691285);
        var days = new Dictionary<DateOnly, Domain.Models.DailyPrayerTimes> { [date] = day };
        var now = DanishTime.At(date, new TimeOnly(18, 0));

        Assert.Equal("Maghrib", HomeController.FindNextPrayer(days, date, now)?.Name);
        var hanafi = HomeController.FindNextPrayer(days, date, now, hanafi: true);
        Assert.Equal("Asr", hanafi?.Name);
        Assert.Equal("18:24", hanafi?.Time);
    }

    private sealed record ReferenceDay(string City, double Latitude, double Longitude, DateOnly Date,
        string? Fajr, string Sunrise, string Dhuhr, string Asr, string AsrHanafi, string Maghrib, string? Isha, int? IshaDayOffset);
}
