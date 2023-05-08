using MySql.Data.MySqlClient;

namespace PrayerTimes.Models
{
    public class PrayeTimesContext
    {
        public string ConnectionString { get; set; }
        public MySqlConnection Connection;

        public PrayeTimesContext(string connectionString)
        {
            this.ConnectionString = connectionString;
            Connection = new MySqlConnection(connectionString);
        }
    }
}
