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
        AddHeading("Indstillinger", "Vælg sted og Asr-metode");
        AddSectionTitle("Sted", top: 0);
        content.AddView(ActionButton(place.Name, OpenPlaces, description: $"Skift sted, nu {place.Name}"), MatchWidth());

        AddSectionTitle("Asr-metode");
        var methods = new LinearLayout(this);
        methods.AddView(ActionButton("Standard", () => ChooseAsr(false), selected: !hanafi), new LinearLayout.LayoutParams(0, -2, 1));
        methods.AddView(ActionButton("Hanafi", () => ChooseAsr(true), selected: hanafi), new LinearLayout.LayoutParams(0, -2, 1) { LeftMargin = Dp(8) });
        content.AddView(methods, MatchWidth());
        content.AddView(Label("Standard lægger én genstandslængde til middagsskyggen. Hanafi lægger to til.", 14, Muted), MatchWidth(top: 8));

        AddSectionTitle("Startskærm");
        content.AddView(ActionButton("Tilføj 4 × 2-widget", AddHomeWidget), MatchWidth());

        AddSectionTitle("Om appen");
        content.AddView(Label("Tiderne beregnes på telefonen og vises altid i dansk lokal tid.", 14, Muted), MatchWidth(bottom: 10));
        content.AddView(ActionButton("Dansk lokal tid og beregningsmetode", OpenMethod), MatchWidth(bottom: 8));
        content.AddView(ActionButton("Steddata fra GeoNames", () => OpenUrl("https://www.geonames.org/")), MatchWidth(bottom: 8));
        content.AddView(ActionButton("CC BY 4.0-licens", () => OpenUrl("https://creativecommons.org/licenses/by/4.0/")), MatchWidth());
    }

    private void AddHomeWidget()
    {
        if (!PrayerWidgetProvider.RequestPin(this))
        {
            Toast.MakeText(this, "Hold fingeren nede på startskærmen, og vælg Widgets → Salah Times 4 × 2.",
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
            PrayerWidgetProvider.Refresh(this);
            Show(Screen.Settings);
        }
    }

    private void RenderPlaces()
    {
        AddHeading("Skift sted", "Søg efter en dansk by eller et postnummer");
        var input = new EditText(this)
        {
            Hint = "By eller postnummer", TextSize = 16,
            InputType = InputTypes.ClassText | InputTypes.TextFlagCapWords,
            ImeOptions = ImeAction.Search,
            ContentDescription = "By eller postnummer"
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
                results.AddView(Label(recent.Length == 0 ? "Vælg en by" : "Senest valgte steder",
                    16, Text, bold: true, heading: true), MatchWidth(bottom: 4));
                foreach (var suggestion in suggestions)
                {
                    AddPlaceResult(results, suggestion);
                }
                return;
            }

            if (query.Length < 2)
            {
                results.AddView(Label("Skriv mindst to tegn", 14, Muted), MatchWidth());
                return;
            }
            matches = places.Search(query);
            if (matches.Count == 0)
            {
                var suggestions = places.SuggestSpelling(query);
                results.AddView(Label(suggestions.Count == 0 ? "Ingen steder fundet" : "Mente du?",
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

        input.TextChanged += (_, _) => Search();
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

    private void ChoosePlace(PrayerCity selection)
    {
        if (selection.Slug != place.Slug)
        {
            place = selection;
            schedule = new LocalPrayerSchedule(selection);
        }
        var recent = RecentPlaceIds().Where(id => id != selection.Slug).Prepend(selection.Slug).Take(5);
        using var change = preferences.Edit();
        change?.PutString(PrayerPreferences.PlaceKey, selection.Slug)?.PutString("recent-places", string.Join('|', recent))?.Apply();
        PrayerWidgetProvider.Refresh(this);
        HideKeyboard();
        Show(returnScreen);
    }

    private void RenderMethod()
    {
        AddHeading("Sådan beregnes bønnetiderne", "Dansk lokal tid");
        AddMethodSection("Beregningen", "Salah Times beregner solens position for det valgte sted. Fajr bruger 18 grader under horisonten, og Isha bruger 17 grader. Der lægges tre minutter til de beregnede tider.");
        AddMethodSection("Dhuhr", "Ved solmiddag står solen højest på himlen. Dhuhr-bønnen indtræder, så snart solen har passeret det punkt og begynder at synke mod vest.");
        AddMethodSection("Asr", "Asr er den islamiske eftermiddagsbøn. Med standardmetoden indtræder Asr, når skyggen af en lodret genstand er lige så lang som genstanden selv plus dens skyggelængde ved solmiddag. Med Hanafi-metoden lægger man to gange genstandens længde til skyggelængden ved solmiddag.");
        AddMethodSection("Fajr og Isha under lyse nætter", "En streg betyder, at solen ikke når den vinkel, der bruges til bønnen. Din lokale moské eller en lærd, du har tillid til, kan hjælpe dig med at vælge en metode for de lyse nætter. Isha kan også falde efter midnat. Det fremgår ved tiden.");
    }

    private void AddMethodSection(string title, string body)
    {
        AddSectionTitle(title, top: 8);
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
            Toast.MakeText(this, "Ingen browser fundet", ToastLength.Short)?.Show();
        }
    }
}
