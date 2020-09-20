﻿using System;
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
using Microsoft.Extensions.Logging;

namespace PrayerTimes.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger _logger;
        static string selected_City;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            //      string userIP = HttpContext.Connection.RemoteIpAddress.ToString();
            //    _logger.LogInformation("Current user IP: " + userIP);
            //      _logger.LogInformation("Previous user IP: " + Database.remoteAddress);

            if (HttpContext.Session.GetString("SameSession") == string.Empty || HttpContext.Session.GetString("SameSession") == null)
            {
                HttpContext.Session.SetString("Active", "cph");
                selected_City = "cph";
                _logger.LogInformation("Active variable set to: " + HttpContext.Session.GetString("Active"));
                HttpContext.Session.SetString("SameSession", "true");
            }

            //if (HttpContext.Connection.RemoteIpAddress.ToString() != Database.remoteAddress)
            //{
            //    HttpContext.Session.SetString("Active", "cph");
            //    selected_City = "cph";
            //    _logger.LogInformation("Active variable set to: " + HttpContext.Session.GetString("Active"));
            //    Database.remoteAddress = userIP;
            //}

            if (Database.api == null)
                Database.api = new List<APIResult>();
            var result = Database.api.FirstOrDefault(x => x.cityName == selected_City);
            _logger.LogInformation("Database result:");

            string muwaqqit_URL = string.Empty;

            if (result == null)
            {
                result = new APIResult();
                result.cityName = selected_City;
                switch (selected_City)
                {
                    case "cph":
                        muwaqqit_URL = "https://www.muwaqqit.com/api.json?lt=55.6759142&ln=12.5691285&d=" + getTodayDate() + "&tz=Europe%2FCopenhagen&fa=-18.0&ea=-17.0&fea=0&rsa=0";
                        break;
                    case "odense":
                        muwaqqit_URL = "https://www.muwaqqit.com/api.json?lt=55.4037560&ln=10.4023700&d=" + getTodayDate() + "&tz=Europe%2FCopenhagen&fa=-18.0&ea=-17.0&fea=0&rsa=0";
                        break;
                    case "aarhus":
                        muwaqqit_URL = "https://www.muwaqqit.com/api.json?lt=56.1629390&ln=10.2039210&d=" + getTodayDate() + "&tz=Europe%2FCopenhagen&fa=-18.0&ea=-17.0&fea=0&rsa=0";
                        break;
                    case "aalborg":
                        muwaqqit_URL = "https://www.muwaqqit.com/api.json?lt=57.0488195&ln=9.9217470&d=" + getTodayDate() + "&tz=Europe%2FCopenhagen&fa=-18.0&ea=-17.0&fea=0&rsa=0";
                        break;
                    default:
                        result.cityName = "cph";
                        muwaqqit_URL = "https://www.muwaqqit.com/api.json?lt=55.6759142&ln=12.5691285&d=" + getTodayDate() + "&tz=Europe%2FCopenhagen&fa=-18.0&ea=-17.0&fea=0&rsa=0";
                        HttpContext.Session.SetString("Active", result.cityName);
                        break;
                }
            } else
            {
                _logger.LogInformation(result.content);
            }

            try
            {
                if (result.content == null ||
                              !JsonConvert.DeserializeObject<Root>(result.content).list.Any(n => n.fajr_date == getTodayDate()))
                {
                    if (result.content != null && !JsonConvert.DeserializeObject<Root>(result.content).list.Any(n => n.fajr_date == getTodayDate()))
                        Database.api = new List<APIResult>();

                    using (var httpClient = new HttpClient())
                    {
                        using (var response = await httpClient.GetAsync(muwaqqit_URL))
                        {
                            string apiResponse = await response.Content.ReadAsStringAsync();
                            result.content = apiResponse;
                        }
                    }
                }

                ViewData["prayers"] = JsonConvert.DeserializeObject<Root>(result.content).list;
                var findcity = Database.api.FirstOrDefault(x => x.cityName == result.cityName);

                if (findcity == null)
                    Database.api.Add(result);
            }
            catch (Exception e)
            {
                _logger.LogInformation("ERROR IS HERE");
                _logger.LogInformation(e.Message);
                _logger.LogInformation(e.StackTrace);
            }

            
            ViewData["Active"] = HttpContext.Session.GetString("Active");
            ViewData["Deactive"] = HttpContext.Session.GetString("Deactive");

            return View();
        }

        public IActionResult check(string button_value)
        {
            if (!button_value.Equals("cph")) { 
                ViewData["Deactive"] = "cph";
                HttpContext.Session.SetString("Deactive", "cph");
            } else
            {
                ViewData["Deactive"] = null;
                HttpContext.Session.SetString("Deactive", string.Empty);
            }

            ViewData["Active"] = button_value;
            HttpContext.Session.SetString("Active", button_value);
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
            string dateFormatted = dt.Date.ToString("yyyy-MM-dd");
            return dateFormatted;
        }
    }
}
