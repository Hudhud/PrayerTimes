using System.Text.Json;
using System.Text.Json.Serialization;

namespace Infrastructure.DTO;

public sealed record MuwaqqitResponse([property: JsonPropertyName("list")] List<MuwaqqitDay>? Days);

public sealed record MuwaqqitDay(
    [property: JsonPropertyName("fajr_date")] string? FajrDate,
    [property: JsonPropertyName("fajr_time")] string? FajrTime,
    [property: JsonPropertyName("fajr_angle")] JsonElement FajrAngle,
    [property: JsonPropertyName("sunrise_time")] string? Sunrise,
    [property: JsonPropertyName("zohr_time")] string? Dhuhr,
    [property: JsonPropertyName("mithl_time")] string? Asr,
    [property: JsonPropertyName("mithlain_time")] string? AsrHanafi,
    [property: JsonPropertyName("sunset_time")] string? Maghrib,
    [property: JsonPropertyName("esha_date")] string? IshaDate,
    [property: JsonPropertyName("esha_time")] string? IshaTime);
