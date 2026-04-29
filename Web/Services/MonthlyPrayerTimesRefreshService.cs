using Application.Interfaces;
using Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace Web.Services
{
    public class MonthlyPrayerTimesRefreshService : BackgroundService
    {

        private static readonly string[] SupportedCities = ["cph", "odense", "aarhus", "aalborg"];

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<MonthlyPrayerTimesRefreshService> _logger;

        public MonthlyPrayerTimesRefreshService(
            IServiceScopeFactory scopeFactory,
            ILogger<MonthlyPrayerTimesRefreshService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var denmarkTimeZone = GetDenmarkTimeZone();

            await RefreshIfNeededAsync(denmarkTimeZone, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                var delay = GetDelayUntilNextMidnight(denmarkTimeZone);
                _logger.LogInformation("Monthly refresh worker sleeping for {Delay} until next Denmark midnight.", delay);
                await Task.Delay(delay, stoppingToken);
                await RefreshIfNeededAsync(denmarkTimeZone, stoppingToken);
            }
        }

        private async Task RefreshIfNeededAsync(TimeZoneInfo denmarkTimeZone, CancellationToken cancellationToken)
        {
            var nowInDenmark = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, denmarkTimeZone);
            var firstOfMonth = new DateTime(nowInDenmark.Year, nowInDenmark.Month, 1);

            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<ICityPrayerTimesRepository>();
            var prayerTimeService = scope.ServiceProvider.GetRequiredService<IPrayerTimeService>();

            var allRows = await repository.GetAllAsync();
            var hasCurrentMonthData = allRows
                .SelectMany(c => c.PrayerTimes)
                .Any(p => p.Date.Year == firstOfMonth.Year && p.Date.Month == firstOfMonth.Month);

            if (hasCurrentMonthData)
            {
                _logger.LogInformation("Monthly refresh skipped. Data for {Month}/{Year} already exists.", firstOfMonth.Month, firstOfMonth.Year);
                return;
            }

            _logger.LogInformation("Refreshing prayer calendar data for {Month}/{Year}.", firstOfMonth.Month, firstOfMonth.Year);
            await repository.TruncateTablesAsync();

            foreach (var city in SupportedCities)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await prayerTimeService.FetchAndCachePrayerTimesAsync(city);
            }

            _logger.LogInformation("Monthly prayer calendar refresh complete for {Month}/{Year}.", firstOfMonth.Month, firstOfMonth.Year);
        }

        private static TimeSpan GetDelayUntilNextMidnight(TimeZoneInfo denmarkTimeZone)
        {
            var nowInDenmark = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, denmarkTimeZone);
            var nextMidnightInDenmark = nowInDenmark.Date.AddDays(1);
            return nextMidnightInDenmark - nowInDenmark;
        }

        private static TimeZoneInfo GetDenmarkTimeZone()
        {
            var timeZoneId = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "Romance Standard Time"
                : "Europe/Copenhagen";

            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
    }
}
