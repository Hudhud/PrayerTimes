using Application.Interfaces;
using Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class PrayerTimeUpdateService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PrayerTimeUpdateService> _logger;
    private int _lastRunMonth = -1;

    public PrayerTimeUpdateService(IServiceProvider serviceProvider, ILogger<PrayerTimeUpdateService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (DateTime.Today.Day == 1 && _lastRunMonth != DateTime.Today.Month)
                {
                    _lastRunMonth = DateTime.Today.Month;

                    using var scope = _serviceProvider.CreateScope();
                    var cityPrayerTimesRepo = scope.ServiceProvider.GetRequiredService<ICityPrayerTimesRepository>();

                    await cityPrayerTimesRepo.TruncateTablesAsync();

                    var prayerTimeService = scope.ServiceProvider.GetRequiredService<IPrayerTimeService>();
                    string[] cities = { "cph", "odense", "aarhus", "aalborg" };

                    foreach (var city in cities)
                    {
                        await prayerTimeService.FetchAndCachePrayerTimesAsync(city);
                        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while updating prayer times.");
            }

            await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
        }
    }
}
