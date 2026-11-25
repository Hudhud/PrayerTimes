namespace Domain.Models;

public sealed class DailyPrayerTimes
{
    public required DateTime Date { get; init; }
    public required string FajrTime { get; init; }
    public required string SunriseTime { get; init; }
    public required string DhuhrTime { get; init; }
    public required string AsrTime { get; init; }
    public required string AsrHanafiTime { get; init; }
    public required string MaghribTime { get; init; }
    public required string IshaTime { get; init; }
    public bool FajrTimeUnavailable { get; init; }
    public bool IshaTimeUnavailable { get; init; }
    public int? IshaDayOffset { get; init; }
    public PrayerInstants? Instants { get; init; }
    public bool HasUnavailableSunEvents => string.IsNullOrEmpty(SunriseTime) || string.IsNullOrEmpty(MaghribTime)
        || string.IsNullOrEmpty(AsrTime) || string.IsNullOrEmpty(AsrHanafiTime);
    public bool HasUnavailableTimes => FajrTimeUnavailable || IshaTimeUnavailable || HasUnavailableSunEvents;

}

public sealed record PrayerInstants(DateTimeOffset? Fajr, DateTimeOffset? Sunrise, DateTimeOffset? Dhuhr,
    DateTimeOffset? Asr, DateTimeOffset? AsrHanafi, DateTimeOffset? Maghrib, DateTimeOffset? Isha);
