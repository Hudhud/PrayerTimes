using System.Collections.Generic;
using Web.Services;

namespace Web.ViewModels;

public sealed record PlaceSearchViewModel(string Query, IReadOnlyList<PrayerPlace> Results, bool IsSpellingSuggestion = false);
