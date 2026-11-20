using System;
using System.Collections.Generic;
using System.Linq;
using Application.Calendars;
using Domain.Models;

namespace Web.ViewModels;

public sealed record CityPrayerTimesViewModel(
    PrayerCity City,
    DateOnly Date,
    IReadOnlyList<DailyPrayerTimes> Month,
    DailyPrayerTimes? Today,
    PrayerEvent? Next,
    string? RequestId)
{
    public bool HasSummerAdjustments => Month.Any(day =>
        day.UsesPredefinedFajrTime == true || day.UsesPredefinedIshaTime);
}
