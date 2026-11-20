using System.Globalization;
using System.Net;
using System.Text.Json;
using Application.Calendars;
using Application.Interfaces;
using Domain.Models;
using Domain.Repositories;
using Infrastructure.DTO;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public sealed class PrayerTimeService(
    ICityPrayerTimesRepository repository,
    ILogger<PrayerTimeService> logger,
    HttpClient httpClient) : IPrayerTimeService
{
    private static readonly SemaphoreSlim FetchLock = new(1);
    private static readonly TimeSpan MinimumRequestInterval = TimeSpan.FromSeconds(15);
    private static DateTimeOffset nextRequestAt = DateTimeOffset.MinValue;
    private const int TimeAdjustmentMinutes = 3;
    private const string ApiOptions = "&tz=Europe%2FCopenhagen&fa=-18.0&ea=-17.0&fea=0&rsa=0";

    public Task<IReadOnlyList<DailyPrayerTimes>> GetDaysAsync(PrayerCity city, DateOnly from, DateOnly until,
        CancellationToken cancellationToken) =>
        repository.GetDaysAsync(city.DataKey, from, until, cancellationToken);

    public async Task RefreshMonthIfNeededAsync(PrayerCity city, DateOnly month,
        CancellationToken cancellationToken)
    {
        var current = await GetDaysAsync(city, month, month.AddMonths(1), cancellationToken);
        if (PrayerDay.IsCompleteMonth(month, current) &&
            current.All(day => day.UsesPredefinedFajrTime.HasValue && day.IshaDayOffset.HasValue))
        {
            return;
        }

        var data = await FetchMonthAsync(city, month, cancellationToken);
        var days = ValidateMonth(data, city, month);
        await repository.ReplaceMonthAsync(city.DataKey, month, days, cancellationToken);
        logger.LogInformation("Saved {DayCount} prayer days for {City}, {Month}", days.Length, city.Slug, month);
    }

    internal static DailyPrayerTimes[] ValidateMonth(MuwaqqitResponse data, PrayerCity city, DateOnly month)
    {
        var days = (data.Days ?? []).Select(day => ConvertDay(day, city)).OrderBy(day => day.Date).ToArray();

        if (!PrayerDay.IsCompleteMonth(month, days))
        {
            throw new InvalidDataException($"Muwaqqit returned an incomplete or invalid timetable for {city.Slug}, {month:yyyy-MM}.");
        }

        return days;
    }

    private async Task<MuwaqqitResponse> FetchMonthAsync(PrayerCity city, DateOnly month,
        CancellationToken cancellationToken)
    {
        var date = month.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var url = $"https://www.muwaqqit.com/api.json?lt={city.Latitude}&ln={city.Longitude}&d={date}{ApiOptions}";

        await FetchLock.WaitAsync(cancellationToken);
        try
        {
            var wait = nextRequestAt - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, cancellationToken);
            }

            for (var attempt = 1; ; attempt++)
            {
                using var response = await httpClient.GetAsync(url, cancellationToken);
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var rateLimited = response.StatusCode == HttpStatusCode.TooManyRequests || content.Trim() == "429";

                if (rateLimited || (int)response.StatusCode >= 500)
                {
                    var now = DateTimeOffset.UtcNow;
                    var retryAfter = response.Headers.RetryAfter;
                    var retryDelay = retryAfter?.Delta ?? (retryAfter?.Date - now)
                        ?? TimeSpan.FromSeconds(rateLimited ? 45 : 15);
                    retryDelay = retryDelay > MinimumRequestInterval ? retryDelay : MinimumRequestInterval;
                    nextRequestAt = now.Add(retryDelay);

                    if (attempt < 3 && retryDelay <= TimeSpan.FromMinutes(5))
                    {
                        await Task.Delay(retryDelay, cancellationToken);
                        continue;
                    }

                    throw new HttpRequestException("Muwaqqit postponed the timetable refresh.",
                        null, response.StatusCode);
                }

                response.EnsureSuccessStatusCode();
                return JsonSerializer.Deserialize<MuwaqqitResponse>(content)
                    ?? throw new InvalidDataException("Muwaqqit returned an empty timetable.");
            }
        }
        finally
        {
            var nextAllowed = DateTimeOffset.UtcNow.Add(MinimumRequestInterval);
            if (nextAllowed > nextRequestAt)
            {
                nextRequestAt = nextAllowed;
            }
            FetchLock.Release();
        }
    }

    private static DailyPrayerTimes ConvertDay(MuwaqqitDay day, PrayerCity city)
    {
        var date = ReadDate(day.FajrDate);
        var fixedFajr = day.FajrAngle.ValueKind == JsonValueKind.String && day.FajrAngle.GetString() == "anti-transit";
        var fixedIsha = string.IsNullOrWhiteSpace(day.IshaTime);
        var maghrib = PrayerTimesHelper.AdjustTime(day.Maghrib, TimeAdjustmentMinutes, out _);
        var isha = PrayerTimesHelper.AdjustTime(fixedIsha ? city.SummerIsha : day.IshaTime,
            TimeAdjustmentMinutes, out var wrappedDays);

        var ishaDayOffset = fixedIsha
            ? (isha < maghrib ? 1 : 0)
            : ReadDate(day.IshaDate).DayNumber - date.DayNumber + wrappedDays;

        return new DailyPrayerTimes
        {
            Date = date.ToDateTime(TimeOnly.MinValue),
            FajrTime = Adjust(fixedFajr ? city.SummerFajr : day.FajrTime),
            SunriseTime = Adjust(day.Sunrise),
            DhuhrTime = Adjust(day.Dhuhr),
            AsrTime = Adjust(day.Asr),
            AsrHanafiTime = Adjust(day.AsrHanafi),
            MaghribTime = Format(maghrib),
            IshaTime = Format(isha),
            UsesPredefinedFajrTime = fixedFajr,
            UsesPredefinedIshaTime = fixedIsha,
            IshaDayOffset = ishaDayOffset
        };
    }

    private static DateOnly ReadDate(string? date)
    {
        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value))
        {
            throw new InvalidDataException("Muwaqqit returned a missing or malformed prayer date.");
        }

        return value;
    }

    private static string Adjust(string? time) =>
        Format(PrayerTimesHelper.AdjustTime(time, TimeAdjustmentMinutes, out _));

    private static string Format(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);
}
