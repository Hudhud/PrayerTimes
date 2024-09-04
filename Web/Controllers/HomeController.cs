using Application.Interfaces;
using AutoMapper;
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
        private readonly ILogger<HomeController> _logger;
        private readonly IPrayerTimeService _prayerTimeService;
        private readonly IMapper _mapper;


        public HomeController(ILogger<HomeController> logger, IPrayerTimeService prayerTimeService, IMapper mapper)
        {
            _logger = logger;
            _prayerTimeService = prayerTimeService;
            _mapper = mapper;
        }

        public async Task<IActionResult> Index()
        {
            string selectedCity = HttpContext.Session.GetString("SelectedCity") ?? "cph";
            HttpContext.Session.SetString("SelectedCity", selectedCity);

            try
            {
                var prayerDataDTO = await _prayerTimeService.GetPrayerTimesAsync(selectedCity.ToLower());

                if (prayerDataDTO == null)
                {
                    _logger.LogWarning("No prayer data available for {City}", selectedCity);
                    return View("Error", new CustomErrorViewModel { ErrorMessage = "No prayer data available." });
                }

                var prayerDataViewModel = _mapper.Map<CityPrayerTimesViewModel>(prayerDataDTO);
                return View(prayerDataViewModel);
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

        [HttpPost]
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
