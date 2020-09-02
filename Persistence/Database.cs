using System.Collections.Generic;

namespace PrayerTimes.Persistence
{

    public class APIResult
    {
        public string cityName { get; set; }
        public string content { get; set; }
    }
    public class Database
    {
        public static List<APIResult> api { get; set; }
    }
}
