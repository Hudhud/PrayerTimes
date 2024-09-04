namespace Application.DTOs
{
    public class CityPrayerTimesDTO
    {
        public string City { get; set; } = string.Empty;
        public List<DailyPrayerTimesDTO> PrayerTimes { get; set; } = new List<DailyPrayerTimesDTO>();
    }
}
