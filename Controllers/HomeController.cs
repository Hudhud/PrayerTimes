using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PrayerTimes.Models;

namespace PrayerTimes.Controllers
{
    public class HomeController : Controller
    {

        static string selected_City;
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            string ctx, muwaqqit_URL, sessionString;
            var ctx_cph = HttpContext.Session.GetString("PrayerData_cph");
            var ctx_odense = HttpContext.Session.GetString("PrayerData_odense");
            var ctx_aarhus = HttpContext.Session.GetString("PrayerData_aarhus");
            var ctx_aalborg = HttpContext.Session.GetString("PrayerData_aalborg");

            switch (selected_City)
            {
                case "cph":
                    ctx = ctx_cph;
                    muwaqqit_URL = "https://www.muwaqqit.com/api.json?lt=55.6759142&ln=12.5691285&d=" + getTodayDate() + "&tz=Europe%2FCopenhagen&fa=-18.0&ea=-17.0&fea=0&rsa=0";
                    sessionString = "PrayerData_cph";
                    break;
                case "odense":
                    ctx = ctx_odense;
                    muwaqqit_URL = "https://www.muwaqqit.com/api.json?lt=55.4037560&ln=10.4023700&d=" + getTodayDate() + "&tz=Europe%2FCopenhagen&fa=-18.0&ea=-17.0&fea=0&rsa=0";
                    sessionString = "PrayerData_odense";
                    break;
                case "aarhus":
                    ctx = ctx_aarhus;
                    muwaqqit_URL = "https://www.muwaqqit.com/api.json?lt=56.1629390&ln=10.2039210&d=" + getTodayDate() + "&tz=Europe%2FCopenhagen&fa=-18.0&ea=-17.0&fea=0&rsa=0";
                    sessionString = "PrayerData_aarhus";
                    break;
                case "aalborg":
                    ctx = ctx_aalborg;
                    muwaqqit_URL = "https://www.muwaqqit.com/api.json?lt=57.0488195&ln=9.9217470&d=" + getTodayDate() + "&tz=Europe%2FCopenhagen&fa=-18.0&ea=-17.0&fea=0&rsa=0";
                    sessionString = "PrayerData_aalborg";
                    break;
                default:
                    ctx = ctx_cph;
                    muwaqqit_URL = "https://www.muwaqqit.com/api.json?lt=55.6759142&ln=12.5691285&d=" + getTodayDate() + "&tz=Europe%2FCopenhagen&fa=-18.0&ea=-17.0&fea=0&rsa=0";
                    sessionString = "PrayerData_cph";
                    break;
            }

            if (ctx == null ||
                !JsonConvert.DeserializeObject<Root>(ctx).list.Any(n => n.fajr_date == getTodayDate()))
            {

                using (var httpClient = new HttpClient())
                {
                    using (var response = await httpClient.GetAsync(muwaqqit_URL))
                    {
                        string apiResponse = await response.Content.ReadAsStringAsync();
                        HttpContext.Session.SetString(sessionString, apiResponse);
                        ctx = HttpContext.Session.GetString(sessionString);
                    }
                }
            }

            if (ctx.Contains("429"))
            {
                Thread.Sleep(10000);
                return RedirectToAction("Index");
            }
               
            ViewData["prayers"] = JsonConvert.DeserializeObject<Root>(ctx).list;

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

        public IActionResult Privacy()
        {
            return View();
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
