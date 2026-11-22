using System.Collections.Generic;
using Application.Calendars;

namespace Web.ViewModels;

public sealed record PlaceSearchViewModel(string Query, IReadOnlyList<PrayerPlace> Results, bool IsSpellingSuggestion = false);
