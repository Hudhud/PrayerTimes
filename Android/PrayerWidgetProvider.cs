using System.Globalization;
using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Graphics;
using Android.Views;
using Android.Widget;
using Application.Calendars;

namespace SalahTimes.AndroidApp;

[BroadcastReceiver(Exported = false, Label = "Salah Times 2 × 4")]
[IntentFilter(new[]
{
    AppWidgetManager.ActionAppwidgetUpdate,
    Intent.ActionTimeChanged,
    Intent.ActionTimezoneChanged,
    Intent.ActionMyPackageReplaced
})]
[MetaData("android.appwidget.provider", Resource = "@xml/prayer_widget_info")]
public sealed class PrayerWidgetProvider : AppWidgetProvider
{
    private const string RefreshAction = "dk.salahtimes.android.WIDGET_REFRESH";
    private static readonly CultureInfo Danish = CultureInfo.GetCultureInfo("da-DK");
    private static readonly Color Accent = Color.ParseColor("#F5CE78");
    private static readonly Color Muted = Color.ParseColor("#BEC5C9");

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is not null && (intent?.Action is RefreshAction or Intent.ActionTimeChanged
            or Intent.ActionTimezoneChanged or Intent.ActionMyPackageReplaced))
        {
            Refresh(context);
            return;
        }

        base.OnReceive(context, intent);
    }

    public override void OnUpdate(Context? context, AppWidgetManager? manager, int[]? appWidgetIds)
    {
        if (context is not null && manager is not null && appWidgetIds is { Length: > 0 })
        {
            RenderWidgets(context, manager, appWidgetIds);
        }
    }

    public override void OnDisabled(Context? context)
    {
        if (context?.GetSystemService(Context.AlarmService) is AlarmManager alarms && RefreshIntent(context) is { } pending)
        {
            alarms.Cancel(pending);
        }
    }

    internal static void Refresh(Context context)
    {
        var manager = AppWidgetManager.GetInstance(context);
        if (manager is null)
        {
            return;
        }

        using var provider = WidgetComponent(context);
        var ids = manager.GetAppWidgetIds(provider);
        if (ids is { Length: > 0 })
        {
            RenderWidgets(context, manager, ids);
        }
    }

    internal static bool RequestPin(Context context)
    {
        var manager = AppWidgetManager.GetInstance(context);
        if (manager?.IsRequestPinAppWidgetSupported != true)
        {
            return false;
        }

        using var provider = WidgetComponent(context);
        return manager.RequestPinAppWidget(provider, null, null);
    }

    private static ComponentName WidgetComponent(Context context) =>
        new(context, Java.Lang.Class.FromType(typeof(PrayerWidgetProvider)));

    private static void RenderWidgets(Context context, AppWidgetManager manager, int[] ids)
    {
        var views = new RemoteViews(context.PackageName, Resource.Layout.prayer_widget);
        using var open = new Intent(context, typeof(MainActivity))
            .SetAction(Intent.ActionMain).AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
        views.SetOnClickPendingIntent(Resource.Id.widget_card,
            PendingIntent.GetActivity(context, 0, open, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable));

        try
        {
            var preferences = PrayerPreferences.Open(context);
            var place = PrayerPreferences.SelectedPlace(context, preferences);
            var hanafi = preferences.GetBoolean(PrayerPreferences.HanafiKey, false);
            var now = new DanishTime(TimeProvider.System).Now;
            var date = DateOnly.FromDateTime(now.Date);
            var schedule = new LocalPrayerSchedule(place);
            var day = schedule.Day(date);
            var (next, refreshAt) = schedule.Next(now, hanafi);

            views.SetTextViewText(Resource.Id.widget_place, place.Name);
            views.SetTextViewText(Resource.Id.widget_date, $"{date.ToString("d. MMM", Danish)} · {(hanafi ? "Hanafi" : "Standard")}");
            views.SetTextViewText(Resource.Id.widget_next_label,
                next?.StartsAt?.Date > now.Date ? "Næste bøn i morgen" : "Næste bøn");
            views.SetTextViewText(Resource.Id.widget_next_prayer,
                next is null ? "Lyse nætter" : $"{next.Name} · {next.Time}");
            views.RemoveAllViews(Resource.Id.widget_times);

            foreach (var prayer in PrayerDay.Events(day, hanafi))
            {
                if (prayer.Name == "Dhuhr")
                {
                    AddRow(context, views, "Solopgang", day.SunriseTime, sunrise: true);
                }

                var overnight = prayer.StartsAt?.Date > day.Date;
                var highlight = next is not null && next.Name == prayer.Name && next.StartsAt == prayer.StartsAt;
                AddRow(context, views, prayer.Name, prayer.Time, overnight: overnight, highlight: highlight);
            }

            var notes = new List<string>(2);
            if (day.FajrTimeUnavailable || day.IshaTimeUnavailable)
            {
                notes.Add("— Lyse nætter");
            }
            if (day.IshaDayOffset == 1)
            {
                notes.Add("⁺ Næste døgn");
            }
            views.SetTextViewText(Resource.Id.widget_note, string.Join('\n', notes));
            views.SetViewVisibility(Resource.Id.widget_note, notes.Count > 0 ? ViewStates.Visible : ViewStates.Gone);

            if (context.GetSystemService(Context.AlarmService) is AlarmManager alarms && RefreshIntent(context) is { } pending)
            {
                alarms.SetAndAllowWhileIdle(AlarmType.Rtc, refreshAt.ToUnixTimeMilliseconds(), pending);
            }
        }
        catch (Exception error) when (error is IOException or InvalidDataException or TimeZoneNotFoundException or Java.IO.IOException)
        {
            global::Android.Util.Log.Warn("SalahTimes", error.Message);
        }

        manager.UpdateAppWidget(ids, views);
    }

    private static PendingIntent? RefreshIntent(Context context)
    {
        using var intent = new Intent(context, typeof(PrayerWidgetProvider)).SetAction(RefreshAction);
        return PendingIntent.GetBroadcast(context, 0, intent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
    }

    private static void AddRow(Context context, RemoteViews views, string name, string? time,
        bool sunrise = false, bool overnight = false, bool highlight = false)
    {
        var row = new RemoteViews(context.PackageName, Resource.Layout.prayer_widget_row);
        row.SetTextViewText(Resource.Id.widget_prayer_name, name);
        row.SetTextViewText(Resource.Id.widget_prayer_time, overnight ? $"{time}⁺" : time ?? "—");
        var spoken = time is null ? "Lyse nætter" : $"{time}{(overnight ? ", næste døgn" : "")}";
        row.SetContentDescription(Resource.Id.widget_prayer_row, $"{name}, {spoken}");
        if (sunrise || highlight)
        {
            var color = highlight ? Accent : Muted;
            row.SetTextColor(Resource.Id.widget_prayer_name, color);
            row.SetTextColor(Resource.Id.widget_prayer_time, color);
        }
        views.AddView(Resource.Id.widget_times, row);
    }
}
