using System.Collections.Generic;

namespace PrayerTimes.Models
{

    public class PrayerViewModel
    {
        public double esha_angle { get; set; }
        public string sunset_date { get; set; }
        public string fajr_date { get; set; }
        public string mithl_time { get; set; }
        public double fea { get; set; }
        public double ln { get; set; }
        public string sunset_time { get; set; }
        public double lt { get; set; }
        public string esha_date { get; set; }
        public string mithlain_date { get; set; }
        public string sunset_tz { get; set; }
        public object esha_time_max { get; set; }
        public double ea { get; set; }
        public string duha_date { get; set; }
        public double rsa { get; set; }
        public string fajr_tz { get; set; }
        public string esha_time { get; set; }
        public object diptype { get; set; }
        public string d { get; set; }
        public object esha_angle_min { get; set; }
        public string mithl_date { get; set; }
        public object esha_angle_max { get; set; }
        public string karaha_tz { get; set; }
        public double p { get; set; }
        public object sunrise_angle_max { get; set; }
        public double t { get; set; }
        public double karaha_angle { get; set; }
        public double k { get; set; }
        public object sunrise_angle_min { get; set; }
        public object esha_time_min { get; set; }
        public string esha_tz { get; set; }
        public double eo { get; set; }
        public double eh { get; set; }
        public object sunrise_time_min { get; set; }
        public object sunrise_time_max { get; set; }
        public string mithlain_tz { get; set; }
        public double duha_angle { get; set; }
        public string sunrise_time { get; set; }
        public string karaha_time { get; set; }
        public double ia { get; set; }
        public object ehtype { get; set; }
        public string zohr_date { get; set; }
        public string mithl_tz { get; set; }
        public string karaha_date { get; set; }
        public object fajr_angle_max { get; set; }
        public object fajr_time_max { get; set; }
        public string duha_time { get; set; }
        public object fajr_time_min { get; set; }
        public string fajr_time { get; set; }
        public string sunrise_date { get; set; }
        public object fajr_angle { get; set; }
        public object fajr_angle_min { get; set; }
        public string zohr_tz { get; set; }
        public double fa { get; set; }
        public string zohr_time { get; set; }
        public string tz { get; set; }
        public string mithlain_time { get; set; }
        public string duha_tz { get; set; }
        public string dtz { get; set; }
        public object sunset_angle_min { get; set; }
        public object sunset_time_min { get; set; }
        public string sunrise_tz { get; set; }
        public object sunset_time_max { get; set; }
        public object sunset_angle_max { get; set; }
    }

    public class Root
    {
        public List<PrayerViewModel> list { get; set; }
    }

}
