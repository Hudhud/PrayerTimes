using Android.Content;
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
        AddSectionTitle("Notifikationer");
        var options = Panel(Surface, Outline);
        AddNotificationSwitch(options, "Påmindelse 30 min. før bøn", PrayerNotificationReceiver.ReminderKey);
        options.AddView(Divider());
        AddNotificationSwitch(options, "Adhan ved bønnetid", PrayerNotificationReceiver.AdhanKey);
        content.AddView(options, MatchWidth());
        content.AddView(Label("Begge beskeder vibrerer uden lyd.", 14, Muted), MatchWidth(top: 8));

        if (!PrayerNotificationReceiver.Enabled(preferences))
        {
            return;
        }

        if (!PrayerNotificationReceiver.CanPost(this))
        {
            content.AddView(ActionButton("Tillad notifikationer", AllowPrayerNotifications), MatchWidth(top: 8));
        }
        else if (!HasSelectedPlace)
        {
            content.AddView(Label("Vælg et sted for at få bønnebeskeder.", 14, Muted), MatchWidth(top: 8));
        }
        else if (OperatingSystem.IsAndroidVersionAtLeast(31) && !PrayerNotificationReceiver.CanScheduleExactly(this))
        {
            content.AddView(Label("Android kan forsinke beskeder, hvis præcise alarmer er slået fra.", 14, Muted), MatchWidth(top: 8));
            content.AddView(ActionButton("Tillad præcise alarmer", AllowPrecisePrayerAlarms), MatchWidth(top: 7));
        }
    }

    private void AddNotificationSwitch(LinearLayout options, string title, string key)
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
                Toast.MakeText(this, "Åbn Salah Times i Androids appindstillinger.", ToastLength.Long)?.Show();
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
