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
using Web.Services;
using Web.ViewModels;

namespace Web.Controllers;

[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class HomeController(
    IPrayerTimeService prayerTimes,
    DanishTime clock,
    PrayerPlaceDirectory places,
    ILogger<HomeController> logger) : Controller
{
    public const string CityCookie = "salahtimes-city";
    private const string AsrCookie = "salahtimes-asr";
    private const string AnglesCookie = "salahtimes-angles";
    public const string AnalyticsCookie = "salahtimes-statistics";
    private static readonly string[] PrayerNames = ["Fajr", "Solopgang", "Dhuhr", "Asr", "Maghrib", "Isha"];

    [HttpGet("/")]
    public Task<IActionResult> Index(string? month, string? week, CancellationToken cancellationToken)
    {
        var rememberedCity = places.Find(Request.Cookies[CityCookie]);
        return rememberedCity is null || rememberedCity == PrayerCities.Default
            ? ShowCityAsync(PrayerCities.Default, month, week, cancellationToken, isDefaultPlace: rememberedCity is null)
            : Task.FromResult<IActionResult>(RedirectToRoute("City", new { city = rememberedCity.Slug, month, week }));
    }

    [HttpGet("/{city}", Name = "City")]
    public Task<IActionResult> City(string city, string? month, string? week, bool settings, CancellationToken cancellationToken)
    {
        var selected = places.Find(city);
        if (selected is null)
        {
            return Task.FromResult<IActionResult>(NotFound());
        }

        if (Request.Cookies[CityCookie] != selected.Slug)
        {
            Response.Cookies.Append(CityCookie, selected.Slug, PreferenceCookie());
        }

        return settings
            ? Task.FromResult<IActionResult>(RedirectToRoute(SiteLanguage.SettingsRoute))
            : ShowCityAsync(selected, month, week, cancellationToken);
    }

    [HttpGet(SiteLanguage.DanishSettingsPath, Name = SiteLanguage.DanishSettingsRoute)]
    [HttpGet(SiteLanguage.EnglishSettingsPath, Name = SiteLanguage.EnglishSettingsRoute)]
    public IActionResult Settings()
    {
        if (Request.Path != SiteLanguage.SettingsPath)
        {
            return RedirectToRoute(SiteLanguage.SettingsRoute);
        }

        var city = places.Find(Request.Cookies[CityCookie]) ?? PrayerCities.Default;
        return View(new WebsiteSettingsViewModel(city, UsesHanafi, SavedAngles(city), Url.RouteUrl(SiteLanguage.SettingsRoute)!));
    }

    [HttpGet("/sprog")]
    public IActionResult Language(string? language, string? returnUrl)
    {
        if (language is not ("da" or "en"))
        {
            return BadRequest();
        }

        Response.Cookies.Append(SiteLanguage.Cookie, language, PreferenceCookie());
        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl!) : LocalRedirect(language == "en" ? "/en/" : "/");
    }

    [HttpGet("/steder")]
    public IActionResult Places(string? q, bool settings)
    {
        var query = q?.Trim() ?? string.Empty;
        if (query.Length > 80)
        {
            return BadRequest();
        }

        var search = SearchPlaces(query);
        return !search.IsSpellingSuggestion && search.Results.Count == 1
            ? RedirectToRoute("City", new { city = search.Results[0].Id, settings = settings ? true : (bool?)null })
            : View(search with { ReturnToSettings = settings });
    }

    [HttpGet("/steder/forslag")]
    public IActionResult PlaceSuggestions(string? q, bool settings)
    {
        var query = q?.Trim() ?? string.Empty;
        if (query.Length > 80)
        {
            return BadRequest();
        }

        var search = SearchPlaces(query);
        return Json(search.Results.Take(6).Select(place => new
        {
            place.Label,
            Area = place.Area == place.Name ? null : place.Area,
            Url = Url.RouteUrl("City", new { city = place.Id, settings = settings ? true : (bool?)null }),
            search.IsSpellingSuggestion
        }));
    }

    private PlaceSearchViewModel SearchPlaces(string query)
    {
        var results = places.Search(query);
        if (results.Count > 0)
        {
            return new PlaceSearchViewModel(query, results);
        }

        var alternatives = places.SuggestSpelling(query);
        return new PlaceSearchViewModel(query, alternatives, IsSpellingSuggestion: alternatives.Count > 0);
    }

    [HttpGet("/{city}/i-dag", Name = "Today")]
    public async Task<IActionResult> Today(string city, string? asr, string? angles, CancellationToken cancellationToken)
    {
        var selected = places.Find(city);
        if (selected is null)
        {
            return NotFound();
        }

        if (asr is not (null or "standard" or "hanafi") || RequestedAngles(angles, selected) is not { } twilight)
        {
            return BadRequest();
        }

        var daily = await LoadTodayAsync(selected, clock.NowIn(selected.TimeZoneId), asr is null ? UsesHanafi : asr == "hanafi", twilight, cancellationToken);
        return PartialView("_Today", daily);
    }

    [HttpGet("/{city}/offline", Name = "OfflinePlan")]
    public async Task<IActionResult> OfflinePlan(string city, string? asr, string? angles, CancellationToken cancellationToken)
    {
        var selected = places.Find(city);
        if (selected is null)
        {
            return NotFound();
        }
        if (asr is not ("standard" or "hanafi") || RequestedAngles(angles, selected) is not { } twilight)
        {
            return BadRequest();
        }

        var hanafi = asr == "hanafi";
        var start = DateOnly.FromDateTime(clock.NowIn(selected.TimeZoneId).Date);
        var days = await LoadCalendarDaysAsync(selected, start, start.AddDays(7), hanafi, twilight, cancellationToken);
        if (days.Count != 7)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        return View(new OfflinePrayerTimesViewModel(selected, start, days, hanafi, twilight));
    }

    [HttpGet("/offline")]
    public IActionResult Offline() => RedirectToAction(nameof(Index));

    [HttpGet("/beregning")]
    public IActionResult Method() => View("Method", SavedAngles(places.Find(Request.Cookies[CityCookie]) ?? PrayerCities.Default));

    [HttpPost("/vinkler")]
    [ValidateAntiForgeryToken]
    public IActionResult Angles(string? fajr, string? isha, string? returnUrl, bool reset = false)
    {
        if (reset)
        {
            Response.Cookies.Delete(AnglesCookie, PreferenceCookie());
        }
        else if (TwilightAngles.TryParse(fajr, isha, out var twilight))
        {
            Response.Cookies.Append(AnglesCookie, twilight.PreferenceValue, PreferenceCookie());
        }
        else
        {
            return BadRequest(SiteLanguage.T(
                $"Vælg Fajr- og Isha-vinkler mellem {TwilightAngles.Minimum} og {TwilightAngles.Maximum} grader med højst én decimal.",
                $"Choose Fajr and Isha angles between {TwilightAngles.Minimum} and {TwilightAngles.Maximum} degrees, with at most one decimal place."));
        }

        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl!) : RedirectToAction(nameof(Index));
    }

    [HttpPost("/asr")]
    [ValidateAntiForgeryToken]
    public IActionResult Asr(string? choice, string? returnUrl)
    {
        if (choice is not ("standard" or "hanafi"))
        {
            return BadRequest();
        }

        Response.Cookies.Append(AsrCookie, choice, PreferenceCookie());
        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl!) : RedirectToAction(nameof(Index));
    }

    [HttpGet("/{city}/kalender.ics", Name = "CalendarExport")]
    public async Task<IActionResult> Export(string city, string? month, string? angles, CancellationToken cancellationToken)
    {
        var selected = places.Find(city);
        if (selected is null)
        {
            return NotFound();
        }
        var now = clock.NowIn(selected.TimeZoneId);
        var today = DateOnly.FromDateTime(now.Date);
        if (!TryReadMonth(month, today, out var monthStart) || RequestedAngles(angles, selected) is not { } twilight)
        {
            return BadRequest();
        }

        var hanafi = UsesHanafi;
        var days = await LoadCalendarDaysAsync(selected, monthStart, monthStart.AddMonths(1), hanafi, twilight, cancellationToken);
        if (days.Count != DateTime.DaysInMonth(monthStart.Year, monthStart.Month))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        return File(PrayerCalendarExport.Create(selected, days, hanafi, now), "text/calendar; charset=utf-8",
            $"salah-times-{selected.Slug}-{CalendarViewModel.MonthParameter(monthStart)}.ics");
    }

    private bool UsesHanafi => Request.Cookies[AsrCookie] == "hanafi";
    private TwilightAngles SavedAngles(PrayerCity city) =>
        TwilightAngles.TryParsePreference(Request.Cookies[AnglesCookie], out var angles) ? angles : TwilightAngles.ForPlace(city);

    private TwilightAngles? RequestedAngles(string? value, PrayerCity city) =>
        value is null ? SavedAngles(city) : TwilightAngles.TryParsePreference(value, out var angles) ? angles : null;

    private async Task<IActionResult> ShowCityAsync(PrayerCity city, string? month, string? week,
        CancellationToken cancellationToken, bool isDefaultPlace = false)
    {
        var now = clock.NowIn(city.TimeZoneId);
        var today = DateOnly.FromDateTime(now.Date);
        if (!TryReadMonth(month, today, out var monthStart))
        {
            return BadRequest();
        }

        var monthEnd = monthStart.AddMonths(1);
        DateOnly? selectedDate = today >= monthStart && today < monthEnd ? today : null;
        // Open the date from links shared before the switch to a monthly calendar.
        if (week is not null and not "i-dag")
        {
            if (!DateOnly.TryParseExact(week, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                || date < monthStart || date >= monthEnd)
            {
                return BadRequest();
            }
            selectedDate = date;
        }

        var hanafi = UsesHanafi;
        var twilight = SavedAngles(city);
        var monthRows = await LoadCalendarDaysAsync(city, monthStart, monthEnd, hanafi, twilight, cancellationToken);
        var daily = await LoadTodayAsync(city, now, hanafi, twilight, cancellationToken);
        var calendar = new CalendarViewModel(city, today, monthStart, selectedDate, monthRows, hanafi, twilight);
        return View("Index", new CityPrayerTimesViewModel(daily, calendar, isDefaultPlace));
    }

    private static bool TryReadMonth(string? value, DateOnly today, out DateOnly month)
    {
        if (value is null)
        {
            month = new DateOnly(today.Year, today.Month, 1);
            return true;
        }

        month = default;
        return value.Length == 7 && DateOnly.TryParseExact($"{value}-01", "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out month) && month < CalendarViewModel.MonthLimit;
    }

    private async Task<IReadOnlyList<DailyPrayerTimes>> LoadCalendarDaysAsync(PrayerCity city, DateOnly from, DateOnly until, bool hanafi,
        TwilightAngles angles, CancellationToken cancellationToken)
    {
        try
        {
            var rows = await prayerTimes.GetDaysAsync(city, from, until, cancellationToken, angles);
            return ValidDays(rows, hanafi).Where(entry => entry.Key >= from && entry.Key < until)
                .OrderBy(entry => entry.Key).Select(entry => entry.Value).ToArray();
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Could not load the timetable for {City} from {From} until {Until}", city.Slug, from, until);
            return [];
        }
    }

    private async Task<DailyPrayerTimesViewModel> LoadTodayAsync(PrayerCity city, DateTimeOffset now, bool hanafi,
        TwilightAngles angles, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(now.Date);
        var requestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        try
        {
            var rows = await prayerTimes.GetDaysAsync(city, today.AddDays(-1), today.AddDays(2), cancellationToken, angles);
            var days = ValidDays(rows, hanafi);
            if (days.TryGetValue(today, out var todayRow))
            {
                var next = PrayerDay.FindNextPrayer(days, today, now, hanafi);
                var brightNights = next is null && days.Values.Any(day => day.HasUnavailableTimes);
                var current = PrayerDay.FindCurrentPrayer(days, today, now, hanafi);
                return new DailyPrayerTimesViewModel(city, now, todayRow, current, next, brightNights, null, hanafi, angles);
            }

            logger.LogWarning("No valid prayer times for {City}, {Date}. Request {RequestId}", city.Slug, today, requestId);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Could not load prayer times for {City}, {Date}. Request {RequestId}",
                city.Slug, today, requestId);
        }

        Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        return new DailyPrayerTimesViewModel(city, now, null, null, null, false, requestId, hanafi, angles);
    }

    private static Dictionary<DateOnly, DailyPrayerTimes> ValidDays(IEnumerable<DailyPrayerTimes> rows, bool hanafi) =>
        rows.GroupBy(row => DateOnly.FromDateTime(row.Date))
            .Where(group => group.Count() == 1 && PrayerDay.IsUsable(group.Single(), hanafi))
            .ToDictionary(group => group.Key, group => group.Single());

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
        var selected = places.Find(city) ?? PrayerCities.Default;
        var hasDate = DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var parsedDate);
        var validPrayer = PrayerNames.Contains(prayer, StringComparer.Ordinal);

        if (open && selected.Slug == city && hasDate && validPrayer)
        {
            var title = SiteLanguage.T($"Bønnetid i {selected.Name} den {parsedDate:dd.MM.yyyy}",
                $"Prayer time in {selected.Name} on {parsedDate:yyyy-MM-dd}");
            var body = SiteLanguage.T($"By: {selected.Name}\nDato: {parsedDate:yyyy-MM-dd}\nBøn: {prayer}\n\nBeskriv fejlen her:",
                $"City: {selected.Name}\nDate: {parsedDate:yyyy-MM-dd}\nPrayer: {prayer}\n\nDescribe the error:");
            return Redirect(QueryHelpers.AddQueryString("https://github.com/Hudhud/PrayerTimes/issues/new",
                new Dictionary<string, string?> { ["title"] = title, ["body"] = body }));
        }

        return View(new CorrectionViewModel(selected, hasDate ? parsedDate : DateOnly.FromDateTime(clock.NowIn(selected.TimeZoneId).Date),
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
        return View(new CustomErrorViewModel(SiteLanguage.T("Siden kunne ikke indlæses. Prøv igen om lidt.",
            "The page could not be loaded. Please try again shortly."), requestId));
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
