using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using OfficeOpenXml;
using PrayerTimes.Models;
using PrayerTimes.Persistence;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace PrayerTimes.Controllers
{
    public class HomeController : Controller
    {
        private static string _selectedCity;
        private string _lastEshaTime;


        public IActionResult Check(string buttonValue)
        {
            if (!string.IsNullOrEmpty(buttonValue))
            {
                _selectedCity = buttonValue;
            }

            return RedirectToAction("Index");
        }

        public async Task Index()
        {
            _lastEshaTime = null;

            Database.api ??= new List<APIResult>();

            // Set default selected city if it's null
            if (string.IsNullOrEmpty(_selectedCity))
                _selectedCity = "cph";

            APIResult result = GetOrCreateApiResult(_selectedCity);
            string muwaqqitUrl = GenerateMuwaqqitUrl(_selectedCity);

            using ExcelPackage excel = new();
            for (int month = 1; month <= 12; month++)
            {
                string currentMonth = 2024 + "-" + month.ToString("D2") + "-01";
                muwaqqitUrl = UpdateMuwaqqitUrlDate(muwaqqitUrl, currentMonth);

                if (result.content == null ||
                    !JsonConvert.DeserializeObject<Root>(result.content).list.Any(n => n.fajr_date == currentMonth))
                {
                    string apiResponse = await FetchApiResponse(muwaqqitUrl);
                    result.content = apiResponse;
                    Console.WriteLine(apiResponse);
                }

                List<PrayerViewModel> prayersData = JsonConvert.DeserializeObject<Root>(result.content).list;
                CreateMonthWorksheet(excel, month, prayersData);

                if (month < 12)
                {
                    await Task.Delay(30000);
                }
            }

            SaveExcelFile(excel);
        }


        private APIResult GetOrCreateApiResult(string cityName)
        {
            APIResult result = Database.api.FirstOrDefault(x => x.cityName == cityName);

            if (result == null)
            {
                result = new APIResult { cityName = cityName };
                Database.api.Add(result);
            }

            return result;
        }

        private static string GenerateMuwaqqitUrl(string cityName)
        {
            string baseUrl = "https://www.muwaqqit.com/api.json?";
            string defaultDate = "2024-01-01";
            string timeZone = "Europe%2FCopenhagen";
            string fajrAngle = "-18.0";
            string eshaAngle = "-18.0";
            string fixedEsha = "0";
            string roundedSunriseAngle = "0";

            Dictionary<string, (double lat, double lon)> cities = new()
            {
                { "cph", (55.6759142, 12.5691285) },
                { "odense", (55.4037560, 10.4023700) },
                { "aarhus", (56.1629390, 10.2039210) },
                { "aalborg", (57.0488195, 9.9217470) }
            };

            if (!cities.ContainsKey(cityName))
                cityName = "cph";

            (double lat, double lon) = cities[cityName];

            CultureInfo invariantCulture = CultureInfo.InvariantCulture;

            return $"{baseUrl}lt={lat.ToString(invariantCulture)}&ln={lon.ToString(invariantCulture)}&d={defaultDate}&tz={timeZone}&fa={fajrAngle}&ea={eshaAngle}&fea={fixedEsha}&rsa={roundedSunriseAngle}";
        }

        private static string UpdateMuwaqqitUrlDate(string url, string date)
        {
            int dateStartIndex = url.IndexOf("d=") + 2;
            int dateEndIndex = url.IndexOf("&", dateStartIndex);
            return string.Concat(url.AsSpan(0, dateStartIndex), date, url.AsSpan(dateEndIndex));
        }

        private static async Task<string> FetchApiResponse(string url)
        {
            using var httpClient = new HttpClient();
            using var response = await httpClient.GetAsync(url);

            return await response.Content.ReadAsStringAsync();
        }

        private void CreateMonthWorksheet(ExcelPackage excel, int month, List<PrayerViewModel> prayersData)
        {
            string monthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month);
            excel.Workbook.Worksheets.Add(monthName);

            var excelWorksheet = excel.Workbook.Worksheets[monthName];

            List<string[]> headerRow = new()
            {
            new string[] { "Dato", "Fajr", "Shuruq", "Dhuhr", "Asr", "Asr (hanafi)", "Maghreb", "Isha" }
            };

            string headerRange = "A1:" + char.ConvertFromUtf32(headerRow[0].Length + 64) + "1";
            excelWorksheet.Cells[headerRange].LoadFromArrays(headerRow);

            for (int i = 2; i < prayersData.Count + 2; i++)
            {
                excelWorksheet.Cells["A" + i].Value = prayersData[i - 2].fajr_date;

                string fajrTime = prayersData[i - 2].fajr_angle.ToString() == "anti-transit" ? "01:30" : ProcessPrayerTime(prayersData[i - 2].fajr_time);
                excelWorksheet.Cells["B" + i].Value = fajrTime;

                excelWorksheet.Cells["C" + i].Value = ProcessPrayerTime(prayersData[i - 2].sunrise_time);
                excelWorksheet.Cells["D" + i].Value = ProcessPrayerTime(prayersData[i - 2].zohr_time);
                excelWorksheet.Cells["E" + i].Value = ProcessPrayerTime(prayersData[i - 2].mithl_time);
                excelWorksheet.Cells["F" + i].Value = ProcessPrayerTime(prayersData[i - 2].mithlain_time);
                excelWorksheet.Cells["G" + i].Value = ProcessPrayerTime(prayersData[i - 2].sunset_time);

                object eshaTimeObject = prayersData[i - 2].esha_time;
                string eshaTime = eshaTimeObject != null ? ProcessPrayerTime(eshaTimeObject.ToString(), _lastEshaTime) : _lastEshaTime;
                excelWorksheet.Cells["H" + i].Value = eshaTime;

                if (!string.IsNullOrEmpty(eshaTime))
                {
                    _lastEshaTime = eshaTime;
                }
            }
        }

        private static void SaveExcelFile(ExcelPackage excel)
        {
            FileInfo excelFile = new(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + @"\PrayerTimes.xlsx");
            excel.SaveAs(excelFile);
        }

        private static string ProcessPrayerTime(string timeString, string lastValidTimeString = null)
        {
            DateTime time = DateTime.ParseExact(timeString, "HH:mm:ss", CultureInfo.InvariantCulture);
            DateTime roundedTime = time.AddSeconds(60 - time.Second).AddMinutes(2);
            return roundedTime.ToString("HH:mm");
        }
    }
}