using Foundation;
using UIKit;

namespace SalahTimes.iOS;

internal static class Program
{
    public static void Main(string[] args) => UIApplication.Main(args, null, typeof(AppDelegate));
}

[Register("AppDelegate")]
public sealed class AppDelegate : UIApplicationDelegate
{
    public override UISceneConfiguration GetConfiguration(UIApplication application, UISceneSession connectingSceneSession,
        UISceneConnectionOptions options) =>
        new("Default Configuration", connectingSceneSession.Role) { DelegateType = typeof(SceneDelegate) };
}

[Register("SalahTimesSceneDelegate")]
public sealed class SceneDelegate : UIWindowSceneDelegate
{
    private PrayerReminders? reminders;
    public override UIWindow? Window { get; set; }

    public override void WillConnect(UIScene scene, UISceneSession session, UISceneConnectionOptions connectionOptions)
    {
        if (scene is not UIWindowScene windowScene)
        {
            return;
        }

        var state = new PrayerAppState();
        reminders = new PrayerReminders(state);
        state.Changed += () => _ = reminders.RefreshAsync();
        Window = new UIWindow(windowScene) { RootViewController = new SalahTabs(state, reminders) };
        Window.MakeKeyAndVisible();
    }

    public override void DidBecomeActive(UIScene scene)
    {
        if (reminders is not null)
        {
            _ = reminders.RefreshAsync();
        }
    }
}
