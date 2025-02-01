using Application.DTOs;
using Application.Interfaces;
using Domain.Models;
using Domain.Repositories;
using Infrastructure.DTO;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Globalization;

namespace Infrastructure.Services
{
    public class PrayerTimeService : IPrayerTimeService
    {
        private readonly ICityPrayerTimesRepository _cityPrayerTimesRepository;
        private readonly ILogger<PrayerTimeService> _logger;
        private readonly HttpClient _httpClient;

        private const string BASE_URL = "https://api.aladhan.com/v1/calendar";
        private const string TIMEZONE = "Europe/Copenhagen";
        private const int METHOD = 3;
        private const string SHAFAQ = "ahmer";
        private const string TUNE = "0,2,0,5,5,2,0,2,0";
        private const int SCHOOL = 0;

        private readonly Dictionary<string, (double Lat, double Lon)> _cityCoordinates = new()
        {
            { "cph", (55.6759142, 12.5691285) },
            { "odense", (55.4037560, 10.4023700) },
            { "aarhus", (56.1629390, 10.2039210) },
            { "aalborg", (57.0488195, 9.9217470) }
        };

        public PrayerTimeService(
            ICityPrayerTimesRepository cityPrayerTimesRepository,
            ILogger<PrayerTimeService> logger,
            HttpClient httpClient)
        {
            _cityPrayerTimesRepository = cityPrayerTimesRepository;
            _logger = logger;
            _httpClient = httpClient;
        }

        public async Task<CityPrayerTimesDTO> FetchAndCachePrayerTimesAsync(string city)
        {
            if (!_cityCoordinates.ContainsKey(city.ToLower()))
            {
                throw new ArgumentException($"Invalid city: {city}");
            }

            var cityPrayerTimes = await _cityPrayerTimesRepository.GetByCityAsync(city);

            if (cityPrayerTimes == null)
            {
                _logger.LogWarning("No data found for city: {CityName}, fetching from Aladhan API.", city);
                var apiData = await GetApiPrayerData(BuildApiUrl(city));
                cityPrayerTimes = await ProcessAndStoreApiData(new CityPrayerTimes { City = city }, apiData);
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
                    MaghribTime = pt.MaghribTime,
                    IshaTime = pt.IshaTime
                }).ToList()
            };
        }

        private string BuildApiUrl(string city)
        {
            var (lat, lon) = _cityCoordinates[city.ToLower()];
            var now = DateTime.UtcNow;

            return $"{BASE_URL}/{now.Year}/{now.Month}?latitude={lat.ToString(CultureInfo.InvariantCulture)}" +
                   $"&longitude={lon.ToString(CultureInfo.InvariantCulture)}" +
                   $"&method={METHOD}&shafaq={SHAFAQ}&tune={TUNE}tuneschool={SCHOOL}" +
                   $"&timezonestring={TIMEZONE}";
        }

        private async Task<AladhanApiResponse> GetApiPrayerData(string url)
        {
            HttpResponseMessage response = await _httpClient.GetAsync(url);
            _logger.LogInformation("Received HTTP status {StatusCode} for URL {Url}", response.StatusCode, url);

            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<AladhanApiResponse>(responseContent);
        }

        private string CleanTime(string timeWithUtc)
        {
            return timeWithUtc.Split(' ')[0];
        }

        private async Task<CityPrayerTimes> ProcessAndStoreApiData(CityPrayerTimes cityPrayerTimes, AladhanApiResponse apiResponse)
        {
            if (apiResponse?.Data == null || !apiResponse.Data.Any())
            {
                _logger.LogWarning("Received empty prayer times data from API for city: {CityName}", cityPrayerTimes.City);
                return cityPrayerTimes;
            }

            var dailyPrayerTimesList = new List<DailyPrayerTimes>();

            foreach (var prayerData in apiResponse.Data)
            {
                var dailyPrayerTimes = new DailyPrayerTimes
                {
                    Date = DateTime.ParseExact(prayerData.Date.Gregorian.Date, "dd-MM-yyyy", CultureInfo.InvariantCulture),
                    FajrTime = CleanTime(prayerData.Timings.Fajr),
                    SunriseTime = CleanTime(prayerData.Timings.Sunrise),
                    DhuhrTime = CleanTime(prayerData.Timings.Dhuhr),
                    AsrTime = CleanTime(prayerData.Timings.Asr),
                    MaghribTime = CleanTime(prayerData.Timings.Maghrib),
                    IshaTime = CleanTime(prayerData.Timings.Isha)
                };

                dailyPrayerTimesList.Add(dailyPrayerTimes);
            }

            cityPrayerTimes.PrayerTimes = dailyPrayerTimesList;
            await _cityPrayerTimesRepository.AddOrUpdateAsync(cityPrayerTimes);

            return cityPrayerTimes;
        }

        public async Task AddOrUpdatePrayerTimesAsync(CityPrayerTimesDTO cityPrayerTimesDTO)
        {
            if (!_cityCoordinates.ContainsKey(cityPrayerTimesDTO.City.ToLower()))
            {
                throw new ArgumentException($"Invalid city: {cityPrayerTimesDTO.City}");
            }

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
                    MaghribTime = pt.MaghribTime,
                    IshaTime = pt.IshaTime
                }).ToList()
            };

            await _cityPrayerTimesRepository.AddOrUpdateAsync(cityPrayerTimes);
        }
    }
}