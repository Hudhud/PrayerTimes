using System.Globalization;

public class PrayerTimesHelper
{
    public static string AddMinutesAndConvertToString(string inputDateTimeString, int minutesToAdd)
    {
        var inputDateTime = DateTime.ParseExact(inputDateTimeString, "HH:mm:ss", CultureInfo.InvariantCulture);
        var adjustedDateTime = inputDateTime.AddMinutes(minutesToAdd);
        return adjustedDateTime.ToString("HH:mm");
    }
}
