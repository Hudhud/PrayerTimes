using Application.Calendars;

namespace Web.ViewModels;

public sealed record WebsiteSettingsViewModel(PrayerCity City, bool Hanafi, TwilightAngles Angles, string ReturnUrl)
{
    public string AsrMethod => Hanafi ? "hanafi" : "standard";
}
