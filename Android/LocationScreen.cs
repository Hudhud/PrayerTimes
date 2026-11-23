using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Locations;
using Android.OS;
using Android.Views;
using Android.Widget;
using Application.Calendars;
using PermissionNames = Android.Manifest.Permission;

namespace SalahTimes.AndroidApp;

public sealed partial class MainActivity
{
    private const int LocationPermissionRequest = 21;
    private const string LocationPermissionAskedKey = "location-permission-asked";
    private const float AutomaticAccuracyMetres = 1_000;
    private const long LocationTimeoutMilliseconds = 20_000;
    private const long RetryIntervalMilliseconds = 120_000;
    private const long MaximumCachedFixAgeNanoseconds = 60_000_000_000L;

    private bool locationPermissionPending;
    private bool returningFromLocationSettings;
    private long? lastLocationAttempt;
    private LocationManager? locationManager;
    private PositionListener? positionListener;
    private Java.Lang.Runnable? locationTimeout;
    private PrayerCity? approximatePlace;
    private float approximateAccuracy;
    private bool positionReceived;

    private LinearLayout? locationPanel;
    private string? locationStatus;
    private PrayerCity? locationSuggestion;

    private bool HasSelectedPlace => preferences.Contains(PrayerPreferences.PlaceKey);
    private bool PreciseLocationAllowed =>
        CheckSelfPermission(PermissionNames.AccessFineLocation) == Permission.Granted;
    private bool HasLocationPermission =>
        PreciseLocationAllowed || CheckSelfPermission(PermissionNames.AccessCoarseLocation) == Permission.Granted;
    private bool LocationPermissionAsked => preferences.GetBoolean(LocationPermissionAskedKey, false);
    private bool LocationPermissionBlocked => !HasLocationPermission && LocationPermissionAsked &&
        !ShouldShowRequestPermissionRationale(PermissionNames.AccessCoarseLocation) &&
        !ShouldShowRequestPermissionRationale(PermissionNames.AccessFineLocation);

    private void TryAutomaticLocation()
    {
        if (returningFromLocationSettings)
        {
            returningFromLocationSettings = false;
            if (HasLocationPermission)
            {
                RequestDeviceLocation(userInitiated: true);
            }
            else
            {
                UpdateLocationPanel();
            }
            return;
        }

        if (screen is Screen.Places or Screen.Method || positionListener is not null || locationPermissionPending ||
            preferences.GetBoolean(PrayerPreferences.ManualLocationKey, false) ||
            (lastLocationAttempt is { } previous && SystemClock.ElapsedRealtime() - previous < RetryIntervalMilliseconds))
        {
            return;
        }

        RequestDeviceLocation(userInitiated: false);
    }

    private void RequestDeviceLocation(bool userInitiated)
    {
        if (positionListener is not null || locationPermissionPending)
        {
            return;
        }

        lastLocationAttempt = SystemClock.ElapsedRealtime();
        locationSuggestion = null;
        if (HasLocationPermission)
        {
            FindDeviceLocation();
            return;
        }

        if (LocationPermissionBlocked || (!userInitiated && LocationPermissionAsked))
        {
            ShowLocationFallback("Giv adgang til placering, eller søg efter by eller postnummer.");
            if (userInitiated && LocationPermissionBlocked)
            {
                returningFromLocationSettings = true;
                StartActivity(new Intent(global::Android.Provider.Settings.ActionApplicationDetailsSettings,
                    global::Android.Net.Uri.FromParts("package", PackageName, null)));
            }
            return;
        }

        using var edit = preferences.Edit();
        edit?.PutBoolean(LocationPermissionAskedKey, true)?.Apply();
        locationPermissionPending = true;
        locationStatus = "Tillad placering for at finde en by i nærheden.";
        UpdateLocationPanel();
        RequestPermissions([PermissionNames.AccessFineLocation, PermissionNames.AccessCoarseLocation], LocationPermissionRequest);
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        if (requestCode == PrayerNotificationPermissionRequest)
        {
            RefreshPrayerNotificationSettings();
            PrayerWidgetProvider.Refresh(this);
            return;
        }
        if (requestCode != LocationPermissionRequest)
        {
            return;
        }

        locationPermissionPending = false;
        lastLocationAttempt = SystemClock.ElapsedRealtime();
        if (HasLocationPermission)
        {
            FindDeviceLocation();
        }
        else
        {
            ShowLocationFallback("Placering blev ikke tilladt. Søg efter by eller postnummer.");
        }
    }

    private void FindDeviceLocation()
    {
        if (GetSystemService(LocationService) is not LocationManager manager)
        {
            ShowLocationFallback("Placering er ikke tilgængelig. Vælg dit sted manuelt.");
            return;
        }

        var providers = new List<string>(3);
        if (PreciseLocationAllowed && manager.IsProviderEnabled(LocationManager.GpsProvider))
        {
            providers.Add(LocationManager.GpsProvider);
        }
        if (manager.IsProviderEnabled(LocationManager.NetworkProvider))
        {
            providers.Add(LocationManager.NetworkProvider);
        }
        if (OperatingSystem.IsAndroidVersionAtLeast(31) && manager.IsProviderEnabled(LocationManager.FusedProvider))
        {
            providers.Add(LocationManager.FusedProvider);
        }
        if (providers.Count == 0)
        {
            ShowLocationFallback("Slå placering til på telefonen, eller vælg dit sted manuelt.");
            return;
        }

        approximatePlace = null;
        approximateAccuracy = float.PositiveInfinity;
        positionReceived = false;
        locationManager = manager;
        var listener = new PositionListener(ReceivePosition);
        positionListener = listener;
        locationTimeout = new Java.Lang.Runnable(CompleteLocationLookup);
        handler.PostDelayed(locationTimeout, LocationTimeoutMilliseconds);
        locationStatus = "Finder din placering…";
        UpdateLocationPanel();

        try
        {
            foreach (var provider in providers)
            {
                manager.RequestLocationUpdates(provider, 1000L, 0f, listener, Looper.MainLooper);
                using var cached = manager.GetLastKnownLocation(provider);
                if (cached is not null &&
                    SystemClock.ElapsedRealtimeNanos() - cached.ElapsedRealtimeNanos is >= 0 and <= MaximumCachedFixAgeNanoseconds)
                {
                    ReceivePosition(listener, cached);
                    if (positionListener is null)
                    {
                        return;
                    }
                }
            }
        }
        catch (Java.Lang.SecurityException)
        {
            CancelLocationLookup();
            ShowLocationFallback("Placering er ikke tilgængelig. Vælg dit sted manuelt.");
        }
    }

    private void ReceivePosition(PositionListener listener, Location fix)
    {
        if (positionListener != listener)
        {
            return;
        }

        positionReceived = true;
        var accuracy = fix.HasAccuracy && float.IsFinite(fix.Accuracy) && fix.Accuracy > 0
            ? fix.Accuracy : float.PositiveInfinity;
        var nearest = places.FindNearestCity(fix.Latitude, fix.Longitude);
        if (PreciseLocationAllowed && accuracy <= AutomaticAccuracyMetres)
        {
            CancelLocationLookup();
            if (nearest is null)
            {
                ShowLocationFallback("Der blev ikke fundet en dansk by nær din placering.");
            }
            else if (!HasSelectedPlace || nearest.Slug != place.Slug ||
                !preferences.GetBoolean(PrayerPreferences.FoundByLocationKey, false))
            {
                ChoosePlace(nearest, fromDevice: true);
            }
            return;
        }

        if (nearest is not null && (approximatePlace is null || accuracy < approximateAccuracy))
        {
            approximatePlace = nearest;
            approximateAccuracy = accuracy;
        }
        if (!PreciseLocationAllowed && approximatePlace is not null)
        {
            CompleteLocationLookup();
        }
    }

    private void CompleteLocationLookup()
    {
        CancelLocationLookup();
        locationSuggestion = approximatePlace;
        ShowLocationFallback(approximatePlace is not null
            ? "Din placering er omtrentlig. Bekræft stedet, eller vælg et andet."
            : positionReceived ? "Der blev ikke fundet en dansk by nær din placering."
            : "Din placering kunne ikke findes. Søg efter by eller postnummer.");
    }

    private void CancelLocationLookup()
    {
        if (positionListener is not { } listener)
        {
            return;
        }

        positionListener = null;
        locationManager?.RemoveUpdates(listener);
        locationManager = null;
        if (locationTimeout is { } timeout)
        {
            handler.RemoveCallbacks(timeout);
            locationTimeout = null;
        }

        locationStatus = null;
        UpdateLocationPanel();
    }

    private void PauseLocationLookup()
    {
        if (positionListener is not null)
        {
            lastLocationAttempt = null;
            CancelLocationLookup();
        }
    }

    private void ShowLocationFallback(string message)
    {
        locationStatus = message;
        if (!HasSelectedPlace && screen is Screen.Today or Screen.Calendar)
        {
            OpenPlaces();
        }
        else
        {
            UpdateLocationPanel();
        }
    }

    private void RenderLocationRequired()
    {
        AddHeading("Find bønnetider for dit sted");
        AddLocationPanel();
    }

    private void AddLocationPanel()
    {
        locationPanel = new LinearLayout(this) { Orientation = Orientation.Vertical };
        locationPanel.AccessibilityLiveRegion = AccessibilityLiveRegion.Polite;
        content.AddView(locationPanel, MatchWidth());
        UpdateLocationPanel();
    }

    private void UpdateLocationPanel()
    {
        if (locationPanel is not { } panel)
        {
            return;
        }

        panel.RemoveAllViews();
        if (locationStatus is { } message)
        {
            panel.AddView(Label(message, 14, Muted), MatchWidth(bottom: 8));
        }
        if (locationSuggestion is { } suggestion)
        {
            panel.AddView(ActionButton($"Vælg {suggestion.Name}", () => ChoosePlace(suggestion, fromDevice: true),
                selected: true), MatchWidth(bottom: 8));
        }

        var busy = positionListener is not null || locationPermissionPending;
        if (screen == Screen.Places || !HasSelectedPlace)
        {
            var label = LocationPermissionBlocked && !locationPermissionPending ? "Åbn appindstillinger" : "Brug min placering";
            var useLocation = ActionButton(label, () => RequestDeviceLocation(userInitiated: true), selected: !busy);
            useLocation.Enabled = !busy;
            panel.AddView(useLocation, MatchWidth(bottom: 8));
        }
        if (screen != Screen.Places && (!HasSelectedPlace || locationStatus is not null))
        {
            panel.AddView(ActionButton("Vælg sted manuelt", OpenPlaces), MatchWidth(bottom: 8));
        }
    }

    private sealed class PositionListener(Action<PositionListener, Location> onLocation)
        : Java.Lang.Object, ILocationListener
    {
        public void OnLocationChanged(Location location) => onLocation(this, location);
        public void OnProviderDisabled(string provider) { }
        public void OnProviderEnabled(string provider) { }
        public void OnStatusChanged(string? provider, Availability status, Bundle? extras) { }
    }
}
