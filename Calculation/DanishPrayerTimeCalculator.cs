using System.Globalization;
using Application.Calendars;
using CosineKitty;
using Domain.Models;

namespace Calculation;

public sealed class DanishPrayerTimeCalculator
{
    private const double DegreesToRadians = Math.PI / 180;
    private const double SolarRadiusKilometers = 695700;
    private const double AstronomicalUnitKilometers = 149597870.7;

    public DailyPrayerTimes Calculate(DateOnly date, double latitude, double longitude, TwilightAngles? angles = null,
        string timeZoneId = DanishTime.ZoneId)
    {
        if (!double.IsFinite(latitude) || latitude is < -90 or > 90)
        {
            throw new ArgumentOutOfRangeException(nameof(latitude));
        }

        if (!double.IsFinite(longitude) || longitude is < -180 or > 180)
        {
            throw new ArgumentOutOfRangeException(nameof(longitude));
        }

        angles ??= TwilightAngles.Default;
        var observer = new Observer(latitude, longitude, height: 0);
        var midnight = new AstroTime(LocalTime.At(date, TimeOnly.MinValue, timeZoneId).UtcDateTime);
        var nextMidnight = new AstroTime(LocalTime.At(date.AddDays(1), TimeOnly.MinValue, timeZoneId).UtcDateTime);
        var noon = Astronomy.SearchHourAngle(Body.Sun, observer, 0, midnight, +1).time;
        var previousSolarMidnight = Astronomy.SearchHourAngle(Body.Sun, observer, 12, noon, -1).time;
        var followingSolarMidnight = Astronomy.SearchHourAngle(Body.Sun, observer, 12, noon, +1).time;
        var sunrise = Astronomy.SearchRiseSet(Body.Sun, observer, Direction.Rise, midnight,
            nextMidnight.ut - midnight.ut, metersAboveGround: 0);
        var sunset = Astronomy.SearchRiseSet(Body.Sun, observer, Direction.Set, noon,
            Math.Max(0, nextMidnight.ut - noon.ut), metersAboveGround: 0);

        var fajr = sunrise is null ? null : SearchAltitude(observer, Direction.Rise, previousSolarMidnight, sunrise, -(double)angles.Fajr);
        var isha = sunset is null ? null : SearchAltitude(observer, Direction.Set, sunset, followingSolarMidnight, -(double)angles.Isha);

        var sun = Astronomy.Equator(Body.Sun, noon, observer, EquatorEpoch.OfDate, Aberration.Corrected);
        // Dhuhr begins after the trailing edge of the solar disk has crossed the meridian.
        var solarRadius = Math.Asin(SolarRadiusKilometers / (sun.dist * AstronomicalUnitKilometers));
        var diskClearance = Math.Asin(Math.Sin(solarRadius) / Math.Cos(sun.dec * DegreesToRadians));
        var dhuhr = Astronomy.SearchHourAngle(Body.Sun, observer, diskClearance / DegreesToRadians / 15,
            noon, +1).time;

        var noonAltitude = 90 - Math.Abs(latitude - sun.dec);
        var apparentNoon = noonAltitude + Astronomy.RefractionAngle(Refraction.Normal, noonAltitude);
        var noonShadow = 1 / Math.Tan(apparentNoon * DegreesToRadians);
        var afternoonEnd = sunset ?? followingSolarMidnight;
        var asr = FindAsr(observer, noon, afternoonEnd, noonShadow, shadowLength: 1, apparentNoon);
        var asrHanafi = FindAsr(observer, noon, afternoonEnd, noonShadow, shadowLength: 2, apparentNoon);
        DateTimeOffset? Local(AstroTime? time) => time is null ? null : RoundLocalMinute(time, timeZoneId);

        var events = new PrayerInstants(Local(fajr), Local(sunrise), Local(dhuhr),
            Local(asr), Local(asrHanafi), Local(sunset), Local(isha));
        return new DailyPrayerTimes
        {
            Date = date.ToDateTime(TimeOnly.MinValue),
            FajrTime = Format(events.Fajr),
            SunriseTime = Format(events.Sunrise),
            DhuhrTime = Format(events.Dhuhr),
            AsrTime = Format(events.Asr),
            AsrHanafiTime = Format(events.AsrHanafi),
            MaghribTime = Format(events.Maghrib),
            IshaTime = Format(events.Isha),
            FajrTimeUnavailable = events.Fajr is null,
            IshaTimeUnavailable = events.Isha is null,
            IshaDayOffset = events.Isha is { } end ? DateOnly.FromDateTime(end.Date).DayNumber - date.DayNumber : null,
            Instants = events
        };
    }

    private static AstroTime? SearchAltitude(Observer observer, Direction direction, AstroTime start, AstroTime end,
        double altitude) =>
        Astronomy.SearchAltitude(Body.Sun, observer, direction, start, end.ut - start.ut, altitude);

    private static AstroTime? FindAsr(Observer observer, AstroTime noon, AstroTime end,
        double noonShadow, int shadowLength, double apparentNoon)
    {
        if (apparentNoon <= 0)
        {
            return null;
        }

        // Shadows use the apparent sun. SearchAltitude takes a geometric altitude.
        var apparentAltitude = Math.Atan(1 / (noonShadow + shadowLength)) / DegreesToRadians;
        var altitude = apparentAltitude + Astronomy.InverseRefractionAngle(Refraction.Normal, apparentAltitude);
        return SearchAltitude(observer, Direction.Set, noon, end, altitude);
    }

    private static DateTimeOffset RoundLocalMinute(AstroTime time, string timeZoneId)
    {
        var utc = time.ToUtcDateTime();
        var remainder = utc.Ticks % TimeSpan.TicksPerMinute;
        return LocalTime.FromUtc(remainder == 0 ? utc : utc.AddTicks(TimeSpan.TicksPerMinute - remainder), timeZoneId);
    }

    private static string Format(DateTimeOffset? time) =>
        time?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? string.Empty;
}
