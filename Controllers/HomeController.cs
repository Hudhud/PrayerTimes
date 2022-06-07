using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using PrayerTimes.Models;
using System.Collections.Generic;
using PrayerTimes.Persistence;
using OfficeOpenXml;
using System.IO;

namespace PrayerTimes.Controllers
{
    public class HomeController : Controller
    {
        static string selected_City;

        public async Task<IActionResult> Index()
        {
            if (Database.api == null)
                Database.api = new List<APIResult>();
            var result = Database.api.FirstOrDefault(x => x.cityName == selected_City);
            string muwaqqit_URL = string.Empty;
  

            if (result == null)
            {
                result = new APIResult();
                result.cityName = selected_City;
                switch (selected_City)
                {
                    case "cph":
                        muwaqqit_URL = "https://www.muwaqqit.com/api.json?lt=55.6759142&ln=12.5691285&d=2022-02-01&tz=Europe%2FCopenhagen&fa=-18.0&ea=-18.0&fea=0&rsa=0";
                        break;
                    case "odense":
                        muwaqqit_URL = "https://www.muwaqqit.com/api.json?lt=55.4037560&ln=10.4023700&d=2020-10-01&tz=Europe%2FCopenhagen&fa=-18.0&ea=-18.0&fea=0&rsa=0";
                        break;
                    case "aarhus":
                        muwaqqit_URL = "https://www.muwaqqit.com/api.json?lt=56.1629390&ln=10.2039210&d=2020-10-01&tz=Europe%2FCopenhagen&fa=-18.0&ea=-18.0&fea=0&rsa=0";
                        break;
                    case "aalborg":
                        muwaqqit_URL = "https://www.muwaqqit.com/api.json?lt=57.0488195&ln=9.9217470&d=2020-10-01&tz=Europe%2FCopenhagen&fa=-18.0&ea=-18.0&fea=0&rsa=0";
                        break;
                    default:
                        result.cityName = "aarhus";
                        muwaqqit_URL = "https://www.muwaqqit.com/api.json?lt=56.1629390&ln=10.2039210&d=2022-09-01&tz=Europe%2FCopenhagen&fa=-18.0&ea=-17.0&fea=0&rsa=0";
                        break;
                }
            }

            if (result.content == null ||
                !JsonConvert.DeserializeObject<Root>(result.content).list.Any(n => n.fajr_date == "2022-09-01"))
            {

                using (var httpClient = new HttpClient())
                {
                    using (var response = await httpClient.GetAsync(muwaqqit_URL))
                    {
                        string apiResponse = await response.Content.ReadAsStringAsync();
                        result.content = apiResponse;
                        Console.WriteLine(apiResponse);
                    }
                }
            }


            ViewData["prayers"] = JsonConvert.DeserializeObject<Root>(result.content).list;
            var findcity = Database.api.FirstOrDefault(x => x.cityName == result.cityName);
            if(findcity==null)
                Database.api.Add(result);

            using (ExcelPackage excel = new ExcelPackage())
            {
                excel.Workbook.Worksheets.Add("September");

                var excelWorksheet = excel.Workbook.Worksheets["September"];

                List<string[]> headerRow = new List<string[]>()
                {
                    new string[] { "Dato", "Fajr", "Shuruk", "Zuhr", "Asr", "Asr (hanafi)", "Maghreb", "Isha (shafiyy)" }
                };

                // Determine the header range (e.g. A1:D1)
                string headerRange = "A1:" + Char.ConvertFromUtf32(headerRow[0].Length + 64) + "1";


                // Popular header row data
                excelWorksheet.Cells[headerRange].LoadFromArrays(headerRow);
                var prayersData = ViewData["prayers"] as List<PrayerViewModel>;
                for (int i = 2; i < prayersData.Count + 2 ; i++)
                {
                    excelWorksheet.Cells["A" + i].Value = prayersData[i - 2].fajr_date;
                    excelWorksheet.Cells["B" + i].Value = Convert.ToDateTime(prayersData[i - 2].fajr_time).AddMinutes(2).ToString("HH:mm");
                    excelWorksheet.Cells["C" + i].Value = Convert.ToDateTime(prayersData[i - 2].sunrise_time).AddMinutes(2).ToString("HH:mm");
                    excelWorksheet.Cells["D" + i].Value = Convert.ToDateTime(prayersData[i - 2].zohr_time).AddMinutes(2).ToString("HH:mm");
                    excelWorksheet.Cells["E" + i].Value = Convert.ToDateTime(prayersData[i - 2].mithl_time).AddMinutes(2).ToString("HH:mm");
                    excelWorksheet.Cells["F" + i].Value = Convert.ToDateTime(prayersData[i - 2].mithlain_time).AddMinutes(2).ToString("HH:mm");
                    excelWorksheet.Cells["G" + i].Value = Convert.ToDateTime(prayersData[i - 2].sunset_time).AddMinutes(2).ToString("HH:mm");
                    excelWorksheet.Cells["H" + i].Value = Convert.ToDateTime(prayersData[i - 2].esha_time).AddMinutes(2).ToString("HH:mm");

                }

                FileInfo excelFile = new FileInfo(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + @"\BønnetiderÅrhus\September.xlsx");
                excel.SaveAs(excelFile);
            }

            return View();
        }

        public IActionResult check(string button_value)
        {
            if (!string.IsNullOrEmpty(button_value))
            {
                selected_City = button_value;
            }

            return RedirectToAction("Index");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        public string getTodayDate()
        {
            DateTime dt = DateTime.Today;
            string dateFormatted = dt.Date.ToString("yyyy-MM-d");
            return dateFormatted;
        }
    }
}
