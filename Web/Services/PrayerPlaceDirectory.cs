using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Application.Calendars;

namespace Web.Services;

public sealed record PrayerPlace(string Id, string Name, string Area, double Latitude, double Longitude,
    string? PostalCode = null)
{
    public string Label => PostalCode is null ? Name : $"{PostalCode} {Name}";

    public PrayerCity ToPrayerCity() => new(Id, Label, Id,
        Latitude.ToString(CultureInfo.InvariantCulture), Longitude.ToString(CultureInfo.InvariantCulture));
}

public sealed class PrayerPlaceDirectory
{
    private readonly Dictionary<string, PrayerPlace> places;
    private readonly (PrayerPlace Place, string SearchText)[] searchable;

    public PrayerPlaceDirectory(string? path)
    {
        var locations = Array.Empty<PrayerPlace>();
        if (path is not null)
        {
            using var data = File.OpenRead(path);
            locations = JsonSerializer.Deserialize<PrayerPlace[]>(data, JsonSerializerOptions.Web)
                ?? throw new InvalidDataException("The Danish place index is missing.");
            if (locations.Length == 0)
            {
                throw new InvalidDataException("The Danish place index is empty.");
            }
        }

        places = locations.ToDictionary(place => place.Id, StringComparer.OrdinalIgnoreCase);
        searchable = locations.Select(place => (place, Normalize(place.Label))).ToArray();
    }

    public bool IsEnabled => places.Count > 0;

    public PrayerCity? Find(string? slug)
    {
        var established = PrayerCities.Find(slug);
        return established ?? (slug is not null && places.TryGetValue(slug, out var place)
            ? place.ToPrayerCity()
            : null);
    }

    public IReadOnlyList<PrayerPlace> Search(string query)
    {
        var text = Normalize(query.Trim());
        if (text.Length < 2 || text.Length > 80)
        {
            return [];
        }

        var isPostalCode = char.IsAsciiDigit(text[0]);
        return searchable
            .Where(entry => (entry.Place.PostalCode is not null) == isPostalCode
                && entry.SearchText.Contains(text, StringComparison.Ordinal))
            .OrderBy(entry => entry.SearchText == text ? 0 : entry.SearchText.StartsWith(text, StringComparison.Ordinal) ? 1 : 2)
            .ThenBy(entry => entry.Place.Label, StringComparer.Create(CultureInfo.GetCultureInfo("da-DK"), true))
            .Take(25)
            .Select(entry => entry.Place)
            .ToArray();
    }

    private static string Normalize(string text)
    {
        var normalized = text.ToLowerInvariant().Replace("æ", "ae").Replace("ø", "oe").Replace("å", "aa")
            .Normalize(NormalizationForm.FormD);
        return string.Concat(normalized.Where(character =>
            CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark));
    }
}
