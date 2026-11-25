using UIKit;

namespace SalahTimes.iOS;

internal sealed class SalahTabs : UITabBarController
{
    private readonly PrayerAppState state;
    private readonly PrayerViewController[] screens;
    private readonly UINavigationController[] tabs;

    public SalahTabs(PrayerAppState state, PrayerReminders reminders)
    {
        this.state = state;
        screens = [new TodayViewController(state), new CalendarViewController(state), new SettingsViewController(state, reminders)];
        tabs = screens.Select(screen => new UINavigationController(screen)).ToArray();
        var icons = new[] { "sun.max", "calendar", "gearshape" };
        for (var index = 0; index < tabs.Length; index++)
        {
            tabs[index].TabBarItem = new UITabBarItem("", UIImage.GetSystemImage(icons[index]), index);
        }
        ViewControllers = tabs;
        Refresh();
        state.Changed += Refresh;
    }

    private void Refresh()
    {
        var titles = new[] { state.Text("I dag", "Today"), state.Text("Kalender", "Calendar"), state.Text("Indstillinger", "Settings") };
        for (var index = 0; index < screens.Length; index++)
        {
            screens[index].Title = titles[index];
            tabs[index].TabBarItem.Title = titles[index];
            screens[index].Refresh();
        }
    }
}
