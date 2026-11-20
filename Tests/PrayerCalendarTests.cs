using System.Globalization;
using System.Net;
using System.Text.Json;
using Application.Calendars;
using Application.Interfaces;
using Domain.Models;
using Domain.Repositories;
using Infrastructure.DTO;
using Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Web.Controllers;
using Xunit;

namespace PrayerTimes.Tests;

public sealed class PrayerCalendarTests
{
    [Theory]
    [InlineData("2026-11-30T23:30:00Z", "2026-12-01 00:30 +01:00")]
    [InlineData("2026-03-29T00:30:00Z", "2026-03-29 01:30 +01:00")]
    [InlineData("2026-03-29T01:30:00Z", "2026-03-29 03:30 +02:00")]
    [InlineData("2026-10-25T00:30:00Z", "2026-10-25 02:30 +02:00")]
    [InlineData("2026-10-25T01:30:00Z", "2026-10-25 02:30 +01:00")]
    public void ClockUsesDanishTime(string utc, string expected)
    {
        var clock = new DanishTime(new TestClock(DateTimeOffset.Parse(utc, CultureInfo.InvariantCulture).UtcDateTime));

        Assert.Equal(expected, clock.Now.ToString("yyyy-MM-dd HH:mm zzz", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void JuneFallbackIsRecordedAndIshaKeepsItsNextDay()
    {
        var june = new DateOnly(2026, 6, 1);
        var days = PrayerTimeService.ValidateMonth(Month(june, summer: true), PrayerCities.Default, june);

        Assert.Equal(30, days.Length);
        var june30 = days[^1];
        Assert.Equal("02:00", june30.FajrTime);
        Assert.True(june30.UsesPredefinedFajrTime);
        var isha = PrayerDay.Events(june30)[^1];
        Assert.True(isha.IsAdjusted);
        Assert.Equal("00:41", isha.Time);
        Assert.Equal(DanishTime.At(new DateOnly(2026, 7, 1), new TimeOnly(0, 41)), isha.StartsAt);
    }

    [Fact]
    public void RealProviderDateAndMidnightAdjustmentAreRetained()
    {
        var month = new DateOnly(2026, 8, 1);
        var data = Month(month);
        data.Days![0] = data.Days[0] with { IshaTime = "23:59:00" };
        data.Days[1] = data.Days[1] with { IshaDate = "2026-08-03", IshaTime = "00:10:00" };

        var days = PrayerTimeService.ValidateMonth(data, PrayerCities.Default, month);

        Assert.Equal(1, days[0].IshaDayOffset);
        Assert.Equal("00:02", days[0].IshaTime);
        Assert.Equal(1, days[1].IshaDayOffset);
        Assert.Equal("00:13", days[1].IshaTime);
    }

    [Fact]
    public void LeapMonthRejectsMissingAndDuplicateDates()
    {
        var month = new DateOnly(2028, 2, 1);
        var data = Month(month);

        Assert.Equal(29, PrayerTimeService.ValidateMonth(data, PrayerCities.Default, month).Length);
        data.Days![^1] = data.Days[0];
        Assert.Throws<InvalidDataException>(() => PrayerTimeService.ValidateMonth(data, PrayerCities.Default, month));
        data.Days.RemoveAt(0);
        Assert.Throws<InvalidDataException>(() => PrayerTimeService.ValidateMonth(data, PrayerCities.Default, month));
    }

    [Fact]
    public void NextPrayerUsesPriorEveningIshaAndThenNextDaysFajr()
    {
        var june = new DateOnly(2026, 6, 1);
        var july = june.AddMonths(1);
        var rows = PrayerTimeService.ValidateMonth(Month(june, summer: true), PrayerCities.Default, june)
            .Concat(PrayerTimeService.ValidateMonth(Month(july, summer: true), PrayerCities.Default, july))
            .ToDictionary(day => DateOnly.FromDateTime(day.Date));

        // Rows from the legacy schema lack an explicit date offset.
        rows[june.AddMonths(1).AddDays(-1)].IshaDayOffset = null;

        var justAfterMidnight = HomeController.FindNextPrayer(rows, july, DanishTime.At(july, new TimeOnly(0, 15)));
        Assert.NotNull(justAfterMidnight);
        Assert.Equal("Isha", justAfterMidnight.Name);
        Assert.Equal(DanishTime.At(july, new TimeOnly(0, 41)), justAfterMidnight.StartsAt);

        var afterIsha = HomeController.FindNextPrayer(rows, july, DanishTime.At(july, new TimeOnly(0, 42)));
        Assert.NotNull(afterIsha);
        Assert.Equal("Fajr", afterIsha.Name);
        Assert.Equal(DanishTime.At(july, new TimeOnly(2, 0)), afterIsha.StartsAt);

        var winter = new DateOnly(2026, 11, 1);
        var winterRows = PrayerTimeService.ValidateMonth(Month(winter), PrayerCities.Default, winter)
            .ToDictionary(day => DateOnly.FromDateTime(day.Date));
        var nextDay = HomeController.FindNextPrayer(winterRows, winter, DanishTime.At(winter, new TimeOnly(23, 0)));
        Assert.NotNull(nextDay);
        Assert.Equal("Fajr", nextDay.Name);
        Assert.Equal(DanishTime.At(winter.AddDays(1), new TimeOnly(5, 33)), nextDay.StartsAt);
    }

    [Fact]
    public async Task RejectedUpdateLeavesDatabaseAlone()
    {
        var repository = new RecordingRepository();
        var handler = new FakeMuwaqqit();
        using var client = new HttpClient(handler);
        var service = new PrayerTimeService(repository, NullLogger<PrayerTimeService>.Instance, client);
        var month = new DateOnly(2026, 11, 1);

        Assert.Empty(await service.GetDaysAsync(PrayerCities.Default, month, month.AddMonths(1), default));
        Assert.Equal(0, handler.Requests);
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.RefreshMonthIfNeededAsync(PrayerCities.Default, month, default));
        Assert.Equal(1, handler.Requests);
        Assert.Equal(0, repository.Writes);
    }

    private static MuwaqqitResponse Month(DateOnly month, bool summer = false)
    {
        var angle = JsonSerializer.SerializeToElement(summer ? "anti-transit" : "-18");
        return new MuwaqqitResponse(Enumerable.Range(0, DateTime.DaysInMonth(month.Year, month.Month))
            .Select(index => month.AddDays(index).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            .Select(date => new MuwaqqitDay(date, "05:30:00", angle, "07:00:00", "12:30:00", "15:30:00",
                "16:30:00", "18:30:00", summer ? null : date, summer ? null : "20:00:00"))
            .ToList());
    }

    private sealed record TestClock(DateTime UtcNow) : IDateTimeProvider;

    private sealed class RecordingRepository : ICityPrayerTimesRepository
    {
        public int Writes { get; private set; }

        public Task<IReadOnlyList<DailyPrayerTimes>> GetDaysAsync(string city, DateOnly from, DateOnly until,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<DailyPrayerTimes>>([]);

        public Task ReplaceMonthAsync(string city, DateOnly month, IReadOnlyList<DailyPrayerTimes> days,
            CancellationToken cancellationToken)
        {
            Writes++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeMuwaqqit : HttpMessageHandler
    {
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"list\":[]}") });
        }
    }
}
