using Android.Content;
using Android.Content.Res;
using Android.Content.PM;
using Android.Views;
using Android.Widget;
using PermissionNames = Android.Manifest.Permission;
using DeviceSettings = Android.Provider.Settings;

namespace SalahTimes.AndroidApp;

public sealed partial class MainActivity
{
    private const int PrayerNotificationPermissionRequest = 22;
    private const string NotificationPermissionAsked = "notification-permission-asked";
    private bool returningFromPrayerNotificationSettings;

    private void AddPrayerNotificationSettings()
    {
        AddSectionTitle(T("Notifikationer", "Notifications"));
        var options = Panel(Surface, Outline);
        var canPost = PrayerNotificationReceiver.CanPost(this);
        AddNotificationSwitch(options, T("Påmindelse 30 min. før bøn", "Reminder 30 minutes before prayer"), PrayerNotificationReceiver.ReminderKey, canPost);
        options.AddView(Divider());
        AddNotificationSwitch(options, T("Vibration ved bønnetid", "Vibration at prayer time"), PrayerNotificationReceiver.AdhanKey, canPost);
        content.AddView(options, MatchWidth());
        content.AddView(Label(T("Begge beskeder vibrerer uden lyd.", "Both notifications vibrate silently."), 14, Muted), MatchWidth(top: 8));

        if (!PrayerNotificationReceiver.Enabled(preferences))
        {
            return;
        }

        if (!canPost)
        {
            content.AddView(ActionButton(T("Tillad notifikationer", "Allow notifications"), AllowPrayerNotifications), MatchWidth(top: 8));
        }
        else if (!HasSelectedPlace)
        {
            content.AddView(Label(T("Vælg et sted for at få bønnebeskeder.", "Choose a location to receive prayer notifications."), 14, Muted), MatchWidth(top: 8));
        }
        else if (OperatingSystem.IsAndroidVersionAtLeast(31) && !PrayerNotificationReceiver.CanScheduleExactly(this))
        {
            content.AddView(Label(T("Android kan forsinke beskeder, hvis præcise alarmer er slået fra.", "Android can delay notifications when exact alarms are disabled."), 14, Muted), MatchWidth(top: 8));
            content.AddView(ActionButton(T("Tillad præcise alarmer", "Allow exact alarms"), AllowPrecisePrayerAlarms), MatchWidth(top: 7));
        }
    }

    private void AddNotificationSwitch(LinearLayout options, string title, string key, bool canPost)
    {
        var toggle = new Switch(this)
        {
            Text = title,
            TextSize = 14,
            Checked = preferences.GetBoolean(key, false),
            Gravity = GravityFlags.CenterVertical
        };
        toggle.SetMinHeight(Dp(52));
        toggle.SetPadding(Dp(13), Dp(5), Dp(13), Dp(5));
        toggle.SetTextColor(Text);
        var blocked = toggle.Checked && !canPost;
        var blockedLabel = T("Blokeret af Android", "Blocked by Android");
        if (blocked)
        {
            toggle.ThumbTintList = ColorStateList.ValueOf(Muted);
            toggle.TrackTintList = ColorStateList.ValueOf(Outline);
            if (OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                toggle.StateDescription = blockedLabel;
            }
        }
        toggle.CheckedChange += (_, args) =>
        {
            using var edit = preferences.Edit();
            edit?.PutBoolean(key, args.IsChecked)?.Apply();
            RefreshPrayerNotificationSettings();
            PrayerWidgetProvider.Refresh(this);
            if (args.IsChecked)
            {
                RequestPrayerNotificationPermission();
            }
        };
        options.AddView(toggle, MatchWidth());
        if (blocked)
        {
            var note = Label(blockedLabel, 13, Accent, bold: true);
            note.SetPadding(Dp(13), 0, Dp(13), Dp(8));
            options.AddView(note, MatchWidth());
        }
    }

    private bool RequestPrayerNotificationPermission()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(33) ||
            CheckSelfPermission(PermissionNames.PostNotifications) == Permission.Granted ||
            (preferences.GetBoolean(NotificationPermissionAsked, false) &&
             !ShouldShowRequestPermissionRationale(PermissionNames.PostNotifications)))
        {
            return false;
        }

        using var edit = preferences.Edit();
        edit?.PutBoolean(NotificationPermissionAsked, true)?.Apply();
        RequestPermissions([PermissionNames.PostNotifications], PrayerNotificationPermissionRequest);
        return true;
    }

    private void AllowPrayerNotifications()
    {
        if (!RequestPrayerNotificationPermission())
        {
            OpenPrayerNotificationSettings(new Intent(DeviceSettings.ActionAppNotificationSettings)
                .PutExtra(DeviceSettings.ExtraAppPackage, PackageName));
        }
    }

    private void AllowPrecisePrayerAlarms()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(31))
        {
            OpenPrayerNotificationSettings(new Intent(DeviceSettings.ActionRequestScheduleExactAlarm,
                global::Android.Net.Uri.FromParts("package", PackageName, null)));
        }
    }

    private void OpenPrayerNotificationSettings(Intent intent)
    {
        using (intent)
        {
            try
            {
                StartActivity(intent);
                returningFromPrayerNotificationSettings = true;
            }
            catch (ActivityNotFoundException)
            {
                Toast.MakeText(this, T("Åbn Salah Times i Androids appindstillinger.", "Open Salah Times in Android app settings."), ToastLength.Long)?.Show();
            }
        }
    }

    private void RefreshPrayerNotificationSettings()
    {
        PrayerNotificationReceiver.Reschedule(this);
        if (returningFromPrayerNotificationSettings)
        {
            returningFromPrayerNotificationSettings = false;
            PrayerWidgetProvider.Refresh(this);
        }

        if (screen == Screen.Settings)
        {
            var position = scroll.ScrollY;
            Show(Screen.Settings);
            var settingsScroll = scroll;
            settingsScroll.Post(() => settingsScroll.ScrollTo(0, position));
        }
    }
}
