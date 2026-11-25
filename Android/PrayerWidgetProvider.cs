using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;
using Application.Calendars;

namespace SalahTimes.AndroidApp;

[BroadcastReceiver(Exported = false, Label = "Salah Times 4 × 2")]
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
    private const int GridMinimumWidth = 215;
    private const int GridMinimumHeight = 148;
    private static readonly Color Sunrise = Color.ParseColor("#B6D5CE");

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

    public override void OnAppWidgetOptionsChanged(Context? context, AppWidgetManager? appWidgetManager,
        int appWidgetId, Bundle? newOptions)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(31) && context is not null && appWidgetManager is not null)
        {
            RenderWidgets(context, appWidgetManager, [appWidgetId]);
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
        using var open = new Intent(context, typeof(MainActivity))
            .SetAction(Intent.ActionMain).AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
        using var openApp = PendingIntent.GetActivity(context, 0, open,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        RemoteViews CreateLayout()
        {
            var views = new RemoteViews(context.PackageName, Resource.Layout.prayer_widget);
            views.SetOnClickPendingIntent(Resource.Id.widget_card, openApp);
            return views;
        }

        try
        {
            var preferences = PrayerPreferences.Open(context);
            var language = new AppLanguage(preferences);
            if (!preferences.Contains(PrayerPreferences.PlaceKey))
            {
                using var views = CreateLayout();
                views.SetTextViewText(Resource.Id.widget_place, "Salah Times");
                views.SetTextViewText(Resource.Id.widget_date, language.Text("Vælg dit sted i appen", "Choose your location in the app"));
                views.SetViewVisibility(Resource.Id.widget_current, ViewStates.Gone);
                views.SetViewVisibility(Resource.Id.widget_next, ViewStates.Gone);
                views.RemoveAllViews(Resource.Id.widget_times);
                views.SetViewVisibility(Resource.Id.widget_note, ViewStates.Gone);
                manager.UpdateAppWidget(ids, views);
                return;
            }

            var place = PrayerPreferences.SelectedPlace(context, preferences);
            var hanafi = preferences.GetBoolean(PrayerPreferences.HanafiKey, false);
            var now = LocalTime.FromUtc(TimeProvider.System.GetUtcNow(), place.TimeZoneId);
            var elapsed = SystemClock.ElapsedRealtime();
            var date = DateOnly.FromDateTime(now.Date);
            var schedule = new LocalPrayerSchedule(place, PrayerPreferences.Angles(preferences));
            var day = schedule.Day(date);
            var (current, next, refreshAt) = schedule.Status(now, hanafi);
            var times = PrayerDay.Events(day, hanafi).ToList();
            var sunrise = PrayerDay.AvailableSunrise(day);
            times.Insert(1, new PrayerEvent("Solopgang", sunrise));
            var remaining = next?.StartsAt - now;
            var currentLabel = current is not null ? language.Text($"Nu · {current.Name}", $"Now · {current.Name}") :
                next?.Name == "Dhuhr" && now >= sunrise ? language.Text("Mellem bønner", "Between prayers") : null;
            var countdownLabel = next is null ? language.Text("Ikke tilgængelig", "Time unavailable") : next.StartsAt?.Date > day.Date
                ? language.Text($"Næste · {next.Name} i morgen", $"Next · {next.Name} tomorrow")
                : language.Text($"Næste · {next.Name}", $"Next · {next.Name}");

            var notes = new List<string>(2);
            if (day.HasUnavailableTimes)
            {
                notes.Add(language.Text("— Ikke tilgængelig", "— Time unavailable"));
            }
            if (day.IshaDayOffset == 1)
            {
                notes.Add(language.Text("⁺ Næste døgn", "⁺ Next day"));
            }

            RemoteViews Layout(bool compact)
            {
                var views = CreateLayout();
                var density = context.Resources!.DisplayMetrics!.Density;
                var horizontal = (int)Math.Round((compact ? 9 : 12) * density);
                var vertical = (int)Math.Round((compact ? 5 : 12) * density);
                views.SetViewPadding(Resource.Id.widget_card, horizontal, vertical, horizontal, vertical);
                views.SetInt(Resource.Id.widget_place, "setMaxLines", compact ? 1 : 2);
                views.SetTextViewTextSize(Resource.Id.widget_place, (int)ComplexUnitType.Sp, compact ? 15 : 18);
                views.SetTextViewTextSize(Resource.Id.widget_date, (int)ComplexUnitType.Sp, compact ? 10 : 11);
                views.SetTextViewTextSize(Resource.Id.widget_current, (int)ComplexUnitType.Sp, compact ? 10 : 12);
                views.SetTextViewTextSize(Resource.Id.widget_countdown, (int)ComplexUnitType.Sp, compact ? 16 : 19);

                views.SetTextViewText(Resource.Id.widget_place, place.Name);
                views.SetTextViewText(Resource.Id.widget_date, $"{date.ToString("d. MMM", language.Culture)} · {(hanafi ? "Hanafi" : "Standard")}");
                views.SetTextViewText(Resource.Id.widget_current, currentLabel);
                views.SetTextColor(Resource.Id.widget_current, new Color(context.GetColor(
                    current is null ? Resource.Color.widget_secondary : Resource.Color.widget_current)));
                views.SetViewVisibility(Resource.Id.widget_current, currentLabel is null ? ViewStates.Gone : ViewStates.Visible);
                views.SetViewVisibility(Resource.Id.widget_next, ViewStates.Visible);
                views.SetTextViewText(Resource.Id.widget_next_label, countdownLabel);
                var deadline = elapsed + (long)(remaining?.TotalMilliseconds ?? 0);
                views.SetChronometer(Resource.Id.widget_countdown, deadline, null, remaining.HasValue);
                views.SetViewVisibility(Resource.Id.widget_countdown, remaining.HasValue ? ViewStates.Visible : ViewStates.Gone);
                views.SetTextViewText(Resource.Id.widget_note, string.Join(" · ", notes));
                views.SetViewVisibility(Resource.Id.widget_note, notes.Count > 0 ? ViewStates.Visible : ViewStates.Gone);
                views.RemoveAllViews(Resource.Id.widget_times);

                foreach (var prayers in times.Chunk(compact ? 6 : 3))
                {
                    using var row = new RemoteViews(context.PackageName, Resource.Layout.prayer_widget_row);
                    foreach (var prayer in prayers)
                    {
                        using var cell = TimeCell(context, language, prayer, day.Date, prayer == current, prayer == next, compact);
                        row.AddView(Resource.Id.widget_time_row, cell);
                    }
                    views.AddView(Resource.Id.widget_times, row);
                }
                return views;
            }

            using var gridView = Layout(compact: false);
            using var compactView = Layout(compact: true);
            UpdateWidgetSizes(manager, ids, gridView, compactView);

            if (context.GetSystemService(Context.AlarmService) is AlarmManager alarms && RefreshIntent(context) is { } pending)
            {
                var trigger = refreshAt.ToUnixTimeMilliseconds();
                if (next?.StartsAt == refreshAt && PrayerNotificationReceiver.HandlesPrayerRefresh(context, preferences))
                {
                    // The adhan alarm refreshes the widget while idle. Keep a non-wakeup fallback.
                    alarms.Set(AlarmType.Rtc, trigger, pending);
                }
                else if (OperatingSystem.IsAndroidVersionAtLeast(31))
                {
                    alarms.SetAndAllowWhileIdle(AlarmType.Rtc, trigger, pending);
                }
                else
                {
                    alarms.SetExactAndAllowWhileIdle(AlarmType.Rtc, trigger, pending);
                }
            }
        }
        catch (Exception error) when (error is IOException or InvalidDataException or TimeZoneNotFoundException or Java.IO.IOException)
        {
            global::Android.Util.Log.Warn("SalahTimes", error.Message);
        }
    }

    private static void UpdateWidgetSizes(AppWidgetManager manager, int[] ids, RemoteViews grid, RemoteViews compact)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(31))
        {
            using var small = new SizeF(1, 1);
            using var large = new SizeF(GridMinimumWidth, GridMinimumHeight);
            using var responsive = new RemoteViews(new Dictionary<SizeF, RemoteViews> { [small] = compact, [large] = grid });
            manager.UpdateAppWidget(ids, responsive);
            return;
        }

        foreach (var id in ids)
        {
            using var size = manager.GetAppWidgetOptions(id);
            var portrait = FitsGrid(size?.GetInt(AppWidgetManager.OptionAppwidgetMinWidth) ?? 0,
                size?.GetInt(AppWidgetManager.OptionAppwidgetMaxHeight) ?? 0) ? grid : compact;
            var landscape = FitsGrid(size?.GetInt(AppWidgetManager.OptionAppwidgetMaxWidth) ?? 0,
                size?.GetInt(AppWidgetManager.OptionAppwidgetMinHeight) ?? 0) ? grid : compact;
            using var orientations = new RemoteViews(landscape, portrait);
            manager.UpdateAppWidget(id, orientations);
        }
    }

    private static bool FitsGrid(int width, int height) => width >= GridMinimumWidth && height >= GridMinimumHeight;

    private static PendingIntent? RefreshIntent(Context context)
    {
        using var intent = new Intent(context, typeof(PrayerWidgetProvider)).SetAction(RefreshAction);
        return PendingIntent.GetBroadcast(context, 0, intent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
    }

    private static RemoteViews TimeCell(Context context, AppLanguage language, PrayerEvent prayer, DateTime date, bool current, bool next, bool compact)
    {
        var cell = new RemoteViews(context.PackageName, compact ? Resource.Layout.prayer_widget_compact_cell : Resource.Layout.prayer_widget_cell);
        var time = prayer.Time;
        var overnight = prayer.StartsAt?.Date > date;
        var name = prayer.Name == "Solopgang" ? language.Text("Solopgang", "Sunrise") : prayer.Name;
        cell.SetTextViewText(Resource.Id.widget_prayer_name, name);
        cell.SetTextViewText(Resource.Id.widget_prayer_time, overnight ? $"{time}⁺" : time ?? "—");
        var spoken = time is null ? language.Text("Ikke tilgængelig", "Time unavailable") : $"{time}{(overnight ? language.Text(", næste døgn", ", next day") : "")}";
        var status = current ? language.Text("Aktuel bøn, ", "Current prayer, ") : next ? language.Text("Næste bøn, ", "Next prayer, ") : "";
        cell.SetContentDescription(Resource.Id.widget_prayer_cell, $"{status}{name}, {spoken}");
        if (current || next)
        {
            var color = new Color(context.GetColor(current ? Resource.Color.widget_current : Resource.Color.widget_accent));
            cell.SetTextColor(Resource.Id.widget_prayer_name, color);
            cell.SetTextColor(Resource.Id.widget_prayer_time, color);
            if (!compact)
            {
                cell.SetInt(Resource.Id.widget_prayer_cell, "setBackgroundResource", current
                    ? Resource.Drawable.widget_cell_current_background : Resource.Drawable.widget_cell_accent_background);
            }
        }
        else if (prayer.Name == "Solopgang")
        {
            cell.SetTextColor(Resource.Id.widget_prayer_name, Sunrise);
            cell.SetTextColor(Resource.Id.widget_prayer_time, Sunrise);
        }
        return cell;
    }
}
