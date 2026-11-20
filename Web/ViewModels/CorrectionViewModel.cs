using System;
using Application.Calendars;

namespace Web.ViewModels;

public sealed record CorrectionViewModel(PrayerCity City, DateOnly Date, string Prayer);
