using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Calendars;
using Application.Interfaces;
using Domain.Models;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Web.ViewModels;

namespace Web.Controllers;

[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class HomeController(
    IPrayerTimeService prayerTimes,
    DanishTime clock,
    ILogger<HomeController> logger) : Controller
{
    public const string CityCookie = "salahtimes-city";
    public const string AnalyticsCookie = "salahtimes-statistics";
    private static readonly string[] PrayerNames = ["Fajr", "Solopgang", "Dhuhr", "Asr", "Maghrib", "Isha"];

    [HttpGet("/")]
    public Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var rememberedCity = PrayerCities.Find(Request.Cookies[CityCookie]);
        return rememberedCity is null || rememberedCity == PrayerCities.Default
            ? ShowCityAsync(PrayerCities.Default, cancellationToken)
            : Task.FromResult<IActionResult>(RedirectToAction(nameof(City), new { city = rememberedCity.Slug }));
    }

    [HttpGet("/{city}", Name = "City")]
    public Task<IActionResult> City(string city, CancellationToken cancellationToken)
    {
        var selected = PrayerCities.Find(city);
        if (selected is null)
        {
            return Task.FromResult<IActionResult>(NotFound());
        }

        if (Request.Cookies[CityCookie] != selected.Slug)
        {
            Response.Cookies.Append(CityCookie, selected.Slug, PreferenceCookie());
        }

        return ShowCityAsync(selected, cancellationToken);
    }

    private async Task<IActionResult> ShowCityAsync(PrayerCity city, CancellationToken cancellationToken)
    {
        var now = clock.Now;
        var today = DateOnly.FromDateTime(now.Date);
        var month = new DateOnly(today.Year, today.Month, 1);
        var nextMonth = month.AddMonths(1);
        var requestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;

        try
        {
            var rows = await prayerTimes.GetDaysAsync(city, month.AddDays(-1), nextMonth.AddDays(1),
                cancellationToken);
            var days = rows.GroupBy(row => DateOnly.FromDateTime(row.Date))
                .Where(group => group.Count() == 1 && PrayerDay.IsUsable(group.Single()))
                .ToDictionary(group => group.Key, group => group.Single());
            var monthRows = days.Where(entry => entry.Key >= month && entry.Key < nextMonth)
                .OrderBy(entry => entry.Key).Select(entry => entry.Value).ToArray();

            if (!days.TryGetValue(today, out var todayRow))
            {
                logger.LogWarning("No valid prayer times for {City}, {Date}. Request {RequestId}", city.Slug, today,
                    requestId);
                Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            }

            var next = todayRow is null ? null : FindNextPrayer(days, today, now);

            return View("Index", new CityPrayerTimesViewModel(city, today, monthRows, todayRow, next,
                todayRow is null ? requestId : null));
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Could not load prayer times for {City}, {Date}. Request {RequestId}",
                city.Slug, today, requestId);
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return View("Index", new CityPrayerTimesViewModel(city, today, [], null, null, requestId));
        }
    }

    internal static PrayerEvent? FindNextPrayer(
        IReadOnlyDictionary<DateOnly, DailyPrayerTimes> days,
        DateOnly today, DateTimeOffset now)
    {
        return new[] { today.AddDays(-1), today, today.AddDays(1) }
            .Where(days.ContainsKey)
            .SelectMany(date => PrayerDay.Events(days[date]))
            .Where(prayer => prayer.StartsAt > now)
            .OrderBy(prayer => prayer.StartsAt)
            .FirstOrDefault();
    }

    [HttpGet("/privatliv")]
    public IActionResult Privacy() => View();

    [HttpPost("/statistik")]
    [ValidateAntiForgeryToken]
    public IActionResult Statistics(string? choice, string? returnUrl)
    {
        if (choice is not ("yes" or "no"))
        {
            return BadRequest();
        }

        Response.Cookies.Append(AnalyticsCookie, choice, PreferenceCookie());

        if (choice == "no")
        {
            foreach (var key in Request.Cookies.Keys.Where(key => key == "_ga" || key.StartsWith("_ga_", StringComparison.Ordinal)))
            {
                Response.Cookies.Delete(key, new CookieOptions { Path = "/" });
                if (Request.Host.Host is "salahtimes.dk" or "www.salahtimes.dk")
                {
                    Response.Cookies.Delete(key, new CookieOptions { Path = "/", Domain = "salahtimes.dk" });
                }
            }
        }

        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl!) : RedirectToAction(nameof(Privacy));
    }

    [HttpGet("/meld-en-fejl")]
    public IActionResult Correction(string? city, string? date, string? prayer, bool open = false)
    {
        var selected = PrayerCities.Find(city) ?? PrayerCities.Default;
        var hasDate = DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var parsedDate);
        var validPrayer = PrayerNames.Contains(prayer, StringComparer.Ordinal);

        if (open && selected.Slug == city && hasDate && validPrayer)
        {
            var title = $"Bønnetid i {selected.Name} den {parsedDate:dd.MM.yyyy}";
            var body = $"By: {selected.Name}\nDato: {parsedDate:yyyy-MM-dd}\nBøn: {prayer}\n\nBeskriv fejlen her:";
            return Redirect(QueryHelpers.AddQueryString("https://github.com/Hudhud/PrayerTimes/issues/new",
                new Dictionary<string, string?> { ["title"] = title, ["body"] = body }));
        }

        return View(new CorrectionViewModel(selected, hasDate ? parsedDate : DateOnly.FromDateTime(clock.Now.Date),
            validPrayer ? prayer! : "Fajr"));
    }

    [Route("/fejl")]
    public IActionResult Error()
    {
        if (HttpContext.Features.Get<IExceptionHandlerFeature>() is null)
        {
            return NotFound();
        }

        Response.StatusCode = StatusCodes.Status500InternalServerError;
        var requestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        return View(new CustomErrorViewModel("Siden kunne ikke indlæses. Prøv igen om lidt.", requestId));
    }

    private CookieOptions PreferenceCookie() => new()
    {
        Path = "/",
        MaxAge = TimeSpan.FromDays(180),
        HttpOnly = true,
        Secure = Request.IsHttps,
        SameSite = SameSiteMode.Lax,
        IsEssential = true
    };
}
