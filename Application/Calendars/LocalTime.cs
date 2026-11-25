namespace Application.Calendars;

public static class LocalTime
{
    public static DateTimeOffset FromUtc(DateTimeOffset utc, string zoneId) =>
        TimeZoneInfo.ConvertTime(utc, TimeZoneInfo.FindSystemTimeZoneById(zoneId));

    public static DateTimeOffset At(DateOnly date, TimeOnly time, string zoneId)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        var local = date.ToDateTime(time);
        // A civil day can begin after midnight when the offset changes.
        while (zone.IsInvalidTime(local))
        {
            local = local.AddMinutes(1);
        }
        var offset = zone.IsAmbiguousTime(local) ? zone.GetAmbiguousTimeOffsets(local).Max() : zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset);
    }
}
