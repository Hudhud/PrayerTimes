using System.Globalization;
using Domain.Models;

namespace Application.Calendars;

public sealed record PrayerEvent(string Name, DateTimeOffset? StartsAt)
{
    public string? Time => StartsAt?.ToString("HH:mm", CultureInfo.InvariantCulture);
}

public static class PrayerDay
{
    public static IReadOnlyList<PrayerEvent> Events(DailyPrayerTimes day, bool hanafi = false)
    {
        var date = DateOnly.FromDateTime(day.Date);
        TimeOnly? fajr = day.FajrTimeUnavailable ? null : Read(day.FajrTime);
        var sunrise = Read(day.SunriseTime);
        var dhuhr = Read(day.DhuhrTime);
        var asr = Read(hanafi ? day.AsrHanafiTime : day.AsrTime);
        var maghrib = Read(day.MaghribTime);
        TimeOnly? isha = day.IshaTimeUnavailable ? null : Read(day.IshaTime);
        var ishaDayOffset = day.IshaDayOffset;

        if (fajr >= sunrise || !(sunrise < dhuhr && dhuhr < asr && asr < maghrib)
            || (isha is { } ishaTime && (ishaDayOffset is not (0 or 1)
                || (ishaDayOffset == 0 && ishaTime <= maghrib)
                || (ishaDayOffset == 1 && ishaTime >= maghrib))))
        {
            throw new InvalidDataException("The timetable contains out-of-order prayer times.");
        }

        return
        [
            new("Fajr", fajr is { } fajrTime ? DanishTime.At(date, fajrTime) : null),
            new("Dhuhr", DanishTime.At(date, dhuhr)),
            new("Asr", DanishTime.At(date, asr)),
            new("Maghrib", DanishTime.At(date, maghrib)),
            new("Isha", isha is { } regularIsha && ishaDayOffset is { } offset ? DanishTime.At(date.AddDays(offset), regularIsha) : null)
        ];
    }

    public static PrayerEvent? FindNextPrayer(
        IReadOnlyDictionary<DateOnly, DailyPrayerTimes> days,
        DateOnly today, DateTimeOffset now, bool hanafi = false)
    {
        if (!days.TryGetValue(today, out var currentDay))
        {
            return null;
        }

        var previousIsha = days.TryGetValue(today.AddDays(-1), out var previousDay)
            ? Events(previousDay, hanafi)[^1]
            : null;
        var prayers = Events(currentDay, hanafi);

        // Leave the next prayer unspecified while an overnight prayer may still be due.
        if (now < (prayers[0].StartsAt ?? Sunrise(currentDay))
            && (prayers[0].StartsAt is null || previousIsha is { StartsAt: null }))
        {
            return null;
        }

        if (previousIsha?.StartsAt > now)
        {
            return previousIsha;
        }

        var next = prayers.FirstOrDefault(prayer => prayer.StartsAt > now);
        if (next is not null || prayers[^1].StartsAt is null)
        {
            return next;
        }

        if (days.TryGetValue(today.AddDays(1), out var tomorrow))
        {
            var tomorrowFajr = Events(tomorrow, hanafi)[0];
            return tomorrowFajr.StartsAt > now ? tomorrowFajr : null;
        }

        return null;
    }

    public static DateTimeOffset Sunrise(DailyPrayerTimes day) =>
        DanishTime.At(DateOnly.FromDateTime(day.Date), Read(day.SunriseTime));

    public static bool IsUsable(DailyPrayerTimes day, bool hanafi = false)
    {
        try
        {
            Events(day, hanafi);
            return true;
        }
        catch (InvalidDataException)
        {
            return false;
        }
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
