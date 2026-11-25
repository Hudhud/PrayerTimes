using System.Collections.Generic;
using Application.Calendars;

namespace Web.ViewModels;

public sealed record PlaceSearchViewModel(string Query, IReadOnlyList<PrayerPlace> Results, bool IsSpellingSuggestion = false,
    bool ReturnToSettings = false);

public sealed record PlacePickerViewModel(string Query, bool ReturnToSettings = false);
