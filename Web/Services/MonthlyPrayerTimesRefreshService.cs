using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Calendars;
using Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Web.Services;

public sealed class MonthlyPrayerTimesRefreshService(
    IServiceScopeFactory scopeFactory,
    DanishTime clock,
    ILogger<MonthlyPrayerTimesRefreshService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = clock.Now;
            var currentMonth = new DateOnly(now.Year, now.Month, 1);
            await RefreshAsync(currentMonth, stoppingToken);
            await RefreshAsync(currentMonth.AddMonths(1), stoppingToken);
            await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
        }
    }

    private async Task RefreshAsync(DateOnly month, CancellationToken stoppingToken)
    {
        foreach (var city in PrayerCities.All)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<PrayerTimeService>();
            try
            {
                await service.RefreshMonthIfNeededAsync(city, month, stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(exception, "Could not refresh prayer times for {City}, {Month}", city.Slug, month);
            }
        }
    }
}
