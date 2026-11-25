namespace Application.Calendars;

public sealed class DanishTime(TimeProvider clock)
{
    public const string ZoneId = "Europe/Copenhagen";

    public DateTimeOffset Now => NowIn(ZoneId);

    public DateTimeOffset NowIn(string zoneId) => LocalTime.FromUtc(clock.GetUtcNow(), zoneId);

    public static DateTimeOffset FromUtc(DateTime utc) => LocalTime.FromUtc(utc, ZoneId);

    public static DateTimeOffset At(DateOnly date, TimeOnly time) => LocalTime.At(date, time, ZoneId);
}
