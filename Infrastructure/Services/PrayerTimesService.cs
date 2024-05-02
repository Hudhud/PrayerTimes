﻿using Domain.Models;
using Domain.Repositories;
using Domain.Services;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Shared;
using System.Net;


namespace Infrastructure.Services
{
    public class PrayerTimesService : IPrayerTimesService
    {
        private readonly ICityPrayerTimesRepository _cityPrayerTimesRepository;
        private readonly ILogger _logger;
        private const string URL_SUFFIX = "&tz=Europe%2FCopenhagen&fa=-18.0&ea=-17.0&fea=0&rsa=0";
        private MuwaqqitResponse _previousMonthResponseForAntiTransit;
        private DateTime _lastFetchedDate = DateTime.MinValue;
        private bool _hasFetchedPreviousMonth = false;

        public PrayerTimesService(ICityPrayerTimesRepository cityPrayerTimesRepository, ILogger<PrayerTimesService> logger)
        {
            _cityPrayerTimesRepository = cityPrayerTimesRepository;
            _logger = logger;
        }

        public async Task<CityPrayerTimes> GetPrayerData(string city)
        {
            _logger.LogInformation("Attempting to fetch prayer data for city: {CityName}", city);
            try
            {
                if (DateTime.Today.Day == 1)
                {
                    _logger.LogInformation("Truncating tables on the first day of the month.");
                    await _cityPrayerTimesRepository.TruncateTablesAsync();
                }

                var cityPrayerTimes = await _cityPrayerTimesRepository.GetByCityAsync(city);
                if (cityPrayerTimes == null)
                {
                    _logger.LogWarning("No data found for city: {CityName}, fetching from API.", city);
                    cityPrayerTimes = new CityPrayerTimes { City = city, DailyPrayerTimesList = new List<DailyPrayerTimes>() };
                    var apiData = await GetApiPrayerData(BuildApiUrl(city));
                    cityPrayerTimes = await ProcessAndStoreApiData(cityPrayerTimes, apiData, city);
                }

                return cityPrayerTimes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred during the API request to {CityName}", city);
                throw;
            }
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

            var targetDate = date ?? DateTime.Now;
            return $"{baseUrl}{targetDate:yyyy-MM-dd}{URL_SUFFIX}";
        }

        private async Task<MuwaqqitResponse> GetApiPrayerData(string url)
        {
            using var httpClient = new HttpClient();
            HttpResponseMessage response = await httpClient.GetAsync(url);
            _logger.LogInformation("Received HTTP status {StatusCode} for URL {Url}", response.StatusCode, url);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var retryAfter = response.Headers.RetryAfter?.Delta?.TotalSeconds ?? 30;
                _logger.LogWarning("Rate limit hit, retrying after {RetryAfterSeconds} seconds", retryAfter);
                await Task.Delay((int)retryAfter * 1000);
                return await GetApiPrayerData(url);
            }

            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<MuwaqqitResponse>(responseContent);
        }

        private async Task<CityPrayerTimes> ProcessAndStoreApiData(CityPrayerTimes cityPrayerTimes, MuwaqqitResponse muwaqqitResponse, string city)
        {
            if (muwaqqitResponse.PrayerTimesDataList == null)
            {
                return cityPrayerTimes;
            }

            var dailyPrayerTimesList = new List<DailyPrayerTimes>();
            foreach (var prayerTimeData in muwaqqitResponse.PrayerTimesDataList)
            {
                var dailyPrayerTimes = new DailyPrayerTimes
                {
                    Date = DateTime.Parse(prayerTimeData.FajrDate),
                    SunriseTime = PrayerTimesHelper.AddMinutesAndConvertToDateTime(prayerTimeData.SunriseTime, 3),
                    DhuhrTime = PrayerTimesHelper.AddMinutesAndConvertToDateTime(prayerTimeData.ZohrTime, 3),
                    AsrTime = PrayerTimesHelper.AddMinutesAndConvertToDateTime(prayerTimeData.AsrTime, 3),
                    AsrHanafiTime = PrayerTimesHelper.AddMinutesAndConvertToDateTime(prayerTimeData.MithlainTime, 3),
                    MaghribTime = PrayerTimesHelper.AddMinutesAndConvertToDateTime(prayerTimeData.SunsetTime, 3),
                    IshaTime = prayerTimeData.EshaTime != null ? PrayerTimesHelper.AddMinutesAndConvertToDateTime(prayerTimeData.EshaTime, 3) : "N/A"
                };

                if (!prayerTimeData.FajrAngle.Equals("anti-transit"))
                {
                    dailyPrayerTimes.FajrTime = PrayerTimesHelper.AddMinutesAndConvertToDateTime(prayerTimeData.FajrTime, 3);
                }
                else
                {
                    if (!_hasFetchedPreviousMonth || DateTime.Now.Month != _lastFetchedDate.Month)
                    {
                        _hasFetchedPreviousMonth = true;
                        _lastFetchedDate = DateTime.Now;
                        _previousMonthResponseForAntiTransit = await FetchPreviousMonthData(city, new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddDays(-1));
                    }

                    var validPreviousFajrTime = _previousMonthResponseForAntiTransit.PrayerTimesDataList
                        .Where(pt => !pt.FajrAngle.Equals("anti-transit"))
                        .OrderByDescending(pt => DateTime.Parse(pt.FajrDate))
                        .FirstOrDefault();

                    dailyPrayerTimes.FajrTime = validPreviousFajrTime != null ?
                        PrayerTimesHelper.AddMinutesAndConvertToDateTime(validPreviousFajrTime.FajrTime, 3) : "N/A";
                }

                dailyPrayerTimesList.Add(dailyPrayerTimes);
            }

            cityPrayerTimes.DailyPrayerTimesList = dailyPrayerTimesList;
            await _cityPrayerTimesRepository.AddOrUpdateAsync(cityPrayerTimes);

            return cityPrayerTimes;
        }
        private async Task<MuwaqqitResponse> FetchPreviousMonthData(string city, DateTime date)
        {
            var url = BuildApiUrl(city, date);
            return await GetApiPrayerData(url);
        }
    }
}
