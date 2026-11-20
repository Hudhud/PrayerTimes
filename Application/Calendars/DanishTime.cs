using Application.Interfaces;

namespace Application.Calendars;

public sealed class DanishTime(IDateTimeProvider clock)
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "Romance Standard Time" : "Europe/Copenhagen");

    public DateTimeOffset Now => TimeZoneInfo.ConvertTime(new DateTimeOffset(clock.UtcNow, TimeSpan.Zero), Zone);

    public static DateTimeOffset At(DateOnly date, TimeOnly time)
    {
        var local = date.ToDateTime(time);
        return new DateTimeOffset(local, Zone.GetUtcOffset(local));
    }
}
