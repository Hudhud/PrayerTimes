using System.ComponentModel.DataAnnotations;

namespace Domain.Models
{
    public class DailyPrayerTimes
    {
        [Key]
        public int Id { get; set; }

        public DateTime Date { get; set; }
        public string FajrTime { get; set; } = null!;
        public string SunriseTime { get; set; } = null!;
        public string DhuhrTime { get; set; } = null!;
        public string AsrTime { get; set; } = null!;
        public string AsrHanafiTime { get; set; } = null!;
        public string MaghribTime { get; set; } = null!;
        public string IshaTime { get; set; } = null!;

        public int CityPrayerTimesId { get; set; }
        public CityPrayerTimes CityPrayerTimes { get; set; }
    }

}
