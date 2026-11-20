using System.Globalization;
using Application.Calendars;
using CosineKitty;
using Domain.Models;

namespace Calculation;

public sealed class DanishPrayerTimeCalculator
{
    private const double FajrAltitude = -18;
    private const double IshaAltitude = -17;
    private const int TimeAdjustmentMinutes = 3;
    private const double DegreesToRadians = Math.PI / 180;
    private const double SolarRadiusKilometers = 695700;
    private const double AstronomicalUnitKilometers = 149597870.7;

    public DailyPrayerTimes Calculate(DateOnly date, double latitude, double longitude)
    {
        if (!double.IsFinite(latitude) || latitude is < -90 or > 90)
        {
            throw new ArgumentOutOfRangeException(nameof(latitude));
        }

        if (!double.IsFinite(longitude) || longitude is < -180 or > 180)
        {
            throw new ArgumentOutOfRangeException(nameof(longitude));
        }

        var observer = new Observer(latitude, longitude, height: 0);
        var midnight = new AstroTime(DanishTime.At(date, TimeOnly.MinValue).UtcDateTime);
        var nextMidnight = new AstroTime(DanishTime.At(date.AddDays(1), TimeOnly.MinValue).UtcDateTime);
        var noon = Astronomy.SearchHourAngle(Body.Sun, observer, 0, midnight, +1).time;
        var previousSolarMidnight = Astronomy.SearchHourAngle(Body.Sun, observer, 12, noon, -1).time;
        var followingSolarMidnight = Astronomy.SearchHourAngle(Body.Sun, observer, 12, noon, +1).time;
        var sunrise = Required(Astronomy.SearchRiseSet(Body.Sun, observer, Direction.Rise, midnight,
            nextMidnight.ut - midnight.ut, metersAboveGround: 0), "sunrise");
        var sunset = Required(Astronomy.SearchRiseSet(Body.Sun, observer, Direction.Set, noon,
            nextMidnight.ut - noon.ut, metersAboveGround: 0), "sunset");

        var fajr = SearchAltitude(observer, Direction.Rise, previousSolarMidnight, sunrise, FajrAltitude);
        var isha = SearchAltitude(observer, Direction.Set, sunset, followingSolarMidnight, IshaAltitude);

        var sun = Astronomy.Equator(Body.Sun, noon, observer, EquatorEpoch.OfDate, Aberration.Corrected);
        // Dhuhr begins after the trailing edge of the solar disk has crossed the meridian.
        var solarRadius = Math.Asin(SolarRadiusKilometers / (sun.dist * AstronomicalUnitKilometers));
        var diskClearance = Math.Asin(Math.Sin(solarRadius) / Math.Cos(sun.dec * DegreesToRadians));
        var dhuhr = Astronomy.SearchHourAngle(Body.Sun, observer, diskClearance / DegreesToRadians / 15,
            noon, +1).time;

        var noonAltitude = 90 - Math.Abs(latitude - sun.dec);
        var apparentNoon = noonAltitude + Astronomy.RefractionAngle(Refraction.Normal, noonAltitude);
        var noonShadow = 1 / Math.Tan(apparentNoon * DegreesToRadians);
        var asr = FindAsr(observer, noon, sunset, noonShadow, shadowLength: 1);
        var asrHanafi = FindAsr(observer, noon, sunset, noonShadow, shadowLength: 2);

        var localIsha = isha is null ? (DateTimeOffset?)null : AdjustLocal(isha);
        var result = new DailyPrayerTimes
        {
            Date = date.ToDateTime(TimeOnly.MinValue),
            FajrTime = fajr is null ? string.Empty : FormatOnDate(date, fajr),
            SunriseTime = FormatOnDate(date, sunrise),
            DhuhrTime = FormatOnDate(date, dhuhr),
            AsrTime = FormatOnDate(date, asr),
            AsrHanafiTime = FormatOnDate(date, asrHanafi),
            MaghribTime = FormatOnDate(date, sunset),
            IshaTime = localIsha?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? string.Empty,
            FajrTimeUnavailable = fajr is null,
            IshaTimeUnavailable = isha is null,
            IshaDayOffset = localIsha is { } end ? DateOnly.FromDateTime(end.Date).DayNumber - date.DayNumber : null
        };

        if (!PrayerDay.IsUsable(result))
        {
            throw new InvalidDataException("The calculated times do not form a valid Danish prayer day.");
        }

        return result;
    }

    private static AstroTime? SearchAltitude(Observer observer, Direction direction, AstroTime start, AstroTime end,
        double altitude) =>
        Astronomy.SearchAltitude(Body.Sun, observer, direction, start, end.ut - start.ut, altitude);

    private static AstroTime FindAsr(Observer observer, AstroTime noon, AstroTime sunset, double noonShadow,
        int shadowLength)
    {
        // Shadows use the apparent sun. SearchAltitude takes a geometric altitude.
        var apparentAltitude = Math.Atan(1 / (noonShadow + shadowLength)) / DegreesToRadians;
        var altitude = apparentAltitude + Astronomy.InverseRefractionAngle(Refraction.Normal, apparentAltitude);
        return Required(SearchAltitude(observer, Direction.Set, noon, sunset, altitude), "Asr");
    }

    private static AstroTime Required(AstroTime? time, string eventName) =>
        time ?? throw new InvalidDataException($"Could not calculate {eventName} for the date and location.");

    private static DateTimeOffset AdjustLocal(AstroTime time) =>
        DanishTime.FromUtc(time.ToUtcDateTime().AddMinutes(TimeAdjustmentMinutes));

    private static string FormatOnDate(DateOnly date, AstroTime time)
    {
        var local = AdjustLocal(time);
        if (DateOnly.FromDateTime(local.Date) != date)
        {
            throw new InvalidDataException($"An event assigned to {date:yyyy-MM-dd} falls outside that Danish date.");
        }

        return local.ToString("HH:mm", CultureInfo.InvariantCulture);
    }
}
