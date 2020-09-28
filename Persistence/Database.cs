using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PrayerTimes.Models;

namespace PrayerTimes.Persistence
{
    public class APIResult
    {
        public string cityName { get; set; }
        public string content { get; set; }
        public string url { get; set; }
    }

    public class Database
    {
        private const string URL_SUFFIX = "&tz=Europe%2FCopenhagen&fa=-18.0&ea=-17.0&fea=0&rsa=0";
        private readonly Dictionary<string, APIResult> api;

        public Database()
        {
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

            api = new Dictionary<string, APIResult>
            {
                { cph.cityName, cph },
                { odense.cityName, odense },
                { aarhus.cityName, aarhus },
                { aalborg.cityName, aalborg }
            };
        }

        public async Task<string> GetApiResult(string cityName)
        {
            var apiResult = api[cityName.ToLower()];
            if (apiResult.content == null || !JsonConvert.DeserializeObject<Root>(apiResult.content).list.Any(n => n.fajr_date == getTodayDate()))
            {
                var url = $"{apiResult.url}{getTodayDate()}{URL_SUFFIX}";
                var content = await GetApiContent(url);
                apiResult.content = content;
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
    }
}
