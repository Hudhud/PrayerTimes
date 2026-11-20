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
    public void BrightNightRowsAreCompleteWithoutAnEstimatedFajrOrIsha()
    {
        var june = new DateOnly(2026, 6, 1);
        var days = PrayerTimeService.ValidateMonth(Month(june, summer: true), PrayerCities.Default, june);

        Assert.Equal(30, days.Length);
        var june30 = days[^1];
        Assert.True(june30.FajrTimeUnavailable);
        Assert.True(june30.UsesPredefinedIshaTime);
        Assert.Empty(june30.FajrTime);
        Assert.Empty(june30.IshaTime);
        Assert.Null(june30.IshaDayOffset);

        var prayers = PrayerDay.Events(june30);
        Assert.Null(prayers[0].StartsAt);
        Assert.NotNull(prayers[1].StartsAt);
        Assert.Null(prayers[^1].StartsAt);
    }

    [Fact]
    public void BrightNightTimesAreHandledIndependentlyAndLegacyEstimatesAreHidden()
    {
        var june = new DateOnly(2026, 6, 1);
        var data = Month(june);
        data.Days![0] = data.Days[0] with { FajrAngle = JsonSerializer.SerializeToElement("anti-transit") };
        data.Days[1] = data.Days[1] with { IshaTime = null, IshaDate = null };

        var days = PrayerTimeService.ValidateMonth(data, PrayerCities.Default, june);
        Assert.Null(PrayerDay.Events(days[0])[0].StartsAt);
        Assert.NotNull(PrayerDay.Events(days[0])[^1].StartsAt);
        Assert.NotNull(PrayerDay.Events(days[1])[0].StartsAt);
        Assert.Null(PrayerDay.Events(days[1])[^1].StartsAt);

        days[0].FajrTime = "02:00";
        days[0].UsesPredefinedFajrTime = null;
        days[1].IshaTime = "00:41";
        days[1].IshaDayOffset = 1;
        Assert.Null(PrayerDay.Events(days[0])[0].StartsAt);
        Assert.Null(PrayerDay.Events(days[1])[^1].StartsAt);

        days[0].UsesPredefinedFajrTime = false;
        Assert.Equal("02:00", PrayerDay.Events(days[0])[0].Time);
    }

    [Fact]
    public async Task CompleteBrightNightMonthDoesNotFetchReplacementTimes()
    {
        var june = new DateOnly(2026, 6, 1);
        var days = PrayerTimeService.ValidateMonth(Month(june, summer: true), PrayerCities.Default, june);
        var repository = new RecordingRepository(days);
        var handler = new FakeMuwaqqit();
        using var client = new HttpClient(handler);
        var service = new PrayerTimeService(repository, NullLogger<PrayerTimeService>.Instance, client);

        await service.RefreshMonthIfNeededAsync(PrayerCities.Default, june, default);

        Assert.Equal(0, handler.Requests);
        Assert.Equal(0, repository.Writes);
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
    public void NextPrayerDoesNotSkipFajrOrIshaWhenTheirTimeIsUnavailable()
    {
        var june = new DateOnly(2026, 6, 1);
        var july = june.AddMonths(1);
        var rows = PrayerTimeService.ValidateMonth(Month(june, summer: true), PrayerCities.Default, june)
            .Concat(PrayerTimeService.ValidateMonth(Month(july, summer: true), PrayerCities.Default, july))
            .ToDictionary(day => DateOnly.FromDateTime(day.Date));

        Assert.Null(HomeController.FindNextPrayer(rows, july, DanishTime.At(july, new TimeOnly(0, 15))));
        Assert.Equal("Dhuhr", HomeController.FindNextPrayer(rows, july, DanishTime.At(july, new TimeOnly(8, 0)))?.Name);
        Assert.Equal("Maghrib", HomeController.FindNextPrayer(rows, july, DanishTime.At(july, new TimeOnly(18, 0)))?.Name);
        Assert.Null(HomeController.FindNextPrayer(rows, july, DanishTime.At(july, new TimeOnly(19, 0))));
    }

    [Fact]
    public void NextPrayerUsesRegularAfterMidnightIshaAndThenNextDaysFajr()
    {
        var month = new DateOnly(2026, 11, 1);
        var data = Month(month);
        var nextDay = month.AddDays(1);
        data.Days![0] = data.Days[0] with { IshaTime = "00:38:00", IshaDate = "2026-11-02" };
        var rows = PrayerTimeService.ValidateMonth(data, PrayerCities.Default, month)
            .ToDictionary(day => DateOnly.FromDateTime(day.Date));

        // Older regular Isha rows may not have an explicit date offset.
        rows[month].IshaDayOffset = null;

        var isha = HomeController.FindNextPrayer(rows, nextDay, DanishTime.At(nextDay, new TimeOnly(0, 15)));
        Assert.Equal("Isha", isha?.Name);
        Assert.Equal(DanishTime.At(nextDay, new TimeOnly(0, 41)), isha?.StartsAt);

        var fajr = HomeController.FindNextPrayer(rows, nextDay, DanishTime.At(nextDay, new TimeOnly(0, 42)));
        Assert.Equal("Fajr", fajr?.Name);
        Assert.Equal(DanishTime.At(nextDay, new TimeOnly(5, 33)), fajr?.StartsAt);

        var tomorrow = HomeController.FindNextPrayer(rows, nextDay, DanishTime.At(nextDay, new TimeOnly(23, 0)));
        Assert.Equal("Fajr", tomorrow?.Name);
        Assert.Equal(DanishTime.At(nextDay.AddDays(1), new TimeOnly(5, 33)), tomorrow?.StartsAt);

        rows[nextDay.AddDays(1)].UsesPredefinedFajrTime = true;
        Assert.Null(HomeController.FindNextPrayer(rows, nextDay, DanishTime.At(nextDay, new TimeOnly(23, 0))));
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

    private sealed class RecordingRepository(IReadOnlyList<DailyPrayerTimes>? days = null) : ICityPrayerTimesRepository
    {
        public int Writes { get; private set; }

        public Task<IReadOnlyList<DailyPrayerTimes>> GetDaysAsync(string city, DateOnly from, DateOnly until,
            CancellationToken cancellationToken) => Task.FromResult(days ?? []);

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
