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
    private static readonly StringComparer DanishComparer = StringComparer.Create(CultureInfo.GetCultureInfo("da-DK"), true);
    private readonly (PrayerPlace Place, string Label, string FoldedLabel, string Area)[] searchable;

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
        searchable = locations.Select(place => (place, Normalize(place.Label), Fold(place.Label), Fold(place.Area))).ToArray();
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
        var input = query.Trim();
        var text = Normalize(input);
        if (text.Length < 2 || text.Length > 80)
        {
            return [];
        }

        if (text == "kbh")
        {
            input = "København";
            text = "koebenhavn";
        }

        var isPostalCode = char.IsAsciiDigit(text[0]);
        var folded = Fold(input);
        var words = folded.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return searchable
            .Where(entry => (entry.Place.PostalCode is not null) == isPostalCode)
            .Select(entry => (entry.Place, Rank: Rank(entry.Label, entry.FoldedLabel, entry.Area, text, folded, words, isPostalCode)))
            .Where(entry => entry.Rank >= 0)
            .OrderBy(entry => entry.Rank)
            .ThenBy(entry => entry.Place.Label, DanishComparer)
            .ThenBy(entry => entry.Place.Area, DanishComparer)
            .Take(25)
            .Select(entry => entry.Place)
            .ToArray();
    }

    private static int Rank(string label, string foldedLabel, string area, string query, string folded, string[] words, bool isPostalCode)
    {
        if (label == query)
        {
            return 0;
        }
        if (foldedLabel == folded)
        {
            return 1;
        }
        if (label.StartsWith(query, StringComparison.Ordinal) || foldedLabel.StartsWith(folded, StringComparison.Ordinal))
        {
            return 2;
        }
        if (isPostalCode)
        {
            return -1;
        }
        if (label.Contains(query, StringComparison.Ordinal) || foldedLabel.Contains(folded, StringComparison.Ordinal))
        {
            return 3;
        }
        return words.Length > 1 && foldedLabel.StartsWith(words[0], StringComparison.Ordinal) &&
            words.Skip(1).All(word => foldedLabel.Contains(word, StringComparison.Ordinal) || area.Contains(word, StringComparison.Ordinal))
            ? 4 : -1;
    }

    private static string Normalize(string text) => RemoveMarks(text.Normalize().ToLowerInvariant()
        .Replace("æ", "ae").Replace("ø", "oe").Replace("å", "aa"));

    private static string Fold(string text) => RemoveMarks(text.ToLowerInvariant().Replace("æ", "ae").Replace("ø", "o"));

    private static string RemoveMarks(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        return string.Concat(normalized.Where(character =>
            CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark));
    }
}
