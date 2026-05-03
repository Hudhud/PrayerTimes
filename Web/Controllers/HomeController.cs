using Application.Interfaces;
using AutoMapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
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
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;


        public HomeController(ILogger<HomeController> logger, IPrayerTimeService prayerTimeService, IMapper mapper, IWebHostEnvironment environment, IConfiguration configuration)
        {
            _logger = logger;
            _prayerTimeService = prayerTimeService;
            _mapper = mapper;
            _environment = environment;
            _configuration = configuration;
        }

        public async Task<IActionResult> Index()
        {
            string selectedCity = HttpContext.Session.GetString("SelectedCity") ?? "cph";
            HttpContext.Session.SetString("SelectedCity", selectedCity);

            try
            {
                var prayerDataDTO = await _prayerTimeService.FetchAndCachePrayerTimesAsync(selectedCity.ToLower());

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

                var adminSecret = _configuration["AdminErrorDetailsSecret"];
                var providedSecret = HttpContext.Request.Query["adminSecret"].ToString();
                var secretConfigured = !string.IsNullOrWhiteSpace(adminSecret);
                var showAdminDetails = secretConfigured
                    && !string.IsNullOrWhiteSpace(providedSecret)
                    && string.Equals(adminSecret, providedSecret, StringComparison.Ordinal);

                var details = string.Empty;
                if (_environment.IsDevelopment() || showAdminDetails)
                {
                    details = e.ToString();
                }
                else if (!string.IsNullOrWhiteSpace(providedSecret))
                {
                    details = secretConfigured
                        ? "Provided admin secret did not match the configured secret."
                        : "Admin error-secret is not configured in the deployed app.";
                }

                return View("Error", new CustomErrorViewModel
                {
                    ErrorMessage = "An error occurred while processing your request.",
                    Details = details,
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
            var exceptionFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
            var details = string.Empty;

            var adminSecret = _configuration["AdminErrorDetailsSecret"];
            var providedSecret = HttpContext.Request.Query["adminSecret"].ToString();
            var secretConfigured = !string.IsNullOrWhiteSpace(adminSecret);
            var showAdminDetails = secretConfigured
                && !string.IsNullOrWhiteSpace(providedSecret)
                && string.Equals(adminSecret, providedSecret, StringComparison.Ordinal);

            if ((_environment.IsDevelopment() || showAdminDetails) && exceptionFeature?.Error != null)
            {
                details = exceptionFeature.Error.ToString();
            }
            else if (!string.IsNullOrWhiteSpace(providedSecret))
            {
                details = secretConfigured
                    ? "Provided admin secret did not match the configured secret."
                    : "Admin error-secret is not configured in the deployed app.";
            }

            return View(new CustomErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                ErrorMessage = errorMessage,
                ErrorCode = errorCode,
                Details = details
            });
        }
    }
}
