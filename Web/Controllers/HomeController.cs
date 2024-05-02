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
        private readonly ILoggerFactory _loggerFactory;


        public HomeController(ILogger<HomeController> logger, ICityPrayerTimesRepository cityPrayerTimesRepository, ILoggerFactory loggerFactory)
        {
            _logger = logger;
            _cityPrayerTimesRepository = cityPrayerTimesRepository;
            _loggerFactory = loggerFactory;
        }

        public async Task<IActionResult> Index()
        {
            string selectedCity = HttpContext.Session.GetString("SelectedCity") ?? "cph";
            HttpContext.Session.SetString("SelectedCity", selectedCity);

            try
            {
                var serviceLogger = _loggerFactory.CreateLogger<PrayerTimesService>();
                var service = new PrayerTimesService(_cityPrayerTimesRepository, serviceLogger);
                var prayerData = await service.GetPrayerData(selectedCity.ToLower());

                if (prayerData == null)
                {
                    _logger.LogWarning("No prayer data available for {City}", selectedCity);
                    return View("Error", new CustomErrorViewModel { ErrorMessage = "No prayer data available." });
                }

                return View(prayerData);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error occurred while fetching prayer data for city {City}", selectedCity);
                return View("Error", new CustomErrorViewModel
                {
                    ErrorMessage = "An error occurred while processing your request.",
                    Details = e.Message,
                    ErrorCode = "500",
                    RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
                });
            }
        }


        public IActionResult Check(string button_value)
        {
            if (!string.IsNullOrEmpty(button_value))
            {
                HttpContext.Session.SetString("SelectedCity", button_value);
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
