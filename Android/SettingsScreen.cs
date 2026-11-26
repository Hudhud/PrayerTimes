using Android.App;
using Android.Content;
using Android.Text;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using Application.Calendars;

namespace SalahTimes.AndroidApp;

public sealed partial class MainActivity
{
    private void RenderSettings()
    {
        AddHeading(T("Indstillinger", "Settings"));
        AddSectionTitle(T("Sted", "Location"), top: 0);
        content.AddView(HasSelectedPlace
            ? ActionButton(place.Name, OpenPlaces, description: T($"Skift sted, nu {place.Name}", $"Change location, currently {place.Name}"))
            : ActionButton(T("Vælg sted", "Choose location"), OpenPlaces), MatchWidth());
        AddBackgroundLocationSetting();

        AddSectionTitle(T("Asr-metode", "Asr method"));
        var methods = new LinearLayout(this);
        methods.AddView(ActionButton("Standard", () => ChooseAsr(false), selected: !hanafi), new LinearLayout.LayoutParams(0, -2, 1));
        methods.AddView(ActionButton("Hanafi", () => ChooseAsr(true), selected: hanafi), new LinearLayout.LayoutParams(0, -2, 1) { LeftMargin = Dp(8) });
        content.AddView(methods, MatchWidth());
        content.AddView(Label(T("Standard lægger én genstandslængde til middagsskyggen. Hanafi lægger to til.", "Standard adds one object length to the noon shadow. Hanafi adds two."), 14, Muted), MatchWidth(top: 8));

        AddTwilightSettings();
        AddSectionTitle(T("Sprog", "Language"));
        var languages = new LinearLayout(this);
        languages.AddView(ActionButton("Dansk", () => ChooseLanguage(false), selected: !language.IsEnglish),
            new LinearLayout.LayoutParams(0, -2, 1));
        languages.AddView(ActionButton("English", () => ChooseLanguage(true), selected: language.IsEnglish),
            new LinearLayout.LayoutParams(0, -2, 1) { LeftMargin = Dp(8) });
        content.AddView(languages, MatchWidth());

        AddPrayerNotificationSettings();

        AddSectionTitle(T("Startskærm", "Home screen"));
        content.AddView(ActionButton(T("Tilføj 4 × 2-widget", "Add 4 × 2 widget"), AddHomeWidget), MatchWidth());

        AddSectionTitle(T("Om appen", "About"));
        content.AddView(Label(T("Tiderne beregnes på telefonen og vises i det valgte steds lokale tid.", "Prayer times are calculated on your phone and shown in the selected place’s time zone."), 14, Muted), MatchWidth(bottom: 10));
        content.AddView(ActionButton(T("Lokal tid og beregningsmetode", "Local time and calculation method"), OpenMethod), MatchWidth(bottom: 8));
        content.AddView(ActionButton(T("Privatliv", "Privacy"), () => OpenUrl(
            T("https://salahtimes.dk/privatliv#apps", "https://salahtimes.dk/en/privatliv#apps"))), MatchWidth(bottom: 8));
        content.AddView(ActionButton(T("Steddata fra GeoNames", "Place data from GeoNames"), () => OpenUrl("https://www.geonames.org/")), MatchWidth(bottom: 8));
        content.AddView(ActionButton(T("CC BY 4.0-licens", "CC BY 4.0 licence"), () => OpenUrl("https://creativecommons.org/licenses/by/4.0/")), MatchWidth());
    }

    private TwilightAngles ActiveAngles => PrayerPreferences.Angles(preferences) ?? TwilightAngles.ForPlace(place);

    private string AngleSummary => $"Fajr {ActiveAngles.Fajr.ToString("0.#", UiCulture)}° · Isha {ActiveAngles.Isha.ToString("0.#", UiCulture)}°";

    private void AddTwilightSettings()
    {
        AddSectionTitle(T("Fajr- og Isha-vinkler", "Fajr and Isha angles"));
        content.AddView(ActionButton(AngleSummary, OpenTwilightAngleSettings,
            description: T($"Skift vinkler. {AngleSummary}", $"Change angles. {AngleSummary}")), MatchWidth());
    }

    private void OpenTwilightAngleSettings()
    {
        var fields = new LinearLayout(this) { Orientation = Orientation.Vertical };
        fields.SetPadding(Dp(24), Dp(8), Dp(24), Dp(8));
        fields.AddView(Label(T("Vælg 5–30° under horisonten med højst én decimal.",
            "Choose 5–30° below the horizon, with at most one decimal place."), 14, Muted), MatchWidth(bottom: 10));
        var angles = ActiveAngles;
        var fajr = AddAngleField(fields, "Fajr (°)", angles.Fajr);
        var isha = AddAngleField(fields, "Isha (°)", angles.Isha);
        var pickerContent = new ScrollView(this) { FillViewport = true };
        pickerContent.AddView(fields);
        var dialog = new AlertDialog.Builder(this)
            .SetTitle(T("Fajr- og Isha-vinkler", "Fajr and Isha angles"))!
            .SetView(pickerContent)!
            .SetNeutralButton(T("Nulstil", "Reset"), (_, _) => SaveAngles(null))!
            .SetNegativeButton(T("Annuller", "Cancel"), (_, _) => { })!
            .SetPositiveButton(T("Gem", "Save"), (_, _) => { })!
            .Show()!;
        dialog.GetButton((int)DialogButtonType.Positive)!.Click += (_, _) =>
        {
            if (!TwilightAngles.TryParse(fajr.Text, isha.Text, out var selectedAngles))
            {
                isha.Error = T("Kontrollér begge vinkler, før du gemmer.", "Check both angles before saving.");
                return;
            }

            dialog.Dismiss();
            SaveAngles(selectedAngles);
        };
    }

    private EditText AddAngleField(LinearLayout fields, string name, decimal angle)
    {
        var field = new EditText(this)
        {
            Text = angle.ToString("0.#", UiCulture),
            ContentDescription = name,
            InputType = InputTypes.ClassNumber | InputTypes.NumberFlagDecimal
        };
        field.SetSingleLine();
        fields.AddView(Label(name, 14, Muted), MatchWidth());
        fields.AddView(field, MatchWidth(bottom: 8));
        return field;
    }

    private void SaveAngles(TwilightAngles? angles)
    {
        using var change = preferences.Edit();
        if (angles is null)
        {
            change?.Remove(PrayerPreferences.AngleKey);
        }
        else
        {
            change?.PutString(PrayerPreferences.AngleKey, angles.PreferenceValue);
        }
        change?.Apply();
        schedule = new LocalPrayerSchedule(place, angles);
        PrayerNotificationReceiver.Reschedule(this);
        PrayerWidgetProvider.Refresh(this);
        HideKeyboard();
        Show(Screen.Settings, scrollPosition: scroll.ScrollY);
    }

    private void AddHomeWidget()
    {
        if (!PrayerWidgetProvider.RequestPin(this))
        {
            Toast.MakeText(this, T("Hold fingeren nede på startskærmen, og vælg Widgets → Salah Times 4 × 2.", "Touch and hold your home screen, then choose Widgets → Salah Times 4 × 2."),
                ToastLength.Long)?.Show();
        }
    }

    private void ChooseAsr(bool choice)
    {
        if (hanafi != choice)
        {
            hanafi = choice;
            using var change = preferences.Edit();
            change?.PutBoolean(PrayerPreferences.HanafiKey, choice)?.Apply();
            PrayerNotificationReceiver.Reschedule(this);
            PrayerWidgetProvider.Refresh(this);
            Show(Screen.Settings);
        }
    }

    private void RenderPlaces()
    {
        AddHeading(T("Vælg sted", "Choose location"));
        AddPlaceSearch();
    }

    private void AddPlaceSearch()
    {
        AddSectionTitle(T("Søg efter by eller dansk postnummer", "Search by city or Danish postcode"), top: 8);
        var input = new EditText(this)
        {
            Hint = T("By eller dansk postnummer", "City or Danish postcode"), TextSize = 16,
            InputType = InputTypes.ClassText | InputTypes.TextFlagCapWords,
            ImeOptions = ImeAction.Search,
            ContentDescription = T("By eller dansk postnummer", "City or Danish postcode")
        };
        input.SetSingleLine();
        input.SetMinimumHeight(Dp(52));
        input.SetPadding(Dp(14), Dp(6), Dp(14), Dp(6));
        input.SetTextColor(Text);
        input.SetHintTextColor(Muted);
        input.Background = Rounded(Surface, 14, Outline);
        content.AddView(input, MatchWidth(bottom: 14));
        var results = new LinearLayout(this) { Orientation = Orientation.Vertical };
        content.AddView(results, MatchWidth());
        AddLocationPanel(top: 12);

        IReadOnlyList<PrayerPlace> matches = [];
        var recent = RecentPlaceIds().Select(id => places.Find(id)).OfType<PrayerCity>().ToArray();
        void Search()
        {
            var query = input.Text?.Trim() ?? "";
            results.RemoveAllViews();
            matches = [];
            if (query.Length == 0)
            {
                var suggestions = recent.Length == 0 ? PrayerCities.All : recent;
                results.AddView(Label(recent.Length == 0 ? T("Vælg en by", "Choose a city") : T("Senest valgte steder", "Recent places"),
                    16, Text, bold: true, heading: true), MatchWidth(bottom: 4));
                foreach (var suggestion in suggestions)
                {
                    AddPlaceResult(results, suggestion);
                }
                return;
            }

            if (query.Length < 2)
            {
                results.AddView(Label(T("Skriv mindst to tegn", "Enter at least two characters"), 14, Muted), MatchWidth());
                return;
            }
            matches = places.Search(query);
            if (matches.Count == 0)
            {
                var suggestions = places.SuggestSpelling(query);
                results.AddView(Label(suggestions.Count == 0 ? T("Ingen steder fundet", "No places found") : T("Mente du?", "Did you mean?"),
                    15, Muted, heading: true), MatchWidth(bottom: 4));
                matches = suggestions;
            }
            foreach (var match in matches.Take(12))
            {
                var city = places.Find(match.Id) ?? match.ToPrayerCity();
                var area = match.Area == match.Name ? null : match.Area;
                AddPlaceResult(results, city, area);
            }
        }

        input.TextChanged += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(input.Text))
            {
                CancelLocationLookup();
            }
            Search();
        };
        input.EditorAction += (_, args) =>
        {
            if (args.ActionId == ImeAction.Search && matches.Count == 1)
            {
                ChoosePlace(places.Find(matches[0].Id) ?? matches[0].ToPrayerCity());
                args.Handled = true;
            }
        };
        Search();
    }

    private void AddPlaceResult(LinearLayout list, PrayerCity city, string? area = null)
    {
        var action = ActionButton(area is null ? city.Name : $"{city.Name}\n{area}", () => ChoosePlace(city));
        action.Gravity = GravityFlags.CenterVertical | GravityFlags.Left;
        action.SetPadding(Dp(13), Dp(8), Dp(13), Dp(8));
        list.AddView(action, MatchWidth(top: 6));
    }

    private string[] RecentPlaceIds() =>
        (preferences.GetString("recent-places", "") ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries);

    private void ChoosePlace(PrayerCity selection, bool fromDevice = false)
    {
        CancelLocationLookup();
        locationStatus = null;
        locationSuggestion = null;
        SetSchedulePlace(selection, selection.Slug);
        var recent = RecentPlaceIds().Where(id => id != selection.Slug).Prepend(selection.Slug).Take(5);
        PrayerPreferences.SavePlace(preferences, selection, fromDevice, string.Join('|', recent));
        PrayerLocationReceiver.Sync(this);
        PrayerNotificationReceiver.Reschedule(this);
        PrayerWidgetProvider.Refresh(this);
        HideKeyboard();
        var target = screen == Screen.Places ? returnScreen : screen;
        Show(target, jumpToSelected: target == Screen.Calendar);
    }

    private void RenderMethod()
    {
        var helpTitle = T("Lyse nætter og polardage", "Bright nights and polar days");
        var help = T("En streg betyder, at en solbegivenhed ikke kan beregnes for stedet og dagen. Spørg din lokale moské eller en lærd, du har tillid til, om en egnet bønneplan. Isha kan falde efter midnat. Det fremgår ved tiden.",
            "A dash means a solar event cannot be calculated for the chosen place and day. Ask your local mosque or a trusted scholar about an appropriate timetable. Isha can occur after midnight and is marked when it does.");
        if (solarEventHelp)
        {
            AddHeading(helpTitle);
            AddMethodParagraph(help);
            return;
        }

        AddHeading(T("Sådan beregnes bønnetiderne", "How prayer times are calculated"),
            T("Valgt steds lokale tid", "Local time at the selected place"));
        AddMethodSection(T("Beregningen", "Calculation"),
            T($"Salah Times beregner solens position for det valgte sted. Dine vinkler under horisonten er {AngleSummary}. Du kan ændre dem under Indstillinger. Tiderne rundes op til nærmeste minut.",
              $"Salah Times calculates the sun’s position for the selected place. Your angles below the horizon are {AngleSummary}. You can change them in Settings. Times are rounded up to the next minute."));
        AddMethodSection("Dhuhr", T("Ved solmiddag står solen højest på himlen. Dhuhr-bønnen indtræder, så snart solen har passeret det punkt og begynder at synke mod vest.", "The sun is highest at solar noon. Dhuhr begins once the sun has cleared the meridian."));
        AddMethodSection("Asr", T("Asr er den islamiske eftermiddagsbøn. Med standardmetoden indtræder Asr, når skyggen af en lodret genstand er lige så lang som genstanden selv plus dens skyggelængde ved solmiddag. Med Hanafi-metoden lægger man to gange genstandens længde til skyggelængden ved solmiddag.", "Asr is the afternoon prayer. The Standard method adds one object length to its noon shadow. The Hanafi method adds two."));
        AddMethodSection(helpTitle, help);
    }

    private void AddMethodSection(string title, string body)
    {
        AddSectionTitle(title, top: 8);
        AddMethodParagraph(body);
    }

    private void AddMethodParagraph(string body)
    {
        var paragraph = Label(body, 16, Muted);
        paragraph.SetLineSpacing(Dp(4), 1);
        content.AddView(paragraph, MatchWidth(bottom: 10));
    }

    private void OpenUrl(string url)
    {
        try
        {
            StartActivity(new Intent(Intent.ActionView, global::Android.Net.Uri.Parse(url)));
        }
        catch (ActivityNotFoundException)
        {
            Toast.MakeText(this, T("Ingen browser fundet", "No browser found"), ToastLength.Short)?.Show();
        }
    }
}
