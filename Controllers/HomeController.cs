using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PrayerTimes.Models;
using PrayerTimes.Persistence;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace PrayerTimes.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger _logger;
        private readonly Database _database;
        static string selected_City;

        public HomeController(ILogger<HomeController> logger, Database database)
        {
            _logger = logger;
            _database = database;
        }

        public async Task<IActionResult> Index()
        {
            if (HttpContext.Session.GetString("SameSession") == string.Empty || HttpContext.Session.GetString("SameSession") == null)
            {
                HttpContext.Session.SetString("Active", "cph");
                selected_City = "cph";
                HttpContext.Session.SetString("SameSession", "true");
            }

            if (string.IsNullOrEmpty(selected_City))
            {
                selected_City = "cph";
            }

            try
            {
                var content = await _database.GetApiResult(selected_City.ToLower());
                ViewData["prayers"] = JsonConvert.DeserializeObject<Root>(content).list;
            }
            catch (Exception e)
            {
                _logger.LogInformation(e.Message);
            }


            ViewData["Active"] = HttpContext.Session.GetString("Active");
            ViewData["Deactive"] = HttpContext.Session.GetString("Deactive");

            return View();
        }

        public IActionResult Check(string button_value)
        {
            if (!button_value.Equals("cph"))
            {
                ViewData["Deactive"] = "cph";
                HttpContext.Session.SetString("Deactive", "cph");
            }
            else
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
    }
}
