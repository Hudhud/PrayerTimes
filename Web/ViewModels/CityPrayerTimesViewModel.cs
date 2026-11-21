using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Application.Calendars;
using Domain.Models;

namespace Web.ViewModels;

public sealed record DailyPrayerTimesViewModel(
    PrayerCity City,
    DateTimeOffset Now,
    DailyPrayerTimes? Day,
    PrayerEvent? Next,
    bool NextAffectedByBrightNights,
    string? RequestId,
    bool Hanafi)
{
    public DateOnly Date => DateOnly.FromDateTime(Now.Date);
    public DateOnly? NextDate => Next?.StartsAt is { } start ? DateOnly.FromDateTime(start.Date) : null;
    public DateTimeOffset Midnight => DanishTime.At(Date.AddDays(1), TimeOnly.MinValue);
    public string AsrMethod => Hanafi ? "hanafi" : "standard";

    public DateTimeOffset RefreshAt
    {
        get
        {
            var midnight = Midnight;
            if (Day is null)
            {
                var retry = Now.AddMinutes(2);
                return retry < midnight ? retry : midnight;
            }
            if (Next?.StartsAt is { } next && next < midnight)
            {
                return next;
            }
            var sunrise = PrayerDay.Sunrise(Day);
            return Next is null && Now < sunrise ? sunrise : midnight;
        }
    }
}

public sealed record CalendarViewModel(
    PrayerCity City,
    DateOnly Today,
    DateOnly Month,
    DateOnly WeekStart,
    IReadOnlyList<DailyPrayerTimes> Days,
    IReadOnlyList<DailyPrayerTimes> WeekDays,
    bool Hanafi)
{
    public static readonly DateOnly MonthLimit = new(9999, 12, 1);
    public DateOnly NextMonth => Month.AddMonths(1);
    public DateOnly? PreviousMonth => Month == DateOnly.MinValue ? null : Month.AddMonths(-1);
    public DateOnly WeekEnd => WeekStart.AddDays(6);
    public DateOnly? PreviousWeek => WeekStart.DayNumber >= 7 ? WeekStart.AddDays(-7) : null;
    public DateOnly? NextWeek => WeekStart.DayNumber + 7 < MonthLimit.DayNumber ? WeekStart.AddDays(7) : null;
    public bool HasBrightNightTimes =>
        Days.Concat(WeekDays).Any(day => day.FajrTimeUnavailable || day.IshaTimeUnavailable);

    public static DateOnly StartOfWeek(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
    public static string MonthParameter(DateOnly date) => date.ToString("yyyy-MM", CultureInfo.InvariantCulture);
    public static string DayParameter(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

public sealed record CityPrayerTimesViewModel(
    DailyPrayerTimesViewModel Daily,
    CalendarViewModel Calendar);

public sealed record PrayerListViewModel(
    DailyPrayerTimes Day, bool Hanafi, bool ShowSunrise = false, bool ExplainBrightNights = false);

public sealed record OfflinePrayerTimesViewModel(
    PrayerCity City, DateOnly Start, IReadOnlyList<DailyPrayerTimes> Days, bool Hanafi)
{
    public DateOnly End => Start.AddDays(6);
}
