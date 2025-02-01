namespace Infrastructure.DTO
{
    public class AladhanApiResponse
    {
        public List<AladhanData> Data { get; set; }
    }

    public class AladhanData
    {
        public AladhanTimings Timings { get; set; }
        public AladhanDate Date { get; set; }
    }

    public class AladhanTimings
    {
        public string Fajr { get; set; }
        public string Sunrise { get; set; }
        public string Dhuhr { get; set; }
        public string Asr { get; set; }
        public string Maghrib { get; set; }
        public string Isha { get; set; }
    }

    public class AladhanDate
    {
        public AladhanGregorian Gregorian { get; set; }
    }

    public class AladhanGregorian
    {
        public string Date { get; set; }
    }
}