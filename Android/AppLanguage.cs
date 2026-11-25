using System.Globalization;
using Android.Content;

namespace SalahTimes.AndroidApp;

internal sealed class AppLanguage(ISharedPreferences preferences)
{
    internal const string Key = "language";
    internal bool IsEnglish => preferences.GetString(Key, "da") == "en";
    internal CultureInfo Culture => CultureInfo.GetCultureInfo(IsEnglish ? "en-GB" : "da-DK");
    internal string Text(string danish, string english) => IsEnglish ? english : danish;
}

public sealed partial class MainActivity
{
    private AppLanguage language = null!;
    private CultureInfo UiCulture => language.Culture;
    private string T(string danish, string english) => language.Text(danish, english);

    private void ChooseLanguage(bool english)
    {
        using var change = preferences.Edit();
        change?.PutString(AppLanguage.Key, english ? "en" : "da")?.Apply();
        PrayerNotificationReceiver.Reschedule(this);
        PrayerWidgetProvider.Refresh(this);
        Show(Screen.Settings, scrollPosition: scroll.ScrollY);
    }
}
