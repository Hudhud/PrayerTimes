using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<CityPrayerTimes> CityPrayerTimes { get; set; }
        public DbSet<DailyPrayerTimes> DailyPrayerTimes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<CityPrayerTimes>()
                .HasIndex(c => c.City)
                .IsUnique();

            modelBuilder.Entity<CityPrayerTimes>()
                .HasMany(c => c.PrayerTimes)
                .WithOne(d => d.CityPrayerTimes)
                .HasForeignKey(d => d.CityPrayerTimesId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
