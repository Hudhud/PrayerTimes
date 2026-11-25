using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Application.Calendars;

public sealed record PrayerPlace(string Id, string Name, string Area, double Latitude, double Longitude,
    string? PostalCode = null, string TimeZoneId = DanishTime.ZoneId, string CountryCode = "DK",
    string? AlternateName = null, long Population = 0)
{
    public string Label => PostalCode is null ? Name : $"{PostalCode} {Name}";

    public PrayerCity ToPrayerCity() => new(Id, Label,
        Latitude.ToString(CultureInfo.InvariantCulture), Longitude.ToString(CultureInfo.InvariantCulture), TimeZoneId, CountryCode);
}

public sealed class PrayerPlaceDirectory
{
    private readonly Dictionary<string, PrayerPlace> places;
    private static readonly StringComparer DanishComparer = StringComparer.Create(CultureInfo.GetCultureInfo("da-DK"), true);
    private readonly (PrayerPlace Place, string Label, string FoldedLabel, string Area, string? Alternate)[] searchable;

    public static PrayerPlaceDirectory Load(string path, string? worldwidePath = null)
    {
        using var data = File.OpenRead(path);
        using var worldwide = worldwidePath is null ? null : File.OpenRead(worldwidePath);
        return new PrayerPlaceDirectory(data, worldwide);
    }

    public PrayerPlaceDirectory(Stream data, Stream? worldwide = null)
    {
        var locations = JsonSerializer.Deserialize(data, PrayerPlaceJsonContext.Default.PrayerPlaceArray);
        if (locations is not { Length: > 0 })
        {
            throw new InvalidDataException("The Danish place index is empty.");
        }

        if (worldwide is not null)
        {
            var cities = JsonSerializer.Deserialize(worldwide, PrayerPlaceJsonContext.Default.PrayerPlaceArray)
                ?? throw new InvalidDataException("The worldwide city index is empty.");
            locations = [.. locations, .. cities];
        }
        places = locations.ToDictionary(place => place.Id, StringComparer.OrdinalIgnoreCase);
        searchable = locations.Select(place => (place, Normalize(place.Label), Fold(place.Label), Fold(place.Area),
            place.AlternateName is { } name ? Fold(name) : null)).ToArray();
    }

    public PrayerCity? Find(string? slug)
    {
        var established = PrayerCities.Find(slug);
        return established ?? (slug is not null && places.TryGetValue(slug, out var place)
            ? place.ToPrayerCity()
            : null);
    }

    public PrayerCity? FindNearestCity(double latitude, double longitude)
    {
        if (!double.IsFinite(latitude) || latitude is < -90 or > 90 ||
            !double.IsFinite(longitude) || longitude is < -180 or > 180)
        {
            return null;
        }

        const double radians = Math.PI / 180;
        const double earthRadiusKm = 6371;
        const double maxDistanceKm = 50;
        var radius = Math.Sin(maxDistanceKm / (2 * earthRadiusKm));
        var nearestDistance = radius * radius;
        var latitudeCosine = Math.Cos(latitude * radians);
        PrayerPlace? nearest = null;
        var localLimit = Math.Sin(30 / (2 * earthRadiusKm));
        foreach (var candidate in places.Values)
        {
            if (candidate.PostalCode is not null)
            {
                continue;
            }

            var latitudeSine = Math.Sin((candidate.Latitude - latitude) * radians / 2);
            var longitudeSine = Math.Sin((candidate.Longitude - longitude) * radians / 2);
            var distance = latitudeSine * latitudeSine +
                latitudeCosine * Math.Cos(candidate.Latitude * radians) * longitudeSine * longitudeSine;
            if (distance < nearestDistance && (candidate.CountryCode != "DK" || distance < localLimit * localLimit))
            {
                nearestDistance = distance;
                nearest = candidate;
            }
        }

        return nearest is null ? null : Find(nearest.Id);
    }

    public IReadOnlyList<PrayerPlace> Search(string query)
    {
        var input = query.Trim();
        var text = Normalize(input);
        if (text.Length < 2 || text.Length > 80)
        {
            return [];
        }

        if (text is "kbh" or "copenhagen")
        {
            input = "København";
            text = "koebenhavn";
        }

        var isPostalCode = char.IsAsciiDigit(text[0]);
        var folded = Fold(input);
        var words = folded.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return searchable
            .Where(entry => (entry.Place.PostalCode is not null) == isPostalCode)
            .Select(entry => (entry.Place, Rank: Rank(entry.Label, entry.FoldedLabel, entry.Area, entry.Alternate, text, folded, words, isPostalCode)))
            .Where(entry => entry.Rank >= 0)
            .OrderBy(entry => entry.Rank)
            .ThenBy(entry => entry.Place.CountryCode != "DK")
            .ThenByDescending(entry => entry.Place.Population)
            .ThenBy(entry => entry.Place.Label, DanishComparer)
            .ThenBy(entry => entry.Place.Area, DanishComparer)
            .Take(25)
            .Select(entry => entry.Place)
            .ToArray();
    }

    public IReadOnlyList<PrayerPlace> SuggestSpelling(string query)
    {
        var input = query.Trim();
        if (input.Length is < 5 or > 80 || !char.IsLetter(input[0]))
        {
            return [];
        }

        var label = Normalize(input);
        var folded = Fold(input);
        return searchable
            .Where(entry => entry.Place.PostalCode is null &&
                (HasOneEdit(folded, entry.FoldedLabel) || HasOneEdit(label, entry.Label)))
            .OrderBy(entry => entry.Place.Label, DanishComparer)
            .ThenBy(entry => entry.Place.Area, DanishComparer)
            .Take(6)
            .Select(entry => entry.Place)
            .ToArray();
    }

    private static bool HasOneEdit(string first, string second)
    {
        if (first.Length > second.Length)
        {
            (first, second) = (second, first);
        }
        if (second.Length - first.Length > 1)
        {
            return false;
        }

        var index = 0;
        while (index < first.Length && first[index] == second[index])
        {
            index++;
        }
        if (first.Length < second.Length)
        {
            return first.AsSpan(index).SequenceEqual(second.AsSpan(index + 1));
        }
        if (index == first.Length)
        {
            return false;
        }

        // Compare the suffix after one replaced letter or two swapped neighbors.
        return first.AsSpan(index + 1).SequenceEqual(second.AsSpan(index + 1)) ||
            (index + 1 < first.Length && first[index] == second[index + 1] && first[index + 1] == second[index] &&
                first.AsSpan(index + 2).SequenceEqual(second.AsSpan(index + 2)));
    }

    private static int Rank(string label, string foldedLabel, string area, string? alternate, string query, string folded, string[] words, bool isPostalCode)
    {
        if (label == query)
        {
            return 0;
        }
        if (foldedLabel == folded || alternate == folded)
        {
            return 1;
        }
        if (label.StartsWith(query, StringComparison.Ordinal) || foldedLabel.StartsWith(folded, StringComparison.Ordinal) || alternate?.StartsWith(folded, StringComparison.Ordinal) == true)
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

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(PrayerPlace[]))]
internal partial class PrayerPlaceJsonContext : JsonSerializerContext;
