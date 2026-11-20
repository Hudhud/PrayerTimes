using System.Globalization;
using Domain.Models;

namespace Application.Calendars;

public sealed record PrayerEvent(string Name, DateTimeOffset StartsAt, bool IsAdjusted = false)
{
    public string Time => StartsAt.ToString("HH:mm", CultureInfo.InvariantCulture);
}

public static class PrayerDay
{
    public static IReadOnlyList<PrayerEvent> Events(DailyPrayerTimes day)
    {
        var date = DateOnly.FromDateTime(day.Date);
        var fajr = Read(day.FajrTime);
        var sunrise = Read(day.SunriseTime);
        var dhuhr = Read(day.DhuhrTime);
        var asr = Read(day.AsrTime);
        var maghrib = Read(day.MaghribTime);
        var isha = Read(day.IshaTime);
        var ishaDayOffset = day.IshaDayOffset ?? (isha < maghrib ? 1 : 0);

        if (!(fajr < sunrise && sunrise < dhuhr && dhuhr < asr && asr < maghrib)
            || ishaDayOffset is < 0 or > 1 || (ishaDayOffset == 0 && isha <= maghrib)
            || (ishaDayOffset == 1 && isha >= maghrib))
        {
            throw new InvalidDataException("The timetable contains out-of-order prayer times.");
        }

        return
        [
            new("Fajr", DanishTime.At(date, fajr), day.UsesPredefinedFajrTime == true),
            new("Dhuhr", DanishTime.At(date, dhuhr)),
            new("Asr", DanishTime.At(date, asr)),
            new("Maghrib", DanishTime.At(date, maghrib)),
            new("Isha", DanishTime.At(date.AddDays(ishaDayOffset), isha), day.UsesPredefinedIshaTime)
        ];
    }

    public static bool IsUsable(DailyPrayerTimes day)
    {
        try
        {
            Events(day);
            return true;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    public static bool IsCompleteMonth(DateOnly month, IReadOnlyList<DailyPrayerTimes> days)
    {
        var expectedDays = DateTime.DaysInMonth(month.Year, month.Month);

        return days.Count == expectedDays && days.Select((day, index) =>
            DateOnly.FromDateTime(day.Date) == month.AddDays(index) && IsUsable(day)).All(valid => valid);
    }

    private static TimeOnly Read(string? value)
    {
        if (!TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        {
            throw new InvalidDataException("The timetable contains a missing or malformed time.");
        }

        return time;
    }
}
