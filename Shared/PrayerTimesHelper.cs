using System.Globalization;

public static class PrayerTimesHelper
{
    public static TimeOnly AdjustTime(string? value, int minutes, out int days)
    {
        if (!TimeOnly.TryParseExact(value, "HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        {
            throw new InvalidDataException("The timetable contains a missing or malformed time.");
        }

        return time.AddMinutes(minutes, out days);
    }
}
