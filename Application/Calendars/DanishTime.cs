using Application.Interfaces;

namespace Application.Calendars;

public sealed class DanishTime(IDateTimeProvider clock)
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "Romance Standard Time" : "Europe/Copenhagen");

    public DateTimeOffset Now => FromUtc(clock.UtcNow);

    public static DateTimeOffset FromUtc(DateTime utc) =>
        TimeZoneInfo.ConvertTime(new DateTimeOffset(utc, TimeSpan.Zero), Zone);

    public static DateTimeOffset At(DateOnly date, TimeOnly time)
    {
        var local = date.ToDateTime(time);
        return new DateTimeOffset(local, Zone.GetUtcOffset(local));
    }
}
