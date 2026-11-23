using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Locations;
using Android.OS;
using PermissionNames = Android.Manifest.Permission;

namespace SalahTimes.AndroidApp;

[BroadcastReceiver(Exported = false)]
[IntentFilter(new[] { Intent.ActionBootCompleted, Intent.ActionMyPackageReplaced })]
public sealed class PrayerLocationReceiver : BroadcastReceiver
{
    internal const string BackgroundLocationKey = "background-location";
    internal const float AutomaticAccuracyMetres = 1_000;
    private const string LocationAction = "dk.salahtimes.android.PRAYER_LOCATION";
    private const long IntervalMilliseconds = 15 * 60 * 1000;
    private const float MinimumDistanceMetres = 750;
    private const long MaximumFixAgeNanoseconds = 5 * 60 * 1_000_000_000L;
    private static long lastFixTimestamp;

    internal static bool HasPermission(Context context) =>
        context.CheckSelfPermission(PermissionNames.AccessFineLocation) == Permission.Granted &&
        (!OperatingSystem.IsAndroidVersionAtLeast(29) ||
         context.CheckSelfPermission(PermissionNames.AccessBackgroundLocation) == Permission.Granted);

    internal static bool UsesAutomaticLocation(ISharedPreferences settings) =>
        !settings.GetBoolean(PrayerPreferences.ManualLocationKey, false);

    internal static float AccuracyInMetres(Location fix) =>
        fix.HasAccuracy && float.IsFinite(fix.Accuracy) && fix.Accuracy > 0
            ? fix.Accuracy : float.PositiveInfinity;

    private static bool ShouldTrack(Context context, ISharedPreferences settings) =>
        UsesAutomaticLocation(settings) && settings.GetBoolean(BackgroundLocationKey, false) && HasPermission(context);

    internal static void Sync(Context context)
    {
        if (context.GetSystemService(Context.LocationService) is not LocationManager manager)
        {
            return;
        }

        var settings = PrayerPreferences.Open(context);
        var enabled = ShouldTrack(context, settings);
        using var receiver = new Intent(context, typeof(PrayerLocationReceiver)).SetAction(LocationAction);
        // LocationManager adds the fix to this explicit intent when it delivers an update.
        var flags = OperatingSystem.IsAndroidVersionAtLeast(31) ? PendingIntentFlags.Mutable : 0;
        flags |= enabled ? PendingIntentFlags.UpdateCurrent : PendingIntentFlags.NoCreate;
        using var pending = PendingIntent.GetBroadcast(context, 0, receiver, flags);
        if (pending is null)
        {
            return;
        }

        try
        {
            manager.RemoveUpdates(pending);
            if (!enabled)
            {
                pending.Cancel();
                return;
            }

            manager.RequestLocationUpdates(LocationManager.PassiveProvider, 0L, MinimumDistanceMetres, pending);
            var providers = manager.AllProviders;
            if (OperatingSystem.IsAndroidVersionAtLeast(31) && providers?.Contains(LocationManager.FusedProvider) == true)
            {
                using var builder = new LocationRequest.Builder(IntervalMilliseconds);
                using var request = builder.SetQuality((int)LocationRequestQuality.BalancedPowerAccuracy)
                    .SetMinUpdateDistanceMeters(MinimumDistanceMetres).Build();
                manager.RequestLocationUpdates(LocationManager.FusedProvider, request, pending);
            }
            else
            {
                foreach (var provider in new[] { LocationManager.NetworkProvider, LocationManager.GpsProvider })
                {
                    if (providers?.Contains(provider) == true)
                    {
                        manager.RequestLocationUpdates(provider, IntervalMilliseconds, MinimumDistanceMetres, pending);
                    }
                }
            }
        }
        catch (Java.Lang.SecurityException)
        {
            pending.Cancel();
        }
    }

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null || intent is null)
        {
            return;
        }

        if (intent.Action is Intent.ActionBootCompleted or Intent.ActionMyPackageReplaced)
        {
            Sync(context);
            return;
        }

        var settings = PrayerPreferences.Open(context);
        if (intent.Action != LocationAction || !ShouldTrack(context, settings))
        {
            return;
        }

        using var fix = ReadFix(intent);
        if (fix is null || AccuracyInMetres(fix) > AutomaticAccuracyMetres ||
            fix.ElapsedRealtimeNanos <= lastFixTimestamp ||
            SystemClock.ElapsedRealtimeNanos() - fix.ElapsedRealtimeNanos is < 0 or > MaximumFixAgeNanoseconds)
        {
            return;
        }

        lastFixTimestamp = fix.ElapsedRealtimeNanos;
        try
        {
            var nearby = PrayerPreferences.Places(context).FindNearestCity(fix.Latitude, fix.Longitude);
            if (nearby is null || nearby.Slug == settings.GetString(PrayerPreferences.PlaceKey, null))
            {
                return;
            }

            PrayerPreferences.SavePlace(settings, nearby, fromDevice: true);
            PrayerNotificationReceiver.Reschedule(context);
            PrayerWidgetProvider.Refresh(context);
        }
        catch (Exception error) when (error is IOException or InvalidDataException or TimeZoneNotFoundException or Java.IO.IOException)
        {
            global::Android.Util.Log.Warn("SalahTimes", error.Message);
        }
    }

    private static Location? ReadFix(Intent intent)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            using var type = Java.Lang.Class.FromType(typeof(Location));
            return intent.GetParcelableExtra(LocationManager.KeyLocationChanged, type) as Location;
        }

        return intent.GetParcelableExtra(LocationManager.KeyLocationChanged) as Location;
    }
}
