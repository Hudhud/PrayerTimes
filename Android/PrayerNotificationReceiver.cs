using Android.App;
using Android.Content;
using Application.Calendars;

namespace SalahTimes.AndroidApp;

[BroadcastReceiver(Exported = false)]
[IntentFilter(new[]
{
    Intent.ActionBootCompleted,
    Intent.ActionMyPackageReplaced,
    Intent.ActionTimeChanged,
    Intent.ActionTimezoneChanged,
    AlarmManager.ActionScheduleExactAlarmPermissionStateChanged
})]
public sealed class PrayerNotificationReceiver : BroadcastReceiver
{
    internal const string ReminderKey = "prayer-reminders";
    internal const string AdhanKey = "silent-adhan";
    private const string DueAction = "dk.salahtimes.android.PRAYER_NOTICE";
    private const string DueExtra = "due-at";
    private const string ReminderChannel = "prayer-reminders-silent";
    private const string AdhanChannel = "adhan-silent";
    private static readonly TimeSpan ReminderLead = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan AdhanGrace = TimeSpan.FromMinutes(10);

    private sealed record Notice(PrayerEvent Prayer, DateTimeOffset Due, bool Reminder);

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null || intent is null)
        {
            return;
        }

        var due = intent.Action == DueAction ? intent.GetLongExtra(DueExtra, 0) : 0;
        Schedule(context, due);
        if (OperatingSystem.IsAndroidVersionAtLeast(31) &&
            intent.Action == AlarmManager.ActionScheduleExactAlarmPermissionStateChanged &&
            Enabled(PrayerPreferences.Open(context)))
        {
            PrayerWidgetProvider.Refresh(context);
        }
    }

    internal static void Reschedule(Context context) => Schedule(context, 0);

    internal static bool Enabled(ISharedPreferences settings) =>
        settings.GetBoolean(ReminderKey, false) || settings.GetBoolean(AdhanKey, false);

    internal static bool CanPost(Context context) =>
        context.GetSystemService(Context.NotificationService) is NotificationManager manager && manager.AreNotificationsEnabled();

    internal static bool CanScheduleExactly(Context context) =>
        !OperatingSystem.IsAndroidVersionAtLeast(31) ||
        (context.GetSystemService(Context.AlarmService) is AlarmManager alarms && alarms.CanScheduleExactAlarms());

    internal static bool HandlesPrayerRefresh(Context context, ISharedPreferences settings) =>
        settings.GetBoolean(AdhanKey, false) && CanPost(context) && CanScheduleExactly(context);

    private static void Schedule(Context context, long scheduledDue)
    {
        if (context.GetSystemService(Context.AlarmService) is not AlarmManager alarms ||
            context.GetSystemService(Context.NotificationService) is not NotificationManager notifications)
        {
            return;
        }

        var settings = PrayerPreferences.Open(context);
        using var intent = new Intent(context, typeof(PrayerNotificationReceiver)).SetAction(DueAction);
        if (!Enabled(settings) || !settings.Contains(PrayerPreferences.PlaceKey) || !notifications.AreNotificationsEnabled())
        {
            using var pending = PendingIntent.GetBroadcast(context, 0, intent, PendingIntentFlags.NoCreate | PendingIntentFlags.Immutable);
            if (pending is not null)
            {
                alarms.Cancel(pending);
            }
            return;
        }

        try
        {
            CreateChannel(notifications, ReminderChannel, "30 minutter før bøn");
            CreateChannel(notifications, AdhanChannel, "Lydløs adhan");

            var place = PrayerPreferences.SelectedPlace(context, settings);
            var hanafi = settings.GetBoolean(PrayerPreferences.HanafiKey, false);
            var reminders = settings.GetBoolean(ReminderKey, false);
            var adhan = settings.GetBoolean(AdhanKey, false);
            var schedule = new LocalPrayerSchedule(place);
            var now = new DanishTime(TimeProvider.System).Now;
            var date = DateOnly.FromDateTime(now.Date);
            var pendingNotices = new List<Notice>(30);

            // Isha can start the day after its timetable date. A Fajr reminder can also be due before midnight.
            for (var offset = -1; offset <= 1; offset++)
            {
                foreach (var prayer in PrayerDay.Events(schedule.Day(date.AddDays(offset)), hanafi))
                {
                    if (prayer.StartsAt is not { } starts)
                    {
                        continue;
                    }

                    if (reminders)
                    {
                        pendingNotices.Add(new Notice(prayer, starts - ReminderLead, Reminder: true));
                    }
                    if (adhan)
                    {
                        pendingNotices.Add(new Notice(prayer, starts, Reminder: false));
                    }
                }
            }

            var ordered = pendingNotices.OrderBy(notice => notice.Due).ToArray();
            var refreshWidget = false;
            if (scheduledDue > 0)
            {
                foreach (var notice in ordered)
                {
                    var starts = notice.Prayer.StartsAt!.Value;
                    var expires = notice.Reminder ? starts : starts + AdhanGrace;
                    if (notice.Due.ToUnixTimeMilliseconds() >= scheduledDue && notice.Due <= now && now < expires)
                    {
                        if (PostNotice(context, notifications, settings, notice, place.Name, now) && !notice.Reminder)
                        {
                            refreshWidget = true;
                        }
                    }
                }
            }

            var next = ordered.FirstOrDefault(notice => notice.Due > now);
            if (next is not null)
            {
                var time = next.Due.ToUnixTimeMilliseconds();
                intent.PutExtra(DueExtra, time);
                using var pending = PendingIntent.GetBroadcast(context, 0, intent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;
                if (CanScheduleExactly(context))
                {
                    try
                    {
                        alarms.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, time, pending);
                    }
                    catch (Java.Lang.SecurityException)
                    {
                        alarms.SetAndAllowWhileIdle(AlarmType.RtcWakeup, time, pending);
                    }
                }
                else
                {
                    alarms.SetAndAllowWhileIdle(AlarmType.RtcWakeup, time, pending);
                }
            }

            if (refreshWidget)
            {
                PrayerWidgetProvider.Refresh(context);
            }
        }
        catch (Exception error) when (error is IOException or InvalidDataException or TimeZoneNotFoundException or Java.IO.IOException)
        {
            global::Android.Util.Log.Warn("SalahTimes", error.Message);
        }
    }

    private static void CreateChannel(NotificationManager manager, string id, string name)
    {
        using var channel = new NotificationChannel(id, name, NotificationImportance.Low);
        channel.SetSound(null, null);
        channel.EnableVibration(false);
        channel.EnableLights(false);
        manager.CreateNotificationChannel(channel);
    }

    private static bool PostNotice(Context context, NotificationManager manager, ISharedPreferences settings,
        Notice notice, string place, DateTimeOffset now)
    {
        var channel = notice.Reminder ? ReminderChannel : AdhanChannel;
        var sentKey = $"last-{channel}";
        var due = notice.Due.ToUnixTimeMilliseconds();
        if (settings.GetLong(sentKey, 0) >= due)
        {
            return false;
        }

        var prayer = notice.Prayer;
        var remaining = (int)Math.Ceiling((prayer.StartsAt!.Value - now).TotalMinutes);
        var title = notice.Reminder ? $"{prayer.Name} om {remaining} {(remaining == 1 ? "minut" : "minutter")}" :
            $"Adhan · {prayer.Name}";
        using var open = new Intent(context, typeof(MainActivity)).AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
        using var tap = PendingIntent.GetActivity(context, 1, open, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
        using var builder = new Notification.Builder(context, channel);
        using var notification = builder.SetSmallIcon(Resource.Drawable.ic_prayer_notification)
            .SetContentTitle(title)
            .SetContentText($"{place} · kl. {prayer.Time}")
            .SetCategory(Notification.CategoryReminder)
            .SetContentIntent(tap)
            .SetAutoCancel(true)
            .SetOnlyAlertOnce(true)
            .SetShowWhen(false)
            .Build();
        manager.Notify(channel, (int)(due / 60_000), notification);
        using var edit = settings.Edit();
        edit?.PutLong(sentKey, due)?.Apply();
        return true;
    }
}
