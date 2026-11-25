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
            !PrayerLocationReceiver.UsesAutomaticLocation(preferences) ||
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

        if (!userInitiated)
        {
            return;
        }

        if (LocationPermissionBlocked)
        {
            ShowLocationFallback(T("Placering er slået fra for Salah Times. Du kan stadig vælge dit sted ovenfor.", "Location is off for Salah Times. You can still choose a place above."));
            returningFromLocationSettings = true;
            using var intent = new Intent(global::Android.Provider.Settings.ActionApplicationDetailsSettings,
                global::Android.Net.Uri.FromParts("package", PackageName, null));
            StartActivity(intent);
            return;
        }

        using var edit = preferences.Edit();
        edit?.PutBoolean(LocationPermissionAskedKey, true)?.Apply();
        locationPermissionPending = true;
        locationStatus = T("Tillad placering for at finde en by i nærheden.", "Allow location access to find a nearby city.");
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
            ShowLocationFallback(T("Placering blev ikke tilladt. Søg efter by eller postnummer.", "Location access was denied. Search by city or Danish postcode."));
        }
    }

    private void FindDeviceLocation()
    {
        if (GetSystemService(LocationService) is not LocationManager manager)
        {
            ShowLocationFallback(T("Placering er ikke tilgængelig. Vælg dit sted manuelt.", "Location is unavailable. Choose your place manually."));
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
            ShowLocationFallback(T("Slå placering til på telefonen, eller vælg dit sted manuelt.", "Turn on location on your phone or choose your place manually."));
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
        locationStatus = T("Finder din placering…", "Finding your location…");
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
            ShowLocationFallback(T("Placering er ikke tilgængelig. Vælg dit sted manuelt.", "Location is unavailable. Choose your place manually."));
        }
    }

    private void ReceivePosition(PositionListener listener, Location fix)
    {
        if (positionListener != listener)
        {
            return;
        }

        positionReceived = true;
        var accuracy = PrayerLocationReceiver.AccuracyInMetres(fix);
        var nearest = places.FindNearestCity(fix.Latitude, fix.Longitude);
        if (PreciseLocationAllowed && accuracy <= PrayerLocationReceiver.AutomaticAccuracyMetres)
        {
            CancelLocationLookup();
            if (nearest is null)
            {
                ShowLocationFallback(T("Der blev ikke fundet en by nær din placering.", "No city was found near your location."));
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
            ? T("Din placering er omtrentlig. Bekræft stedet, eller vælg et andet.", "Your location is approximate. Confirm the suggested place or choose another.")
            : positionReceived ? T("Der blev ikke fundet en by nær din placering.", "No city was found near your location.")
            : T("Din placering kunne ikke findes. Søg efter by eller postnummer.", "Your location could not be found. Search by city or Danish postcode."));
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
        UpdateLocationPanel();
    }

    private void RenderLocationRequired()
    {
        AddHeading(T("Find bønnetider for dit sted", "Find prayer times for your location"));
        AddPlaceSearch();
    }

    private void AddLocationPanel(int top = 0)
    {
        locationPanel = new LinearLayout(this) { Orientation = Orientation.Vertical };
        locationPanel.AccessibilityLiveRegion = AccessibilityLiveRegion.Polite;
        content.AddView(locationPanel, MatchWidth(top));
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
            panel.AddView(ActionButton(T($"Vælg {suggestion.Name}", $"Choose {suggestion.Name}"), () => ChoosePlace(suggestion, fromDevice: true),
                selected: true), MatchWidth(bottom: 8));
        }

        var busy = positionListener is not null || locationPermissionPending;
        if (screen == Screen.Places || !HasSelectedPlace)
        {
            var label = LocationPermissionBlocked && !locationPermissionPending ? T("Åbn appindstillinger", "Open app settings") : T("Brug min placering", "Use my location");
            var useLocation = ActionButton(label, () => RequestDeviceLocation(userInitiated: true), selected: !busy);
            useLocation.Enabled = !busy;
            panel.AddView(useLocation, MatchWidth(bottom: 8));
        }
        if (screen != Screen.Places && HasSelectedPlace && locationStatus is not null)
        {
            panel.AddView(ActionButton(T("Vælg sted manuelt", "Choose location manually"), OpenPlaces), MatchWidth(bottom: 8));
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
