using Android.Content;
using Android.Views;
using Android.Widget;
using Application.Calendars;
using DeviceSettings = Android.Provider.Settings;

namespace SalahTimes.AndroidApp;

public sealed partial class MainActivity
{
    private string? loadedPlaceId;

    private void AddBackgroundLocationSetting()
    {
        if (!PrayerLocationReceiver.UsesAutomaticLocation(preferences))
        {
            return;
        }

        var enabled = preferences.GetBoolean(PrayerLocationReceiver.BackgroundLocationKey, false);
        var toggle = new Switch(this)
        {
            Text = "Opdatér sted i baggrunden",
            TextSize = 14,
            Checked = enabled,
            Gravity = GravityFlags.CenterVertical,
            Background = Rounded(Surface, 13, Outline)
        };
        toggle.SetMinHeight(Dp(48));
        toggle.SetPadding(Dp(13), Dp(5), Dp(13), Dp(5));
        toggle.SetTextColor(Text);
        toggle.CheckedChange += (_, args) =>
        {
            using var edit = preferences.Edit();
            edit?.PutBoolean(PrayerLocationReceiver.BackgroundLocationKey, args.IsChecked)?.Apply();
            PrayerLocationReceiver.Sync(this);
            Show(Screen.Settings);
        };
        content.AddView(toggle, MatchWidth(top: 8));

        if (enabled && !PrayerLocationReceiver.HasPermission(this))
        {
            var instruction = OperatingSystem.IsAndroidVersionAtLeast(29)
                ? "Vælg Tilladelser → Placering → Tillad altid, og slå præcis placering til."
                : "Giv Salah Times tilladelse til placering i Androids appindstillinger.";
            content.AddView(Label(instruction, 14, Muted), MatchWidth(top: 8));
            content.AddView(ActionButton("Åbn Androids appindstillinger", OpenBackgroundLocationSettings), MatchWidth(top: 7));
        }
        else if (enabled)
        {
            content.AddView(Label("Widgetten og påmindelserne følger dit sted, også når appen er lukket.",
                14, Muted), MatchWidth(top: 8));
        }
    }

    private void OpenBackgroundLocationSettings()
    {
        using var intent = new Intent(DeviceSettings.ActionApplicationDetailsSettings,
            global::Android.Net.Uri.FromParts("package", PackageName, null));
        StartActivity(intent);
    }

    private void SetSchedulePlace(PrayerCity selection, string? selectedId)
    {
        loadedPlaceId = selectedId;
        if (place.Slug != selection.Slug)
        {
            place = selection;
            schedule = new LocalPrayerSchedule(selection);
        }
    }

    private void LoadBackgroundPlace()
    {
        var selectedId = preferences.GetString(PrayerPreferences.PlaceKey, null);
        if (selectedId == loadedPlaceId)
        {
            return;
        }

        SetSchedulePlace(PrayerPreferences.SelectedPlace(this, preferences), selectedId);
        CancelLocationLookup();
        locationSuggestion = null;
        locationStatus = null;
        if (screen is not (Screen.Places or Screen.Method))
        {
            Show(screen);
        }
    }

    public void OnSharedPreferenceChanged(ISharedPreferences? sharedPreferences, string? key)
    {
        if (foreground && key == PrayerPreferences.PlaceKey)
        {
            LoadBackgroundPlace();
        }
    }
}
