using System.ComponentModel.DataAnnotations;

namespace Domain.Models
{
    public class CityPrayerTimes
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string City { get; set; } = null!;

        public ICollection<DailyPrayerTimes> PrayerTimes { get; set; } = new List<DailyPrayerTimes>();
    }
}
