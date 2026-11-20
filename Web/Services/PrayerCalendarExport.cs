using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Application.Calendars;
using Domain.Models;
using Web.ViewModels;

namespace Web.Services;

internal static class PrayerCalendarExport
{
    internal static byte[] Create(PrayerCity city, IReadOnlyList<DailyPrayerTimes> days, bool hanafi, DateTimeOffset now)
    {
        var calendar = new StringBuilder("BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:-//Salah Times//Bønnetider//DA\r\nCALSCALE:GREGORIAN\r\n");
        var stamp = FormatUtc(now);
        foreach (var day in days)
        {
            var date = CalendarViewModel.DayParameter(DateOnly.FromDateTime(day.Date));
            foreach (var prayer in PrayerDay.Events(day, hanafi))
            {
                if (prayer.StartsAt is not { } start)
                {
                    continue;
                }

                calendar.Append("BEGIN:VEVENT\r\n")
                    .Append($"UID:{city.Slug}-{date}-{prayer.Name.ToLowerInvariant()}@salahtimes.dk\r\n")
                    .Append($"DTSTAMP:{stamp}\r\nDTSTART:{FormatUtc(start)}\r\n")
                    .Append($"SUMMARY:{prayer.Name}\r\n");
                AppendLine(calendar, "LOCATION:" + EscapeText(city.Name));
                calendar.Append("END:VEVENT\r\n");
            }
        }

        calendar.Append("END:VCALENDAR\r\n");
        return Encoding.UTF8.GetBytes(calendar.ToString());
    }

    private static string FormatUtc(DateTimeOffset time) =>
        time.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    private static string EscapeText(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\r\n", "\\n", StringComparison.Ordinal).Replace("\r", "\\n", StringComparison.Ordinal)
        .Replace("\n", "\\n", StringComparison.Ordinal).Replace(";", "\\;", StringComparison.Ordinal)
        .Replace(",", "\\,", StringComparison.Ordinal);

    private static void AppendLine(StringBuilder target, string value)
    {
        var bytes = 0;
        foreach (var rune in value.EnumerateRunes())
        {
            if (bytes + rune.Utf8SequenceLength > 75)
            {
                target.Append("\r\n ");
                bytes = 1;
            }

            target.Append(rune.ToString());
            bytes += rune.Utf8SequenceLength;
        }
        target.Append("\r\n");
    }
}
