using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using PrayerTimes.Models;

namespace PrayerTimes.Persistence
{
    public class APIResult
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
            if (string.IsNullOrEmpty(apiResult.content) || 
                !JsonConvert.DeserializeObject<Root>(apiResult.content).list.Any(n => n.fajr_date == getTodayDate()))
            {
                var url = $"{apiResult.url}{getTodayDate()}{URL_SUFFIX}";
                var content = await GetApiContent(url);
                apiResult.content = content;

                UpdatePrayerTimesData(apiResult);
            }

            return apiResult.content;
        }

        private async Task<string> GetApiContent(string url)
        {
            using (var httpClient = new HttpClient())
            {
                using (var response = await httpClient.GetAsync(url))
                {
                    string apiResponse = await response.Content.ReadAsStringAsync();
                    return apiResponse;
                }
            }
        }

        private string getTodayDate()
        {
            DateTime dt = DateTime.Today;
            string dateFormatted = dt.Date.ToString("yyyy-MM-dd");
            return dateFormatted;
        }

        private void Initialize()
        {
            var apiResultFromDB = RetrievePrayersTimesData();
            if (apiResultFromDB.Count > 0) return;

            var cph = new APIResult
            {
                cityName = "cph",
                url = "https://www.muwaqqit.com/api.json?lt=55.6759142&ln=12.5691285&d="
            };

            var odense = new APIResult
            {
                cityName = "odense",
                url = "https://www.muwaqqit.com/api.json?lt=55.4037560&ln=10.4023700&d="
            };

            var aarhus = new APIResult
            {
                cityName = "aarhus",
                url = "https://www.muwaqqit.com/api.json?lt=56.1629390&ln=10.2039210&d="
            };

            var aalborg = new APIResult
            {
                cityName = "aalborg",
                url = "https://www.muwaqqit.com/api.json?lt=57.0488195&ln=9.9217470&d="
            };

            var apis = new List<APIResult>
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
                    cmd.CommandText = "INSERT INTO PrayerTimes (city,url,content) VALUES(@city, @url, @content)";
                    cmd.Parameters.AddWithValue("@city", apiResult.cityName);
                    cmd.Parameters.AddWithValue("@url", apiResult.url);
                    cmd.Parameters.AddWithValue("@content", apiResult.content);

                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void UpdatePrayerTimesData(APIResult apiResult)
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

        private Dictionary<string, APIResult> RetrievePrayersTimesData()
        {
            var api = new Dictionary<string, APIResult>();
            using (var conn = prayeTimesContext.Connection)
            {
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT * FROM PrayerTimes";
                var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var apiResult = new APIResult
                    {
                        Id = int.Parse(reader["id"].ToString()),
                        cityName = reader["city"].ToString(),
                        content = reader["content"].ToString(),
                        url = reader["url"].ToString()
                    };

                    api.Add(apiResult.cityName, apiResult);
                }
            }

            return api;
        }
    }
}
