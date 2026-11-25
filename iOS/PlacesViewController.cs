using Application.Calendars;
using UIKit;

namespace SalahTimes.iOS;

internal sealed class PlacesViewController(PrayerAppState state) : PrayerViewController(state)
{
    private UITextField search = null!;
    private UIStackView results = null!;

    protected override void Render()
    {
        Title = T("Skift sted", "Change location");
        var hint = T("By eller dansk postnummer", "City or Danish postcode");
        search = new UITextField { Placeholder = hint, AccessibilityLabel = hint, BorderStyle = UITextBorderStyle.RoundedRect,
            ClearButtonMode = UITextFieldViewMode.WhileEditing, ReturnKeyType = UIReturnKeyType.Search,
            Font = UIFont.GetPreferredFontForTextStyle(UIFontTextStyle.Body),
            AdjustsFontForContentSizeCategory = true };
        search.HeightAnchor.ConstraintGreaterThanOrEqualTo(44).Active = true;
        search.EditingChanged += (_, _) => UpdateResults();
        search.ShouldReturn = input => input.ResignFirstResponder();
        results = new UIStackView { Axis = UILayoutConstraintAxis.Vertical, Spacing = 4 };
        Content.AddArrangedSubview(search);
        Content.AddArrangedSubview(results);
        UpdateResults();
    }

    private void UpdateResults()
    {
        Clear(results);
        var query = search.Text?.Trim() ?? "";
        if (query.Length == 0)
        {
            AddPlace(State.Place, null);
            foreach (var city in PrayerCities.All.Where(city => city.Slug != State.Place.Slug))
            {
                AddPlace(city, null);
            }
            return;
        }
        if (query.Length < 2)
        {
            results.AddArrangedSubview(Label(T("Skriv mindst to tegn.", "Enter at least two characters.")));
            return;
        }

        var matches = State.Places.Search(query);
        if (matches.Count == 0)
        {
            matches = State.Places.SuggestSpelling(query);
            results.AddArrangedSubview(Label(matches.Count == 0
                ? T("Ingen steder fundet.", "No places found.")
                : T("Mente du?", "Did you mean?"), UIFontTextStyle.Headline));
        }
        foreach (var match in matches)
        {
            AddPlace(State.Places.Find(match.Id) ?? match.ToPrayerCity(), match.Area);
        }
    }

    private void AddPlace(PrayerCity city, string? area)
    {
        var text = string.IsNullOrWhiteSpace(area) || area == city.Name ? city.Name : $"{city.Name} · {area}";
        var button = Button(text, () =>
        {
            search.ResignFirstResponder();
            State.SelectPlace(city);
            NavigationController?.PopViewController(animated: true);
        });
        button.HorizontalAlignment = UIControlContentHorizontalAlignment.Leading;
        results.AddArrangedSubview(button);
    }
}
