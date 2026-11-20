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
        public string AsrHanafiTime { get; set; } = null!;

        [Required]
        public string MaghribTime { get; set; } = null!;

        [Required]
        public string IshaTime { get; set; } = null!;

        public bool? UsesPredefinedFajrTime { get; set; }

        public int? IshaDayOffset { get; set; }

        public bool UsesPredefinedIshaTime { get; set; }

        // Older rows used 02:00 for the summer exception without storing its flag.
        public bool FajrTimeUnavailable => UsesPredefinedFajrTime ?? FajrTime == "02:00";

        public int CityPrayerTimesId { get; set; }

        public CityPrayerTimes CityPrayerTimes { get; set; } = null!;
    }
}
