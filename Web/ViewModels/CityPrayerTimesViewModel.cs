using System;
using System.Collections.Generic;

namespace Web.ViewModels
{
    public class CityPrayerTimesViewModel
    {
        public string City { get; set; }
        public List<DailyPrayerTimesViewModel> PrayerTimes { get; set; }
    }

    public class DailyPrayerTimesViewModel
    {
        public DateTime Date { get; set; }
        public string FajrTime { get; set; } = null!;
        public string SunriseTime { get; set; } = null!;
        public string DhuhrTime { get; set; } = null!;
        public string AsrTime { get; set; } = null!;
        public string AsrHanafiTime { get; set; } = null!;
        public string MaghribTime { get; set; } = null!;
        public string IshaTime { get; set; } = null!;
    }

}
