using Domain.Models;
using Domain.Repositories;
using Domain.Services;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Shared;

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
            _logger.LogInformation("Getting API result for city: {CityName}", city);

            // Fetch the CityPrayerTimes data from the database
            var cityPrayerTimes = await _cityPrayerTimesRepository.GetByCityAsync(city);

            if (cityPrayerTimes == null)
            {
                cityPrayerTimes = new CityPrayerTimes
                {
                    City = city,
                    DailyPrayerTimesList = new List<DailyPrayerTimes>()
                };
            }

            // Check if the data for the current month is already available
            var currentMonthDataAvailable = IsCurrentMonthDataAvailable((List<DailyPrayerTimes>)cityPrayerTimes.DailyPrayerTimesList);

            if (!currentMonthDataAvailable)
            {
                // Build the URL based on the city
                var url = BuildApiUrl(city);

                // Fetch the API data
                var muwaqqitResponse = await GetApiPrayerData(url);

                // Process and store the API data in the database
                return await ProcessAndStoreApiData(cityPrayerTimes, muwaqqitResponse, city);
            }
            else
            {
                return cityPrayerTimes;
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
            using var response = await httpClient.GetAsync(url);
            using var stream = await response.Content.ReadAsStreamAsync();

            using var streamReader = new StreamReader(stream);
            using var jsonTextReader = new JsonTextReader(streamReader);

            var serializer = new JsonSerializer();
            var deserializedResponse = serializer.Deserialize<MuwaqqitResponse>(jsonTextReader);

            return deserializedResponse;
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
                };

                if (!prayerTimeData.FajrAngle.Equals("anti-transit"))
                {
                    dailyPrayerTimes.FajrTime = PrayerTimesHelper.AddMinutesAndConvertToDateTime(prayerTimeData.FajrTime, 3);
                }
                else
                {
                    var mayDate = new DateTime(DateTime.Now.Year, 5, 25);
                    var url = BuildApiUrl(city, mayDate);
                    var muwaqqitResponseForMay = await GetApiPrayerData(url);
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
                    var mayDate = new DateTime(DateTime.Now.Year, 5, 25);
                    var url = BuildApiUrl(city, mayDate);
                    var muwaqqitResponseForMay = await GetApiPrayerData(url);
                    var lastValidIshaTimeInMay = muwaqqitResponseForMay.PrayerTimesDataList
                        .Where(pt => !string.IsNullOrEmpty(pt.EshaTime))
                        .OrderByDescending(pt => DateTime.Parse(pt.FajrDate))
                        .First();

                    dailyPrayerTimes.IshaTime = PrayerTimesHelper.AddMinutesAndConvertToDateTime(lastValidIshaTimeInMay.EshaTime, 3);
                }

                dailyPrayerTimesList.Add(dailyPrayerTimes);
            }

            cityPrayerTimes.DailyPrayerTimesList = dailyPrayerTimesList;

            await _cityPrayerTimesRepository.AddAsync(cityPrayerTimes);

            return cityPrayerTimes;
        }
    }
}
