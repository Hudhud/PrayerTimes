using Domain.Models;
using Domain.Repositories;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Repositories
{
    public class CityPrayerTimesRepository : ICityPrayerTimesRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CityPrayerTimesRepository> _logger;

        public CityPrayerTimesRepository(ApplicationDbContext context, ILogger<CityPrayerTimesRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task AddOrUpdateAsync(CityPrayerTimes cityPrayerTimes)
        {
            try
            {
                var existingCity = await _context.CityPrayerTimes
                    .Include(c => c.PrayerTimes)
                    .FirstOrDefaultAsync(c => c.City.ToLower() == cityPrayerTimes.City.ToLower());

                if (existingCity != null)
                {
                    _context.Entry(existingCity).CurrentValues.SetValues(cityPrayerTimes);
                    existingCity.PrayerTimes = cityPrayerTimes.PrayerTimes;
                }
                else
                {
                    await _context.CityPrayerTimes.AddAsync(cityPrayerTimes);
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while adding or updating city prayer times.");
                throw;
            }
        }

        public async Task<IEnumerable<CityPrayerTimes>> GetAllAsync()
        {
            try
            {
                return await _context.CityPrayerTimes
                    .Include(c => c.PrayerTimes)
                    .AsNoTracking()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving all city prayer times.");
                throw;
            }
        }

        public async Task<CityPrayerTimes?> GetByCityAsync(string city)
        {
            try
            {
                return await _context.CityPrayerTimes
                    .Include(c => c.PrayerTimes)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.City.ToLower() == city.ToLower());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving prayer times for city: {City}", city);
                throw;
            }
        }

        public async Task TruncateTablesAsync()
        {
            try
            {
                await _context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 0;");

                await _context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE DailyPrayerTimes;");
                await _context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE CityPrayerTimes;");

                await _context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 1;");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while truncating the CityPrayerTimes and DailyPrayerTimes tables.");
                throw;
            }
        }
    }
}
