using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
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
		private readonly ILogger<HomeController> _logger;

		public HomeController(ILogger<HomeController> logger)
		{
			_logger = logger;
		}

		public async Task<IActionResult> Index()
		{
			var ctx = HttpContext.Session.GetString("PrayerData");

			if (ctx == null || 
				!JsonConvert.DeserializeObject<Root>(ctx).list.Any(n => n.fajr_date == getTodayDate())) {

			using (var httpClient = new HttpClient())
            {
				using (var response = await httpClient.GetAsync("https://www.muwaqqit.com/api.json?lt=55.5790965&ln=12.2552774&d="+ getTodayDate() + "&tz=Europe%2FCopenhagen&fa=-18.0&ea=-18.0&fea=0&rsa=0"))
				{
					string apiResponse = await response.Content.ReadAsStringAsync();
					HttpContext.Session.SetString("PrayerData", apiResponse);
					ctx = HttpContext.Session.GetString("PrayerData");
					}
				}
			}

			ViewData["prayers"] = JsonConvert.DeserializeObject<Root>(ctx).list;

			return View();
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
