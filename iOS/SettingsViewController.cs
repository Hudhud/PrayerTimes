using Application.Calendars;
using Foundation;
using UIKit;

namespace SalahTimes.iOS;

internal sealed class SettingsViewController(PrayerAppState state, PrayerReminders reminders) : PrayerViewController(state)
{
    protected override void Render()
    {
        Content.AddArrangedSubview(Card(
            Label(T("Placering", "Location"), UIFontTextStyle.Headline),
            Label($"{State.Place.Name} · {State.Place.TimeZoneId}"),
            Button(T("Skift sted", "Change location"), OpenPlaces)));

        var language = new UISegmentedControl(new[] { "Dansk", "English" }) { SelectedSegment = State.English ? 1 : 0 };
        language.AccessibilityLabel = T("Sprog", "Language");
        language.ValueChanged += (_, _) => State.SetLanguage(language.SelectedSegment == 1);
        Content.AddArrangedSubview(Card(Label(T("Sprog", "Language"), UIFontTextStyle.Headline), language));

        var asr = new UISegmentedControl(new[] { "Standard", "Hanafi" }) { SelectedSegment = State.Hanafi ? 1 : 0 };
        asr.AccessibilityLabel = T("Asr-metode", "Asr method");
        asr.ValueChanged += (_, _) => State.SetHanafi(asr.SelectedSegment == 1);
        var angles = State.Angles;
        Content.AddArrangedSubview(Card(
            Label(T("Beregning", "Calculation"), UIFontTextStyle.Headline), asr,
            Label($"Fajr {angles.Fajr.ToString("0.#", State.Culture)}° · Isha {angles.Isha.ToString("0.#", State.Culture)}°"),
            Button(T("Skift vinkler", "Change angles"), ShowAngles)));

        Content.AddArrangedSubview(Card(
            Label(T("Notifikationer", "Notifications"), UIFontTextStyle.Headline),
            ReminderRow(T("Påmindelse 30 min. før bøn", "Reminder 30 minutes before prayer"), early: true),
            ReminderRow(T("Besked ved bønnetid", "Notification at prayer time"), early: false),
            Label(T("Åbn appen jævnligt for at opdatere kommende påmindelser.",
                "Open the app regularly to refresh upcoming reminders."), UIFontTextStyle.Footnote)));
        Content.AddArrangedSubview(Card(
            Button(T("Steddata fra GeoNames", "Place data from GeoNames"), () => OpenLink("https://www.geonames.org/")),
            Button("CC BY 4.0", () => OpenLink("https://creativecommons.org/licenses/by/4.0/"))));
    }

    private UIStackView ReminderRow(string title, bool early)
    {
        var toggle = new UISwitch { On = early ? State.EarlyReminder : State.PrayerReminder, AccessibilityLabel = title };
        toggle.ValueChanged += async (_, _) =>
        {
            if (!toggle.On)
            {
                State.SetReminder(early, enabled: false);
                return;
            }

            toggle.Enabled = false;
            try
            {
                if (await reminders.RequestPermissionAsync())
                {
                    State.SetReminder(early, enabled: true);
                }
                else
                {
                    toggle.On = false;
                    ShowMessage(T("Tillad notifikationer til Salah Times i iOS-indstillinger.",
                        "Allow notifications for Salah Times in iOS Settings."));
                }
            }
            catch (NSErrorException)
            {
                toggle.On = false;
                ShowMessage(T("Notifikationer kunne ikke aktiveres. Prøv igen.", "Notifications could not be enabled. Try again."));
            }
            finally
            {
                toggle.Enabled = true;
            }
        };
        var row = new UIStackView([Label(title), toggle])
        {
            Axis = UILayoutConstraintAxis.Horizontal,
            Alignment = UIStackViewAlignment.Center,
            Spacing = 8
        };
        toggle.SetContentHuggingPriority((float)UILayoutPriority.Required, UILayoutConstraintAxis.Horizontal);
        return row;
    }

    private void ShowAngles()
    {
        var editor = UIAlertController.Create(T("Fajr- og Isha-vinkler", "Fajr and Isha angles"),
            T("Vælg 5–30° under horisonten med højst én decimal.", "Choose 5–30° below the horizon, with at most one decimal place."),
            UIAlertControllerStyle.Alert);
        void SetField(UITextField field, string label, decimal angle)
        {
            field.Placeholder = label;
            field.AccessibilityLabel = label;
            field.KeyboardType = UIKeyboardType.DecimalPad;
            field.Text = angle.ToString("0.#", State.Culture);
        }
        editor.AddTextField(field => SetField(field, "Fajr (°)", State.Angles.Fajr));
        editor.AddTextField(field => SetField(field, "Isha (°)", State.Angles.Isha));
        editor.AddAction(UIAlertAction.Create(T("Annuller", "Cancel"), UIAlertActionStyle.Cancel, null));
        editor.AddAction(UIAlertAction.Create(T("Nulstil", "Reset"), UIAlertActionStyle.Default, _ => State.SetAngles(null)));
        editor.AddAction(UIAlertAction.Create(T("Gem", "Save"), UIAlertActionStyle.Default, _ =>
        {
            var fields = editor.TextFields;
            if (fields is { Length: 2 } && TwilightAngles.TryParse(fields[0].Text, fields[1].Text, out var angles))
            {
                State.SetAngles(angles);
                return;
            }
            ShowMessage(T("Kontrollér begge vinkler, før du gemmer.", "Check both angles before saving."));
        }));
        PresentViewController(editor, animated: true, null);
    }

    private void ShowMessage(string message)
    {
        var alert = UIAlertController.Create(null, message, UIAlertControllerStyle.Alert);
        alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, null));
        PresentViewController(alert, animated: true, null);
    }

    private static void OpenLink(string url) =>
        UIApplication.SharedApplication.OpenUrl(new NSUrl(url), new UIApplicationOpenUrlOptions(), null);
}
