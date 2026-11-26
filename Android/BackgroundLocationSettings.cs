using Android.App;
using Android.Content;
using Android.Views;
using Android.Widget;
using Application.Calendars;
using Calculation;
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
            Text = T("Opdatér sted i baggrunden", "Update location in the background"),
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
            if (args.IsChecked)
            {
                ShowBackgroundLocationDisclosure();
            }
            else
            {
                SaveBackgroundLocation(false);
            }
        };
        content.AddView(toggle, MatchWidth(top: 8));

        if (enabled && !PrayerLocationReceiver.HasPermission(this))
        {
            var instruction = OperatingSystem.IsAndroidVersionAtLeast(29)
                ? T("Vælg Tilladelser → Placering → Tillad altid, og slå præcis placering til.", "Choose Permissions → Location → Allow all the time and turn on precise location.")
                : T("Giv Salah Times tilladelse til placering i Androids appindstillinger.", "Allow Salah Times to access your location in Android app settings.");
            content.AddView(Label(instruction, 14, Muted), MatchWidth(top: 8));
            content.AddView(ActionButton(T("Åbn Androids appindstillinger", "Open Android app settings"), OpenBackgroundLocationSettings), MatchWidth(top: 7));
        }
        else if (enabled)
        {
            content.AddView(Label(T("Widgetten og påmindelserne følger dit sted, også når appen er lukket.", "The widget and reminders follow your location even when the app is closed."),
                14, Muted), MatchWidth(top: 8));
        }
    }

    private void ShowBackgroundLocationDisclosure()
    {
        var dialog = new AlertDialog.Builder(this)
            .SetTitle(T("Placering i baggrunden", "Background location"))!
            .SetMessage(T(
                "Salah Times bruger din placering til at opdatere widgetten og bønnepåmindelserne, også når appen er lukket eller ikke er i brug. Placeringen behandles på din telefon.",
                "Salah Times uses your location to update the widget and prayer reminders even when the app is closed or not in use. Your location is processed on your phone."))!
            .SetNegativeButton(T("Annuller", "Cancel"), (_, _) => Show(Screen.Settings, scrollPosition: scroll.ScrollY))!
            .SetPositiveButton(T("Fortsæt", "Continue"), (_, _) => SaveBackgroundLocation(true))!
            .Create()!;
        dialog.CancelEvent += (_, _) => Show(Screen.Settings, scrollPosition: scroll.ScrollY);
        dialog.Show();
    }

    private void SaveBackgroundLocation(bool enabled)
    {
        using var edit = preferences.Edit();
        edit?.PutBoolean(PrayerLocationReceiver.BackgroundLocationKey, enabled)?.Apply();
        PrayerLocationReceiver.Sync(this);
        Show(Screen.Settings, scrollPosition: scroll.ScrollY);
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
            var oldToday = DateOnly.FromDateTime(clock.NowIn(place.TimeZoneId).Date);
            place = selection;
            schedule = new LocalPrayerSchedule(selection, PrayerPreferences.Angles(preferences));
            if (month == MonthOf(oldToday) && selectedDate == oldToday)
            {
                var today = DateOnly.FromDateTime(clock.NowIn(place.TimeZoneId).Date);
                month = MonthOf(today);
                selectedDate = today;
            }
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
