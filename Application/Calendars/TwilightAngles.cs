using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Application.Calendars;

public sealed record TwilightAngles
{
    public const int Minimum = 5;
    public const int Maximum = 30;
    public static TwilightAngles Default { get; } = new(18, 17);

    private TwilightAngles(decimal fajr, decimal isha) => (Fajr, Isha) = (fajr, isha);

    public decimal Fajr { get; }
    public decimal Isha { get; }
    public string PreferenceValue => FormattableString.Invariant($"{Fajr:0.#}:{Isha:0.#}");

    public static bool TryParse(string? fajr, string? isha, [NotNullWhen(true)] out TwilightAngles? angles)
    {
        angles = TryRead(fajr, out var morning) && TryRead(isha, out var evening)
            ? new TwilightAngles(morning, evening) : null;
        return angles is not null;
    }

    public static bool TryParsePreference(string? value, [NotNullWhen(true)] out TwilightAngles? angles)
    {
        var parts = value?.Split(':', 2);
        return TryParse(parts is { Length: 2 } ? parts[0] : null, parts is { Length: 2 } ? parts[1] : null, out angles);
    }

    public static TwilightAngles ReadPreference(string? value) =>
        TryParsePreference(value, out var angles) ? angles : Default;

    private static bool TryRead(string? value, out decimal angle) =>
        decimal.TryParse(value?.Trim().Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out angle)
        && angle is >= Minimum and <= Maximum && decimal.Round(angle, 1) == angle;
}
