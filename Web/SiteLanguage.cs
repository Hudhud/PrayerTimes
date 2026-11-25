using System.Globalization;
using Application.Calendars;
using Microsoft.AspNetCore.Http;

namespace Web;

public static class SiteLanguage
{
    internal const string DanishSettingsPath = "/indstillinger";
    internal const string EnglishSettingsPath = "/settings";
    internal const string DanishSettingsRoute = "SettingsDa";
    internal const string EnglishSettingsRoute = "SettingsEn";
    internal const string Cookie = "salahtimes-language";
    public static bool IsEnglish => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en";
    public static string T(string danish, string english) => IsEnglish ? english : danish;
    public static string TimeLabel(PrayerCity city) => city.CountryCode == "DK"
        ? T("Dansk lokal tid", "Local time in Denmark")
        : city.TimeZoneId;

    public static string SettingsRoute => IsEnglish ? EnglishSettingsRoute : DanishSettingsRoute;
    internal static string SettingsPath => IsEnglish ? EnglishSettingsPath : DanishSettingsPath;

    public static string SwitchTarget(HttpRequest request)
    {
        if (request.Path == DanishSettingsPath || request.Path == EnglishSettingsPath)
        {
            return (IsEnglish ? DanishSettingsPath : "/en" + EnglishSettingsPath) + request.QueryString;
        }

        return (IsEnglish ? "" : "/en") + (request.Path.HasValue ? request.Path.Value : "/") + request.QueryString;
    }
}
