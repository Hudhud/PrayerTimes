namespace Application.Calendars;

public sealed class DanishTime(TimeProvider clock)
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "Romance Standard Time" : "Europe/Copenhagen");

    public DateTimeOffset Now => TimeZoneInfo.ConvertTime(clock.GetUtcNow(), Zone);

    public static DateTimeOffset FromUtc(DateTime utc) =>
        TimeZoneInfo.ConvertTime(new DateTimeOffset(utc, TimeSpan.Zero), Zone);

    public static DateTimeOffset At(DateOnly date, TimeOnly time)
    {
        var local = date.ToDateTime(time);
        return new DateTimeOffset(local, Zone.GetUtcOffset(local));
    }
}
