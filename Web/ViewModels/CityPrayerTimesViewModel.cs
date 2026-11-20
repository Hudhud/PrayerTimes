using System;
using System.Collections.Generic;
using System.Linq;
using Application.Calendars;
using Domain.Models;

namespace Web.ViewModels;

public sealed record CityPrayerTimesViewModel(
    PrayerCity City,
    DateTimeOffset Now,
    IReadOnlyList<DailyPrayerTimes> Month,
    DailyPrayerTimes? Today,
    PrayerEvent? Next,
    string? RequestId,
    bool CanSearchPlaces)
{
    public DateOnly Date => DateOnly.FromDateTime(Now.Date);

    public bool HasBrightNightTimes => Month.Any(day => day.FajrTimeUnavailable || day.UsesPredefinedIshaTime);
}
