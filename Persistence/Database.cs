using Newtonsoft.Json;
using PrayerTimes.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace PrayerTimes.Persistence
{
    public class ApiResult
    {
        public int Id { get; set; }
        public string cityName { get; set; }
        public string content { get; set; }
        public string url { get; set; }
    }

    public class Database
    {
        private const string URL_SUFFIX = "&tz=Europe%2FCopenhagen&fa=-18.0&ea=-17.0&fea=0&rsa=0";
        private readonly PrayeTimesContext prayeTimesContext;


        public Database(PrayeTimesContext prayeTimesContext)
        {
            this.prayeTimesContext = prayeTimesContext;
            Initialize();
        }

        public async Task<string> GetApiResult(string cityName)
        {
            var api = RetrievePrayersTimesData();
            var apiResult = api[cityName.ToLower()];

            if (string.IsNullOrEmpty(apiResult.content))
            {
                // Initial run, no data in the cache
                await FetchAndUpdateApiData(apiResult);
            }
            else
            {
                // Deserialize the content into a Root object
                var root = JsonConvert.DeserializeObject<Root>(apiResult.content);

                // Check if the fajr_date of the first item in the list is in the current month
                var firstItemMonth = root.list.FirstOrDefault()?.fajr_date.Month;
                var currentMonth = DateTime.Today.Month;

                if (firstItemMonth != currentMonth)
                {
                    // Cached data is not for the current month
                    await FetchAndUpdateApiData(apiResult);
                }
            }

            return apiResult.content;
        }

        private async Task FetchAndUpdateApiData(ApiResult apiResult)
        {
            var url = $"{apiResult.url}{GetTodayDate():yyyy-MM-dd}{URL_SUFFIX}";
            var content = await GetApiContent(url);

            // Deserialize the content into a Root object
            var root = JsonConvert.DeserializeObject<Root>(content);

            // Find the last elements that meet the conditions
            var lastValidFajr = root.list.LastOrDefault(prayer => prayer.fajr_angle == "-18.0");
            var lastValidEsha = root.list.LastOrDefault(prayer => !string.IsNullOrEmpty(prayer.esha_time));

            // Find the current day based on the fajr_date property matching today's date
            var currentDay = root.list.FirstOrDefault(prayer => prayer.fajr_date.ToString("yyyy-MM-dd") == GetTodayDate());

            // Update the fajr_time and esha_time of the current day
            if (currentDay != null)
            {
                if (currentDay.fajr_angle.Equals("anti-transit") && lastValidFajr != null)
                {
                    currentDay.fajr_time = lastValidFajr.fajr_time;
                }
                if (lastValidEsha != null && string.IsNullOrEmpty(currentDay.esha_time))
                {
                    currentDay.esha_time = lastValidEsha.esha_time;
                }
            }

            // Serialize the updated Root object back to a JSON string
            apiResult.content = JsonConvert.SerializeObject(root);

            UpdatePrayerTimesData(apiResult);
        }


        private async Task<string> GetApiContent(string url)
        {
            using var httpClient = new HttpClient();
            using var response = await httpClient.GetAsync(url);
            string apiResponse = await response.Content.ReadAsStringAsync();
            return apiResponse;
        }

        private string GetTodayDate()
        {
            DateTime dt = DateTime.Today;
            string dateFormatted = dt.Date.ToString("yyyy-MM-dd");
            return dateFormatted;
        }

        private void Initialize()
        {
            var apiResultFromDB = RetrievePrayersTimesData();
            if (apiResultFromDB.Count > 0) return;

            var cph = new ApiResult
            {
                cityName = "cph",
                url = "https://www.muwaqqit.com/api.json?lt=55.6759142&ln=12.5691285&d="
            };

            var odense = new ApiResult
            {
                cityName = "odense",
                url = "https://www.muwaqqit.com/api.json?lt=55.4037560&ln=10.4023700&d="
            };

            var aarhus = new ApiResult
            {
                cityName = "aarhus",
                url = "https://www.muwaqqit.com/api.json?lt=56.1629390&ln=10.2039210&d=",
            };

            var aalborg = new ApiResult
            {
                cityName = "aalborg",
                url = "https://www.muwaqqit.com/api.json?lt=57.0488195&ln=9.9217470&d=",
            };

            var apis = new List<ApiResult>
            {
                cph,
                odense,
                aarhus,
                aalborg
            };

            using (var conn = prayeTimesContext.Connection)
            {
                conn.Open();
                foreach (var apiResult in apis)
                {
                    var cmd = conn.CreateCommand();
                    cmd.CommandText = "INSERT INTO PrayerTimes (city,url,content,date) VALUES(@city, @url, @content, @date)";
                    cmd.Parameters.AddWithValue("@city", apiResult.cityName);
                    cmd.Parameters.AddWithValue("@url", apiResult.url);
                    cmd.Parameters.AddWithValue("@content", apiResult.content);

                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void UpdatePrayerTimesData(ApiResult apiResult)
        {
            using (var conn = prayeTimesContext.Connection)
            {
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = "UPDATE PrayerTimes SET content=@content WHERE id=@id";

                cmd.Parameters.AddWithValue("@id", apiResult.Id);
                cmd.Parameters.AddWithValue("@content", apiResult.content);

                cmd.ExecuteNonQuery();
            }
        }

        private Dictionary<string, ApiResult> RetrievePrayersTimesData()
        {
            var api = new Dictionary<string, ApiResult>();
            using (var conn = prayeTimesContext.Connection)
            {
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT * FROM PrayerTimes";
                var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var apiResult = new ApiResult
                    {
                        Id = int.Parse(reader["id"].ToString()),
                        cityName = reader["city"].ToString(),
                        content = reader["content"].ToString(),
                        url = reader["url"].ToString(),
                    };

                    api.Add(apiResult.cityName, apiResult);
                }
            }

            return api;
        }
    }
}
