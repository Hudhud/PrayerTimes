using System.ComponentModel.DataAnnotations;

namespace Domain.Models
{
    public class CityPrayerTimes
    {
        [Key]
        public int Id { get; set; }

        public string City { get; set; } = null!;

        public ICollection<DailyPrayerTimes> DailyPrayerTimesList { get; set; } = new List<DailyPrayerTimes>();
    }
}
