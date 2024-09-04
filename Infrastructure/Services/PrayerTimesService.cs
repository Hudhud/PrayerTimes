using Application.DTOs;
using Application.Interfaces;
using Domain.Models;
using Domain.Repositories;
using Infrastructure.DTO;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Infrastructure.Services
{
    public class PrayerTimesService : IPrayerTimeService
    {
        private readonly ICityPrayerTimesRepository _cityPrayerTimesRepository;
        private readonly ILogger<PrayerTimesService> _logger;
        private readonly HttpClient _httpClient;
        private const string URL_SUFFIX = "&tz=Europe%2FCopenhagen&fa=-18.0&ea=-17.0&fea=0&rsa=0";
        private readonly Dictionary<string, (string Fajr, string Isha)> _predefinedTimes = new()
        {
            { "cph", ("01:09:00", "00:42:00") },
            { "odense", ("01:44:00", "00:50:00") },
            { "aarhus", ("01:33:00", "00:47:00") },
            { "aalborg", ("01:35:00", "00:50:00") }
        };

        public PrayerTimesService(
            ICityPrayerTimesRepository cityPrayerTimesRepository,
            ILogger<PrayerTimesService> logger,
            HttpClient httpClient)
        {
            _cityPrayerTimesRepository = cityPrayerTimesRepository;
            _logger = logger;
            _httpClient = httpClient;
        }

        public async Task<CityPrayerTimesDTO> GetPrayerTimesAsync(string city)
        {
            var cityPrayerTimes = await _cityPrayerTimesRepository.GetByCityAsync(city);

            if (cityPrayerTimes == null)
            {
                _logger.LogWarning("No data found for city: {CityName}, fetching from API.", city);
                var apiData = await GetApiPrayerData(BuildApiUrl(city));
                cityPrayerTimes = await ProcessAndStoreApiData(new CityPrayerTimes { City = city }, apiData, city);
            }

            return new CityPrayerTimesDTO
            {
                City = cityPrayerTimes.City,
                PrayerTimes = cityPrayerTimes.PrayerTimes.Select(pt => new DailyPrayerTimesDTO
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

            var targetDate = date ?? DateTime.Now;
            return $"{baseUrl}{targetDate:yyyy-MM-dd}{URL_SUFFIX}";
        }

        private async Task<MuwaqqitResponse> GetApiPrayerData(string url)
        {
            HttpResponseMessage response = await _httpClient.GetAsync(url);

            _logger.LogInformation("Received HTTP status {StatusCode} for URL {Url}", response.StatusCode, url);

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
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
                _logger.LogWarning("Received empty prayer times data from API for city: {CityName}", city);
                return cityPrayerTimes;
            }

            var dailyPrayerTimesList = new List<DailyPrayerTimes>();

            foreach (var prayerTimeData in muwaqqitResponse.PrayerTimesDataList)
            {
                var dailyPrayerTimes = new DailyPrayerTimes
                {
                    Date = DateTime.Parse(prayerTimeData.FajrDate).Date, // Removing time part
                    SunriseTime = PrayerTimesHelper.AddMinutesAndConvertToString(prayerTimeData.SunriseTime, 3),
                    DhuhrTime = PrayerTimesHelper.AddMinutesAndConvertToString(prayerTimeData.ZohrTime, 3),
                    AsrTime = PrayerTimesHelper.AddMinutesAndConvertToString(prayerTimeData.AsrTime, 3),
                    AsrHanafiTime = PrayerTimesHelper.AddMinutesAndConvertToString(prayerTimeData.MithlainTime, 3),
                    MaghribTime = PrayerTimesHelper.AddMinutesAndConvertToString(prayerTimeData.SunsetTime, 3),
                    FajrTime = string.IsNullOrWhiteSpace(prayerTimeData.FajrTime)
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
