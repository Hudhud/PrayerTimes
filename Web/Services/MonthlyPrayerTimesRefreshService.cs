using Application.Interfaces;
using Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace Web.Services
{
    public class MonthlyPrayerTimesRefreshService : BackgroundService
    {
        private static readonly string[] SupportedCities = new[] { "cph", "odense", "aarhus", "aalborg" };
        private static readonly TimeSpan CityFetchDelay = TimeSpan.FromSeconds(15);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<MonthlyPrayerTimesRefreshService> _logger;
        private readonly IDateTimeProvider _dateTimeProvider;

        public MonthlyPrayerTimesRefreshService(
            IServiceScopeFactory scopeFactory,
            ILogger<MonthlyPrayerTimesRefreshService> logger,
            IDateTimeProvider dateTimeProvider)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _dateTimeProvider = dateTimeProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var denmarkTimeZone = GetDenmarkTimeZone();

            while (!stoppingToken.IsCancellationRequested)
            {
                var nowInDenmark = TimeZoneInfo.ConvertTimeFromUtc(_dateTimeProvider.UtcNow, denmarkTimeZone);
                var nextRunAt = await GetNextScheduledCheckAsync(nowInDenmark, denmarkTimeZone, stoppingToken);
                var delay = nextRunAt - nowInDenmark;

                _logger.LogInformation("Monthly refresh worker sleeping for {Delay} until next scheduled check at {NextRunAt} Denmark time.", delay, nextRunAt);
                await Task.Delay(delay, stoppingToken);
                await TryRunRefreshCycleAsync(denmarkTimeZone, stoppingToken);
            }
        }

        private async Task RefreshIfNeededAsync(TimeZoneInfo denmarkTimeZone, CancellationToken cancellationToken)
        {
            var nowInDenmark = TimeZoneInfo.ConvertTimeFromUtc(_dateTimeProvider.UtcNow, denmarkTimeZone);
            if (nowInDenmark.Day != 1)
            {
                _logger.LogDebug("Monthly refresh skipped because today is not the first day of the month ({Day}).", nowInDenmark.Day);
                return;
            }

            var firstOfMonth = new DateTime(nowInDenmark.Year, nowInDenmark.Month, 1);
            var previousMonthLastIsha = await GetPreviousMonthLastIshaThresholdAsync(denmarkTimeZone, cancellationToken);

            if (previousMonthLastIsha.HasValue && nowInDenmark < previousMonthLastIsha.Value)
            {
                _logger.LogInformation("Monthly refresh deferred until previous month isha passes at {Threshold} Denmark time.", previousMonthLastIsha.Value);
                return;
            }

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

            for (var i = 0; i < missingCities.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var city = missingCities[i];

                try
                {
                    await prayerTimeService.FetchAndCachePrayerTimesAsync(city);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Monthly refresh failed for city {City} in {Month}/{Year}.", city, firstOfMonth.Month, firstOfMonth.Year);
                }

                if (i < missingCities.Count - 1)
                {
                    await Task.Delay(CityFetchDelay, cancellationToken);
                }
            }

            _logger.LogInformation("Monthly prayer calendar refresh cycle completed for {Month}/{Year}.", firstOfMonth.Month, firstOfMonth.Year);
        }

        private async Task<DateTime?> GetPreviousMonthLastIshaThresholdAsync(TimeZoneInfo denmarkTimeZone, CancellationToken cancellationToken)
        {
            var nowInDenmark = TimeZoneInfo.ConvertTimeFromUtc(_dateTimeProvider.UtcNow, denmarkTimeZone);
            var previousMonth = nowInDenmark.AddMonths(-1);
            var lastDayOfPreviousMonth = new DateTime(previousMonth.Year, previousMonth.Month, DateTime.DaysInMonth(previousMonth.Year, previousMonth.Month));

            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<ICityPrayerTimesRepository>();

            var allRows = await repository.GetAllAsync();
            var lastIshaTimes = new List<DateTime>();

            foreach (var cityName in SupportedCities)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var cityData = allRows
                    .FirstOrDefault(c => string.Equals(c.City, cityName, StringComparison.OrdinalIgnoreCase));

                if (cityData?.PrayerTimes == null)
                {
                    continue;
                }

                var dayData = cityData.PrayerTimes.FirstOrDefault(p => p.Date.Date == lastDayOfPreviousMonth);
                if (dayData == null)
                {
                    continue;
                }

                if (TimeSpan.TryParse(dayData.IshaTime, out var ishaTime))
                {
                    lastIshaTimes.Add(lastDayOfPreviousMonth.Add(ishaTime));
                }
            }

            if (!lastIshaTimes.Any())
            {
                return null;
            }

            return lastIshaTimes.Max();
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

        private async Task<DateTime> GetNextScheduledCheckAsync(DateTime nowInDenmark, TimeZoneInfo denmarkTimeZone, CancellationToken cancellationToken)
        {
            if (nowInDenmark.Day != 1)
            {
                return nowInDenmark.Date.AddDays(1);
            }

            var threshold = await GetPreviousMonthLastIshaThresholdAsync(denmarkTimeZone, cancellationToken);
            return threshold.HasValue && threshold.Value > nowInDenmark
                ? threshold.Value
                : nowInDenmark;
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
