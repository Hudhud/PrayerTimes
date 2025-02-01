using System.ComponentModel.DataAnnotations;

namespace Domain.Models
{
    public class DailyPrayerTimes
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public DateTime Date { get; set; }

        [Required]
        public string FajrTime { get; set; } = null!;

        [Required]
        public string SunriseTime { get; set; } = null!;

        [Required]
        public string DhuhrTime { get; set; } = null!;

        [Required]
        public string AsrTime { get; set; } = null!;

        [Required]
        public string MaghribTime { get; set; } = null!;

        [Required]
        public string IshaTime { get; set; } = null!;

        public int CityPrayerTimesId { get; set; }

        public CityPrayerTimes CityPrayerTimes { get; set; } = null!;
    }
}
