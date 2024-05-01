using Domain.Models;
using Domain.Repositories;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class CityPrayerTimesRepository : ICityPrayerTimesRepository
    {
        private readonly ApplicationDbContext _context;

        public CityPrayerTimesRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task AddOrUpdateAsync(CityPrayerTimes cityPrayerTimes)
        {
            var existingCity = await _context.CityPrayerTimes
                                             .FirstOrDefaultAsync(c => c.City.Equals(cityPrayerTimes.City, StringComparison.OrdinalIgnoreCase));

            if (existingCity != null)
            {
                _context.Entry(existingCity).CurrentValues.SetValues(cityPrayerTimes);
                existingCity.DailyPrayerTimesList = cityPrayerTimes.DailyPrayerTimesList;
            }
            else
            {
                await _context.CityPrayerTimes.AddAsync(cityPrayerTimes);
            }

            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<CityPrayerTimes>> GetAllAsync()
        {
            return await _context.CityPrayerTimes.ToListAsync();
        }

        public async Task<CityPrayerTimes> GetByCityAsync(string city)
        {
            var result = await _context.CityPrayerTimes
                .Include(c => c.DailyPrayerTimesList)
                .SingleOrDefaultAsync(c => c.City.ToLower() == city.ToLower());
            return result;
        }

        public async Task TruncateTablesAsync()
        {
            try
            {
                // Disable foreign key checks
                await _context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 0;");

                // Truncate tables
                await _context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE DailyPrayerTimes;");
                await _context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE CityPrayerTimes;");

                // Re-enable foreign key checks
                await _context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 1;");

            }
            catch (Exception)
            {
                throw;
            }
        }



    }
}
