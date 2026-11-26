using Application.Calendars;
using Foundation;
using UserNotifications;

namespace SalahTimes.iOS;

internal sealed class PrayerReminders
{
    private const string Prefix = "salah-";
    private const int MaximumPending = 60;
    private readonly PrayerAppState state;
    private readonly UNUserNotificationCenter center = UNUserNotificationCenter.Current;
    private readonly ForegroundNotifications delegateInstance = new();
    private readonly SemaphoreSlim refreshLock = new(1, 1);

    public PrayerReminders(PrayerAppState state)
    {
        this.state = state;
        center.Delegate = delegateInstance;
    }

    public async Task<bool> RequestPermissionAsync()
    {
        var settings = await center.GetNotificationSettingsAsync();
        if (settings.AuthorizationStatus == UNAuthorizationStatus.NotDetermined)
        {
            await center.RequestAuthorizationAsync(UNAuthorizationOptions.Alert | UNAuthorizationOptions.Sound);
            settings = await center.GetNotificationSettingsAsync();
        }
        return AllowsNotifications(settings.AuthorizationStatus);
    }

    public async Task RefreshAsync()
    {
        await refreshLock.WaitAsync();
        try
        {
            var enabled = state.EarlyReminder || state.PrayerReminder;
            var existing = await center.GetPendingNotificationRequestsAsync();
            var oldIds = existing.Select(request => request.Identifier)
                .Where(id => id.StartsWith(Prefix, StringComparison.Ordinal)).ToArray();
            if (!enabled || !AllowsNotifications((await center.GetNotificationSettingsAsync()).AuthorizationStatus))
            {
                center.RemovePendingNotificationRequests(oldIds);
                return;
            }

            var current = state.Now;
            var start = DateOnly.FromDateTime(current.Date);
            var schedule = state.Schedule;
            var hanafi = state.Hanafi;
            var early = state.EarlyReminder;
            var atPrayer = state.PrayerReminder;
            var place = state.Place.Name;
            var notices = new List<Notice>();
            for (var offset = -1; offset < 14; offset++)
            {
                foreach (var prayer in PrayerDay.Events(schedule.Day(start.AddDays(offset)), hanafi))
                {
                    if (prayer.StartsAt is not { } time)
                    {
                        continue;
                    }

                    if (early)
                    {
                        notices.Add(new Notice(prayer, time.AddMinutes(-30), Early: true));
                    }

                    if (atPrayer)
                    {
                        notices.Add(new Notice(prayer, time, Early: false));
                    }
                }
            }

            var planned = notices.Where(notice => notice.Due > current).OrderBy(notice => notice.Due)
                .Take(MaximumPending).ToArray();
            center.RemovePendingNotificationRequests(oldIds.Except(planned.Select(notice => notice.Id)).ToArray());

            foreach (var notice in planned)
            {
                var delay = (notice.Due - TimeProvider.System.GetUtcNow()).TotalSeconds;
                if (delay < 1)
                {
                    continue;
                }
                var name = notice.Prayer.Name;
                using var content = new UNMutableNotificationContent
                {
                    Title = notice.Early ? state.Text($"{name} om 30 minutter", $"{name} in 30 minutes") : name,
                    Body = $"{place} · {notice.Prayer.Time}",
                    Sound = UNNotificationSound.Default
                };
                using var trigger = UNTimeIntervalNotificationTrigger.CreateTrigger(delay, repeats: false);
                using var request = UNNotificationRequest.FromIdentifier(notice.Id, content, trigger);
                await center.AddNotificationRequestAsync(request);
            }
        }
        catch (NSErrorException error)
        {
            Console.Error.WriteLine(error.Message);
        }
        finally
        {
            refreshLock.Release();
        }
    }

    private static bool AllowsNotifications(UNAuthorizationStatus status) =>
        status is UNAuthorizationStatus.Authorized or UNAuthorizationStatus.Provisional;

    private sealed record Notice(PrayerEvent Prayer, DateTimeOffset Due, bool Early)
    {
        public string Id => $"{Prefix}{(Early ? "early" : "start")}-{Prayer.Name}-{Due.ToUnixTimeSeconds()}";
    }

    private sealed class ForegroundNotifications : UNUserNotificationCenterDelegate
    {
        public override void WillPresentNotification(UNUserNotificationCenter center, UNNotification notification,
            Action<UNNotificationPresentationOptions> completionHandler) =>
            completionHandler(UNNotificationPresentationOptions.Banner | UNNotificationPresentationOptions.Sound);
    }
}
