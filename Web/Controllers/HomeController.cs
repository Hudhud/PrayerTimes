using Domain.Models;
using Domain.Repositories;
using Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Web.ViewModels;

namespace Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger _logger;
        private readonly ICityPrayerTimesRepository _cityPrayerTimesRepository;
        static string selected_City;
        private readonly ILoggerFactory _loggerFactory;


        public HomeController(ILogger<HomeController> logger, ICityPrayerTimesRepository cityPrayerTimesRepository, ILoggerFactory loggerFactory)
        {
            _logger = logger;
            _cityPrayerTimesRepository = cityPrayerTimesRepository;
            _loggerFactory = loggerFactory;
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
                var serviceLogger = _loggerFactory.CreateLogger<PrayerTimesService>();

                var service = new PrayerTimesService(_cityPrayerTimesRepository, serviceLogger);

                return View(await service.GetPrayerData(selected_City.ToLower()));

            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error occurred while fetching prayer data for city {City}", selected_City);
            }

            // Return an empty model if there's an exception
            return View(new MuwaqqitResponse());
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
        public IActionResult Error(string errorMessage = "An unexpected error occurred.", string errorCode = "500")
        {
            return View(new CustomErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                ErrorMessage = errorMessage,
                ErrorCode = errorCode
            });
        }
    }
}
