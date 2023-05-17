using Newtonsoft.Json;

namespace Domain.Models
{
    public class PrayerTimesData
    {
        [JsonProperty("esha_angle")]
        public double EshaAngle { get; set; }

        [JsonProperty("sunset_date")]
        public string SunsetDate { get; set; }

        [JsonProperty("fajr_date")]
        public string FajrDate { get; set; }

        [JsonProperty("mithl_time")]
        public string AsrTime { get; set; }

        [JsonProperty("fea")]
        public double Fea { get; set; }

        [JsonProperty("ln")]
        public double Ln { get; set; }

        [JsonProperty("sunset_time")]
        public string SunsetTime { get; set; }

        [JsonProperty("lt")]
        public double Lt { get; set; }

        [JsonProperty("esha_date")]
        public string EshaDate { get; set; }

        [JsonProperty("mithlain_date")]
        public string MithlainDate { get; set; }

        [JsonProperty("sunset_tz")]
        public string SunsetTz { get; set; }

        [JsonProperty("esha_time_max")]
        public object EshaTimeMax { get; set; }

        [JsonProperty("ea")]
        public double Ea { get; set; }

        [JsonProperty("duha_date")]
        public string DuhaDate { get; set; }

        [JsonProperty("rsa")]
        public double Rsa { get; set; }

        [JsonProperty("fajr_tz")]
        public string FajrTz { get; set; }

        [JsonProperty("esha_time")]
        public string EshaTime { get; set; }

        [JsonProperty("diptype")]
        public object Diptype { get; set; }

        [JsonProperty("d")]
        public string D { get; set; }

        [JsonProperty("esha_angle_min")]
        public object EshaAngleMin { get; set; }

        [JsonProperty("mithl_date")]
        public string MithlDate { get; set; }

        [JsonProperty("esha_angle_max")]
        public object EshaAngleMax { get; set; }

        [JsonProperty("karaha_tz")]
        public string KarahaTz { get; set; }

        [JsonProperty("p")]
        public double P { get; set; }

        [JsonProperty("sunrise_angle_max")]
        public object SunriseAngleMax { get; set; }

        [JsonProperty("t")]
        public double T { get; set; }

        [JsonProperty("karaha_angle")]
        public double KarahaAngle { get; set; }

        [JsonProperty("k")]
        public double K { get; set; }

        [JsonProperty("sunrise_angle_min")]
        public object SunriseAngleMin { get; set; }

        [JsonProperty("esha_time_min")]
        public object EshaTimeMin { get; set; }

        [JsonProperty("esha_tz")]
        public string EshaTz { get; set; }

        [JsonProperty("eo")]
        public double Eo { get; set; }

        [JsonProperty("eh")]
        public double Eh { get; set; }

        [JsonProperty("sunrise_time_min")]
        public object SunriseTimeMin { get; set; }

        [JsonProperty("sunrise_time_max")]
        public object SunriseTimeMax { get; set; }

        [JsonProperty("mithlain_tz")]
        public string MithlainTz { get; set; }

        [JsonProperty("duha_angle")]
        public double DuhaAngle { get; set; }

        [JsonProperty("sunrise_time")]
        public string SunriseTime { get; set; }

        [JsonProperty("karaha_time")]
        public string KarahaTime { get; set; }

        [JsonProperty("ia")]
        public double Ia { get; set; }

        [JsonProperty("ehtype")]
        public object Ehtype { get; set; }

        [JsonProperty("zohr_date")]
        public string ZohrDate { get; set; }

        [JsonProperty("mithl_tz")]
        public string MithlTz { get; set; }

        [JsonProperty("karaha_date")]
        public string KarahaDate { get; set; }

        [JsonProperty("fajr_angle_max")]
        public object FajrAngleMax { get; set; }

        [JsonProperty("fajr_time_max")]
        public object FajrTimeMax { get; set; }

        [JsonProperty("duha_time")]
        public string DuhaTime { get; set; }

        [JsonProperty("fajr_time_min")]
        public object FajrTimeMin { get; set; }

        [JsonProperty("fajr_time")]
        public string FajrTime { get; set; }

        [JsonProperty("sunrise_date")]
        public string SunriseDate { get; set; }

        [JsonProperty("fajr_angle")]
        public object FajrAngle { get; set; }

        [JsonProperty("fajr_angle_min")]
        public object FajrAngleMin { get; set; }

        [JsonProperty("zohr_tz")]
        public string ZohrTz { get; set; }

        [JsonProperty("fa")]
        public double Fa { get; set; }

        [JsonProperty("zohr_time")]
        public string ZohrTime { get; set; }

        [JsonProperty("tz")]
        public string Tz { get; set; }

        [JsonProperty("mithlain_time")]
        public string MithlainTime { get; set; }

        [JsonProperty("duha_tz")]
        public string DuhaTz { get; set; }

        [JsonProperty("dtz")]
        public string Dtz { get; set; }

        [JsonProperty("sunset_angle_min")]
        public object SunsetAngleMin { get; set; }

        [JsonProperty("sunset_time_min")]
        public object SunsetTimeMin { get; set; }

        [JsonProperty("sunrise_tz")]
        public string SunriseTz { get; set; }

        [JsonProperty("sunset_time_max")]
        public object SunsetTimeMax { get; set; }

        [JsonProperty("sunset_angle_max")]
        public object SunsetAngleMax { get; set; }
    }
    public class MuwaqqitResponse
    {
        [JsonProperty("list")]
        public List<PrayerTimesData> PrayerTimesDataList { get; set; }
    }


}