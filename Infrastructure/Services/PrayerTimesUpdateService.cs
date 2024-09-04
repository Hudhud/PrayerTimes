using Application.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services
{
    public class PrayerTimesUpdateService : BackgroundService
    {
        private readonly IPrayerTimeService _prayerTimeService;
        private readonly ILogger<PrayerTimesUpdateService> _logger;

        public PrayerTimesUpdateService(
            IPrayerTimeService prayerTimeService,
            ILogger<PrayerTimesUpdateService> logger)
        {
            _prayerTimeService = prayerTimeService;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (DateTime.Today.Day == 1)
                    {

                        string[] cities = { "cph", "odense", "aarhus", "aalborg" };

                        foreach (var city in cities)
                        {
                            await _prayerTimeService.GetPrayerTimesAsync(city);
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
}
