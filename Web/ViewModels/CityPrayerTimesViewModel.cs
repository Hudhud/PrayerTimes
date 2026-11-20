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
    public bool HasBrightNightTimes => Day is not null && (Day.FajrTimeUnavailable || Day.IshaTimeUnavailable);

    public DateTimeOffset RefreshAt
    {
        get
        {
            var midnight = DanishTime.At(Date.AddDays(1), TimeOnly.MinValue);
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
    bool Hanafi)
{
    public DateOnly NextMonth => Month.AddMonths(1);
    public DateOnly? PreviousMonth => Month == DateOnly.MinValue ? null : Month.AddMonths(-1);
    public DateOnly? PreviousWeek => WeekStart > Month
        ? DateOnly.FromDayNumber(Math.Max(Month.DayNumber, WeekStart.DayNumber - 7)) : null;
    public DateOnly? NextWeek => WeekStart.DayNumber + 7 < NextMonth.DayNumber ? WeekStart.AddDays(7) : null;
    public bool HasBrightNightTimes => Days.Any(day => day.FajrTimeUnavailable || day.IshaTimeUnavailable);
    public IEnumerable<DailyPrayerTimes> WeekDays =>
        Days.Where(day => DateOnly.FromDateTime(day.Date).DayNumber - WeekStart.DayNumber is >= 0 and < 7);

    public static DateOnly StartOfWeek(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
    public static string MonthParameter(DateOnly date) => date.ToString("yyyy-MM", CultureInfo.InvariantCulture);
    public static string DayParameter(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

public sealed record CityPrayerTimesViewModel(
    DailyPrayerTimesViewModel Daily,
    CalendarViewModel Calendar);

public sealed record PrayerListViewModel(DailyPrayerTimes Day, bool Hanafi, bool ShowSunrise = false);
