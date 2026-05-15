using Application.DTOs;
using Application.Interfaces;
using Domain.Models;
using Domain.Repositories;
using Infrastructure.DTO;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Threading;

namespace Infrastructure.Services
{
    public class PrayerTimeService : IPrayerTimeService
    {
        private static readonly SemaphoreSlim ApiRequestSemaphore = new(1, 1);
        private static readonly TimeSpan InterCityFetchDelay = TimeSpan.FromSeconds(15);

        private readonly ICityPrayerTimesRepository _cityPrayerTimesRepository;
        private readonly ILogger<PrayerTimeService> _logger;
        private readonly HttpClient _httpClient;
        private readonly IDateTimeProvider _dateTimeProvider;
        private const string URL_SUFFIX = "&tz=Europe%2FCopenhagen&fa=-18.0&ea=-17.0&fea=0&rsa=0";
        private const int MaxApiRetries = 5;
        private static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromSeconds(45);
        private readonly Dictionary<string, (string Fajr, string Isha)> _predefinedTimes = new()
        {
            { "cph", ("01:57:00", "00:38:00") },
            { "odense", ("01:57:00", "00:46:00") },
            { "aarhus", ("01:57:00", "01:42:00") },
            { "aalborg", ("01:57:00", "00:46:00") }
        };

        public PrayerTimeService(
            ICityPrayerTimesRepository cityPrayerTimesRepository,
            ILogger<PrayerTimeService> logger,
            HttpClient httpClient,
            IDateTimeProvider dateTimeProvider)
        {
            _cityPrayerTimesRepository = cityPrayerTimesRepository;
            _logger = logger;
            _httpClient = httpClient;
            _dateTimeProvider = dateTimeProvider;
        }

        public async Task<CityPrayerTimesDTO> FetchAndCachePrayerTimesAsync(string city)
        {
            var cityPrayerTimes = await _cityPrayerTimesRepository.GetByCityAsync(city);
            var denmarkTimeZone = GetDenmarkTimeZone();
            var currentMonth = TimeZoneInfo.ConvertTimeFromUtc(_dateTimeProvider.UtcNow, denmarkTimeZone);

            var hasCurrentMonthData = cityPrayerTimes?.PrayerTimes
                .Any(pt => pt.Date.Year == currentMonth.Year && pt.Date.Month == currentMonth.Month) == true;

            if (cityPrayerTimes == null || !hasCurrentMonthData)
            {
                _logger.LogWarning("No current-month data found for city: {CityName}, fetching from API.", city);
                await ApiRequestSemaphore.WaitAsync();
                try
                {
                    var apiData = await GetApiPrayerData(BuildApiUrl(city));
                    cityPrayerTimes = await ProcessAndStoreApiData(new CityPrayerTimes { City = city }, apiData, city);
                }
                finally
                {
                    await Task.Delay(InterCityFetchDelay);
                    ApiRequestSemaphore.Release();
                }
            }

            return new CityPrayerTimesDTO
            {
                City = cityPrayerTimes.City,
                PrayerTimes = [.. cityPrayerTimes.PrayerTimes.Select(pt => new DailyPrayerTimesDTO
                {
                    Date = pt.Date,
                    FajrTime = pt.FajrTime,
                    SunriseTime = pt.SunriseTime,
                    DhuhrTime = pt.DhuhrTime,
                    AsrTime = pt.AsrTime,
                    AsrHanafiTime = pt.AsrHanafiTime,
                    MaghribTime = pt.MaghribTime,
                    IshaTime = pt.IshaTime
                })]
            };
        }

        public async Task AddOrUpdatePrayerTimesAsync(CityPrayerTimesDTO cityPrayerTimesDTO)
        {
            var cityPrayerTimes = new CityPrayerTimes
            {
                City = cityPrayerTimesDTO.City,
                PrayerTimes = cityPrayerTimesDTO.PrayerTimes.Select(pt => new DailyPrayerTimes
                {
                    Date = pt.Date,
                    FajrTime = pt.FajrTime,
                    SunriseTime = pt.SunriseTime,
                    DhuhrTime = pt.DhuhrTime,
                    AsrTime = pt.AsrTime,
                    AsrHanafiTime = pt.AsrHanafiTime,
                    MaghribTime = pt.MaghribTime,
                    IshaTime = pt.IshaTime
                }).ToList()
            };

            await _cityPrayerTimesRepository.AddOrUpdateAsync(cityPrayerTimes);
        }

        private string BuildApiUrl(string city, DateTime? date = null)
        {
            string baseUrl = city switch
            {
                "cph" => "https://www.muwaqqit.com/api.json?lt=55.6759142&ln=12.5691285&d=",
                "odense" => "https://www.muwaqqit.com/api.json?lt=55.4037560&ln=10.4023700&d=",
                "aarhus" => "https://www.muwaqqit.com/api.json?lt=56.1629390&ln=10.2039210&d=",
                "aalborg" => "https://www.muwaqqit.com/api.json?lt=57.0488195&ln=9.9217470&d=",
                _ => throw new ArgumentException($"Invalid city: {city}")
            };

            var targetDate = date ?? GetCurrentDenmarkDate();
            return $"{baseUrl}{targetDate:yyyy-MM-dd}{URL_SUFFIX}";
        }

        private DateTime GetCurrentDenmarkDate()
        {
            var denmarkTimeZone = GetDenmarkTimeZone();
            return TimeZoneInfo.ConvertTimeFromUtc(_dateTimeProvider.UtcNow, denmarkTimeZone).Date;
        }

        private static TimeZoneInfo GetDenmarkTimeZone()
        {
            var timeZoneId = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "Romance Standard Time"
                : "Europe/Copenhagen";

            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }

        private async Task<MuwaqqitResponse> GetApiPrayerData(string url)
        {
            for (var attempt = 1; attempt <= MaxApiRetries; attempt++)
            {
                HttpResponseMessage response;

                try
                {
                    response = await _httpClient.GetAsync(url);
                }
                catch (HttpRequestException ex) when (IsTlsHandshakeFailure(ex))
                {
                    _logger.LogError(
                        ex,
                        "TLS handshake failure while requesting {Url}. This is not retryable from this Windows Server.",
                        url);

                    throw;
                }

                _logger.LogInformation(
                    "Received HTTP status {StatusCode} for URL {Url} on attempt {Attempt}/{MaxAttempts}",
                    response.StatusCode,
                    url,
                    attempt,
                    MaxApiRetries);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();

                    if (int.TryParse(responseContent.Trim(), out var numericResponse) && numericResponse == 429)
                    {
                        if (attempt == MaxApiRetries)
                        {
                            break;
                        }

                        _logger.LogWarning(
                            "Muwaqqit returned body-only rate limit marker (429). Retrying in {RetryDelaySeconds} seconds (attempt {Attempt}/{MaxAttempts}).",
                            DefaultRetryDelay.TotalSeconds,
                            attempt,
                            MaxApiRetries);

                        await Task.Delay(DefaultRetryDelay);
                        continue;
                    }

                    var deserialized = JsonConvert.DeserializeObject<MuwaqqitResponse>(responseContent);

                    if (deserialized == null)
                    {
                        throw new HttpRequestException("Muwaqqit API returned an empty or invalid JSON payload.");
                    }

                    return deserialized;
                }

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    var retryDelay = response.Headers.RetryAfter?.Delta ?? DefaultRetryDelay;

                    if (attempt == MaxApiRetries)
                    {
                        break;
                    }

                    _logger.LogWarning(
                        "Rate limit hit. Retrying in {RetryDelaySeconds} seconds (attempt {Attempt}/{MaxAttempts}).",
                        retryDelay.TotalSeconds,
                        attempt,
                        MaxApiRetries);

                    await Task.Delay(retryDelay);
                    continue;
                }

                if ((int)response.StatusCode >= 500 && attempt < MaxApiRetries)
                {
                    var retryDelay = TimeSpan.FromSeconds(Math.Pow(2, attempt));

                    _logger.LogWarning(
                        "Transient server error {StatusCode}. Retrying in {RetryDelaySeconds} seconds (attempt {Attempt}/{MaxAttempts}).",
                        response.StatusCode,
                        retryDelay.TotalSeconds,
                        attempt,
                        MaxApiRetries);

                    await Task.Delay(retryDelay);
                    continue;
                }

                response.EnsureSuccessStatusCode();
            }

            throw new HttpRequestException($"Failed to fetch prayer data from API after {MaxApiRetries} attempt(s): {url}");
        }

        private static bool IsTlsHandshakeFailure(HttpRequestException ex)
        {
            return ex.InnerException is AuthenticationException
                || ex.InnerException?.InnerException is AuthenticationException;
        }

        private async Task<CityPrayerTimes> ProcessAndStoreApiData(CityPrayerTimes cityPrayerTimes, MuwaqqitResponse muwaqqitResponse, string city)
        {
            if (muwaqqitResponse.PrayerTimesDataList == null)
            {
                _logger.LogWarning("Received empty prayer times data from API for city: {CityName}", city);
                return cityPrayerTimes;
            }

            var dailyPrayerTimesList = new List<DailyPrayerTimes>();

            foreach (var prayerTimeData in muwaqqitResponse.PrayerTimesDataList)
            {
                var dailyPrayerTimes = new DailyPrayerTimes
                {
                    Date = DateTime.Parse(prayerTimeData.FajrDate).Date,
                    SunriseTime = PrayerTimesHelper.AddMinutesAndConvertToString(prayerTimeData.SunriseTime, 3),
                    DhuhrTime = PrayerTimesHelper.AddMinutesAndConvertToString(prayerTimeData.ZohrTime, 3),
                    AsrTime = PrayerTimesHelper.AddMinutesAndConvertToString(prayerTimeData.AsrTime, 3),
                    AsrHanafiTime = PrayerTimesHelper.AddMinutesAndConvertToString(prayerTimeData.MithlainTime, 3),
                    MaghribTime = PrayerTimesHelper.AddMinutesAndConvertToString(prayerTimeData.SunsetTime, 3),
                    FajrTime = prayerTimeData.FajrAngle is string s && s == "anti-transit"
                    ? PrayerTimesHelper.AddMinutesAndConvertToString(_predefinedTimes[city.ToLower()].Fajr, 3)
                    : PrayerTimesHelper.AddMinutesAndConvertToString(prayerTimeData.FajrTime, 3),
                    IshaTime = string.IsNullOrWhiteSpace(prayerTimeData.EshaTime)
                            ? PrayerTimesHelper.AddMinutesAndConvertToString(_predefinedTimes[city.ToLower()].Isha, 3)
                            : PrayerTimesHelper.AddMinutesAndConvertToString(prayerTimeData.EshaTime, 3)
                };

                dailyPrayerTimesList.Add(dailyPrayerTimes);
            }

            cityPrayerTimes.PrayerTimes = dailyPrayerTimesList;
            await _cityPrayerTimesRepository.AddOrUpdateAsync(cityPrayerTimes);

            return cityPrayerTimes;
        }
    }
}
