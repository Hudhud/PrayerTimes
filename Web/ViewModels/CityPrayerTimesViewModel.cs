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
    PrayerEvent? Current,
    PrayerEvent? Next,
    bool NextAffectedByBrightNights,
    string? RequestId,
    bool Hanafi,
    TwilightAngles Angles)
{
    public DateOnly Date => DateOnly.FromDateTime(Now.Date);
    public DateOnly? NextDate => Next?.StartsAt is { } start ? DateOnly.FromDateTime(start.Date) : null;
    public DateTimeOffset Midnight => LocalTime.At(Date.AddDays(1), TimeOnly.MinValue, City.TimeZoneId);
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
            var sunrise = PrayerDay.AvailableSunrise(Day);
            var refresh = Next?.StartsAt is { } next && next < midnight ? next : midnight;
            return sunrise is { } rising && Now < rising && rising < refresh && (Current?.Name == "Fajr" || Next is null) ? rising : refresh;
        }
    }
}

public sealed record CalendarViewModel(
    PrayerCity City,
    DateOnly Today,
    DateOnly Month,
    DateOnly? SelectedDate,
    IReadOnlyList<DailyPrayerTimes> Days,
    bool Hanafi,
    TwilightAngles Angles)
{
    public static readonly DateOnly MonthLimit = new(9999, 12, 1);
    public DateOnly NextMonth => Month.AddMonths(1);
    public DateOnly? PreviousMonth => Month == DateOnly.MinValue ? null : Month.AddMonths(-1);
    public bool HasBrightNightTimes =>
        Days.Any(day => day.HasUnavailableTimes);

    public static string MonthParameter(DateOnly date) => date.ToString("yyyy-MM", CultureInfo.InvariantCulture);
    public static string DayParameter(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

public sealed record CityPrayerTimesViewModel(
    DailyPrayerTimesViewModel Daily,
    CalendarViewModel Calendar,
    bool IsDefaultPlace);

public sealed record PrayerListViewModel(
    DailyPrayerTimes Day, bool Hanafi, bool ShowSunrise = false, bool ExplainBrightNights = false,
    bool InlineSunrise = false, PrayerEvent? Current = null, PrayerEvent? Next = null);

public sealed record OfflinePrayerTimesViewModel(
    PrayerCity City, DateOnly Start, IReadOnlyList<DailyPrayerTimes> Days, bool Hanafi, TwilightAngles Angles)
{
    public DateOnly End => Start.AddDays(6);
}
