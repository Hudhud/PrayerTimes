using Application.Interfaces;
using Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace Web.Services
{
    public class MonthlyPrayerTimesRefreshService : BackgroundService
    {
        private static readonly string[] SupportedCities = new[] { "cph", "odense", "aarhus", "aalborg" };

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

            await TryRunRefreshCycleAsync(denmarkTimeZone, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                var delay = GetDelayUntilNextMidnight(denmarkTimeZone);
                _logger.LogInformation("Monthly refresh worker sleeping for {Delay} until next Denmark midnight.", delay);
                await Task.Delay(delay, stoppingToken);
                await TryRunRefreshCycleAsync(denmarkTimeZone, stoppingToken);
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
            var coveredCities = allRows
                .Where(c => SupportedCities.Contains(c.City, StringComparer.OrdinalIgnoreCase))
                .Where(c => c.PrayerTimes.Any(p => p.Date.Year == firstOfMonth.Year && p.Date.Month == firstOfMonth.Month))
                .Select(c => c.City.ToLowerInvariant())
                .Distinct()
                .ToHashSet();

            var missingCities = SupportedCities
                .Where(city => !coveredCities.Contains(city))
                .ToList();

            if (!missingCities.Any())
            {
                _logger.LogInformation("Monthly refresh skipped. Data for all supported cities already exists for {Month}/{Year}.", firstOfMonth.Month, firstOfMonth.Year);
                return;
            }

            _logger.LogInformation("Refreshing prayer calendar data for {Month}/{Year}. Missing cities: {MissingCities}", firstOfMonth.Month, firstOfMonth.Year, string.Join(",", missingCities));

            if (missingCities.Count == SupportedCities.Length)
            {
                await repository.TruncateTablesAsync();
            }

            foreach (var city in missingCities)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await prayerTimeService.FetchAndCachePrayerTimesAsync(city);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Monthly refresh failed for city {City} in {Month}/{Year}.", city, firstOfMonth.Month, firstOfMonth.Year);
                }
            }

            _logger.LogInformation("Monthly prayer calendar refresh cycle completed for {Month}/{Year}.", firstOfMonth.Month, firstOfMonth.Year);
        }

        private async Task TryRunRefreshCycleAsync(TimeZoneInfo denmarkTimeZone, CancellationToken stoppingToken)
        {
            try
            {
                await RefreshIfNeededAsync(denmarkTimeZone, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Monthly refresh cycle failed unexpectedly.");
            }
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
