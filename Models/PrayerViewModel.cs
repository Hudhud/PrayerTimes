using System;
using System.Collections.Generic;


namespace PrayerTimes.Models
{

    public class PrayerViewModel
    {
        public double esha_angle { get; set; }
        public DateTime sunset_date { get; set; }
        public DateTime fajr_date { get; set; }
        public string mithl_time { get; set; }
        public double fea { get; set; }
        public double ln { get; set; }
        public string sunset_time { get; set; }
        public double lt { get; set; }
        public DateTime? esha_date { get; set; }
        public DateTime mithlain_date { get; set; }
        public string sunset_tz { get; set; }
        public string esha_time_max { get; set; }
        public double ea { get; set; }
        public DateTime duha_date { get; set; }
        public double rsa { get; set; }
        public string fajr_tz { get; set; }
        public string esha_time { get; set; }
        public string diptype { get; set; }
        public DateTime d { get; set; }
        public string esha_angle_min { get; set; }
        public DateTime mithl_date { get; set; }
        public string esha_angle_max { get; set; }
        public string karaha_tz { get; set; }
        public double p { get; set; }
        public string sunrise_angle_max { get; set; }
        public double t { get; set; }
        public double karaha_angle { get; set; }
        public double k { get; set; }
        public string sunrise_angle_min { get; set; }
        public string esha_time_min { get; set; }
        public string esha_tz { get; set; }
        public double eo { get; set; }
        public double eh { get; set; }
        public string sunrise_time_min { get; set; }
        public string sunrise_time_max { get; set; }
        public string mithlain_tz { get; set; }
        public double duha_angle { get; set; }
        public string sunrise_time { get; set; }
        public string karaha_time { get; set; }
        public double ia { get; set; }
        public string ehtype { get; set; }
        public DateTime zohr_date { get; set; }
        public string mithl_tz { get; set; }
        public DateTime karaha_date { get; set; }
        public string fajr_angle_max { get; set; }
        public string fajr_time_max { get; set; }
        public string duha_time { get; set; }
        public string fajr_time_min { get; set; }
        public string fajr_time { get; set; }
        public DateTime sunrise_date { get; set; }
        public string? fajr_angle { get; set; }
        public string fajr_angle_min { get; set; }
        public string zohr_tz { get; set; }
        public double fa { get; set; }
        public string zohr_time { get; set; }
        public string tz { get; set; }
        public string mithlain_time { get; set; }
        public string duha { get; set; }
        public string duha_tz { get; set; }
        public string dtz { get; set; }
        public string sunset_angle_min { get; set; }
        public string sunset_time_min { get; set; }
        public string sunrise_tz { get; set; }
        public string sunset_time_max { get; set; }
        public string sunset_angle_max { get; set; }
    }
    public class Root
    {
        public List<PrayerViewModel> list { get; set; }
    }

}
