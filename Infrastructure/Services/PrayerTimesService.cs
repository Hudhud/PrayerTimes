using Domain.Models;
using Domain.Repositories;
using Domain.Services;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Shared;
using System.Net;
using System.Text;

namespace Infrastructure.Services
{
    public class PrayerTimesService : IPrayerTimesService
    {
        private readonly ICityPrayerTimesRepository _cityPrayerTimesRepository;
        private readonly ILogger _logger;
        private const string URL_SUFFIX = "&tz=Europe%2FCopenhagen&fa=-18.0&ea=-17.0&fea=0&rsa=0";

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
                    var url = BuildApiUrl(city);
                    var apiData = await GetApiPrayerData(url);
                    cityPrayerTimes = await ProcessAndStoreApiData(cityPrayerTimes, apiData, city);
                }

                return cityPrayerTimes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch or process prayer data for city: {CityName}", city);
                throw;
            }
        }

        private bool IsCurrentMonthDataAvailable(List<DailyPrayerTimes> dailyPrayerTimesList)
        {
            if (dailyPrayerTimesList == null || !dailyPrayerTimesList.Any())
            {
                return false;
            }

            var firstItemMonth = dailyPrayerTimesList.FirstOrDefault().Date.Month;
            var currentMonth = DateTime.Today.Month;

            return firstItemMonth == currentMonth;
        }

        private string BuildApiUrl(string city, DateTime? date = null)
        {
            // Replace with the appropriate API URLs based on the city
            string baseUrl = city switch
            {
                "cph" => "https://www.muwaqqit.com/api.json?lt=55.6759142&ln=12.5691285&d=",
                "odense" => "https://www.muwaqqit.com/api.json?lt=55.4037560&ln=10.4023700&d=",
                "aarhus" => "https://www.muwaqqit.com/api.json?lt=56.1629390&ln=10.2039210&d=",
                "aalborg" => "https://www.muwaqqit.com/api.json?lt=57.0488195&ln=9.9217470&d=",
                _ => throw new ArgumentException($"Invalid city: {city}"),
            };

            // Use the provided date if available, otherwise use the current date
            var targetDate = date ?? DateTime.Now;
            var dateString = targetDate.ToString("yyyy-MM-dd");

            return $"{baseUrl}{dateString}{URL_SUFFIX}";
        }

        private async Task<MuwaqqitResponse> GetApiPrayerData(string url)
        {
            using var httpClient = new HttpClient();
            try
            {
                _logger.LogInformation("Requesting API data from URL: {Url}", url);
                using var response = await httpClient.GetAsync(url);
                _logger.LogInformation("API response status: {StatusCode}", response.StatusCode);

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    var retryAfter = response.Headers.RetryAfter?.Delta?.TotalSeconds ?? 30;
                    _logger.LogWarning("Rate limit hit, retrying after {RetryAfterSeconds} seconds", retryAfter);
                    await Task.Delay((int)retryAfter * 1000);
                    return await GetApiPrayerData(url);
                }

                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("API response content: {Content}", responseContent);

                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(responseContent));
                using var streamReader = new StreamReader(stream);
                using var jsonTextReader = new JsonTextReader(streamReader);
                var serializer = new JsonSerializer();
                return serializer.Deserialize<MuwaqqitResponse>(jsonTextReader);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON parsing error occurred.");
                throw;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request failed.");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch or process data from the API.");
                throw;
            }
        }

        private async Task<CityPrayerTimes> ProcessAndStoreApiData(CityPrayerTimes cityPrayerTimes, MuwaqqitResponse muwaqqitResponse, string city)
        {
            if (muwaqqitResponse.PrayerTimesDataList == null)
            {
                return cityPrayerTimes;
            }

            var dailyPrayerTimesList = new List<DailyPrayerTimes>();

            MuwaqqitResponse muwaqqitResponseForMay = new();
            if (DateTime.Now.Month >= 5 && DateTime.Now.Month <= 8)
            {
                var mayDate = new DateTime(DateTime.Now.Year, 5, 31);
                var urlForMay = BuildApiUrl(city, mayDate);
                muwaqqitResponseForMay = await GetApiPrayerData(urlForMay);
            }

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
                };

                if (!prayerTimeData.FajrAngle.Equals("anti-transit"))
                {
                    dailyPrayerTimes.FajrTime = PrayerTimesHelper.AddMinutesAndConvertToDateTime(prayerTimeData.FajrTime, 3);
                }
                else
                {
                    var lastValidFajrTimeInMay = muwaqqitResponseForMay.PrayerTimesDataList
                        .Where(pt => !pt.FajrAngle.Equals("anti-transit"))
                        .OrderByDescending(pt => DateTime.Parse(pt.FajrDate))
                        .First();

                    dailyPrayerTimes.FajrTime = PrayerTimesHelper.AddMinutesAndConvertToDateTime(lastValidFajrTimeInMay.FajrTime, 3);
                }

                if (!string.IsNullOrEmpty(prayerTimeData.EshaTime))
                {
                    dailyPrayerTimes.IshaTime = PrayerTimesHelper.AddMinutesAndConvertToDateTime(prayerTimeData.EshaTime, 3);
                }
                else
                {
                    var lastValidIshaTimeInMay = muwaqqitResponseForMay.PrayerTimesDataList
                        .Where(pt => !string.IsNullOrEmpty(pt.EshaTime))
                        .OrderByDescending(pt => DateTime.Parse(pt.FajrDate))
                        .First();

                    dailyPrayerTimes.IshaTime = PrayerTimesHelper.AddMinutesAndConvertToDateTime(lastValidIshaTimeInMay.EshaTime, 3);
                }

                dailyPrayerTimesList.Add(dailyPrayerTimes);
            }

            cityPrayerTimes.DailyPrayerTimesList = dailyPrayerTimesList;

            await _cityPrayerTimesRepository.AddOrUpdateAsync(cityPrayerTimes);

            return cityPrayerTimes;
        }
    }
}
