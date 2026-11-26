using Application.Calendars;
using Domain.Models;
using UIKit;

namespace SalahTimes.iOS;

internal abstract class PrayerViewController(PrayerAppState state) : UIViewController
{
    protected PrayerAppState State { get; } = state;
    protected UIStackView Content { get; private set; } = null!;
    protected string T(string danish, string english) => State.Text(danish, english);

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        View!.BackgroundColor = UIColor.SystemGroupedBackground;
        var scroll = new UIScrollView { TranslatesAutoresizingMaskIntoConstraints = false,
            KeyboardDismissMode = UIScrollViewKeyboardDismissMode.Interactive };
        Content = new UIStackView { Axis = UILayoutConstraintAxis.Vertical, Spacing = 12,
            TranslatesAutoresizingMaskIntoConstraints = false };
        View.AddSubview(scroll);
        scroll.AddSubview(Content);
        var safeArea = View.SafeAreaLayoutGuide;
        NSLayoutConstraint.ActivateConstraints([
            scroll.TopAnchor.ConstraintEqualTo(safeArea.TopAnchor),
            scroll.BottomAnchor.ConstraintEqualTo(safeArea.BottomAnchor),
            scroll.LeadingAnchor.ConstraintEqualTo(safeArea.LeadingAnchor),
            scroll.TrailingAnchor.ConstraintEqualTo(safeArea.TrailingAnchor),
            Content.TopAnchor.ConstraintEqualTo(scroll.ContentLayoutGuide.TopAnchor, 16),
            Content.BottomAnchor.ConstraintEqualTo(scroll.ContentLayoutGuide.BottomAnchor, -24),
            Content.LeadingAnchor.ConstraintEqualTo(scroll.ContentLayoutGuide.LeadingAnchor, 16),
            Content.TrailingAnchor.ConstraintEqualTo(scroll.ContentLayoutGuide.TrailingAnchor, -16),
            Content.WidthAnchor.ConstraintEqualTo(scroll.FrameLayoutGuide.WidthAnchor, multiplier: 1, constant: -32)
        ]);
        Render();
    }

    internal void Refresh()
    {
        if (IsViewLoaded)
        {
            Clear(Content);
            Render();
        }
    }

    protected abstract void Render();

    protected void OpenPlaces() =>
        NavigationController?.PushViewController(new PlacesViewController(State), animated: true);

    protected UILabel Label(string text, UIFontTextStyle style = UIFontTextStyle.Body) =>
        new()
        {
            Text = text, Font = UIFont.GetPreferredFontForTextStyle(style), TextColor = UIColor.Label,
            Lines = 0, AdjustsFontForContentSizeCategory = true
        };

    protected static UIButton Button(string title, Action action)
    {
        var button = UIButton.FromType(UIButtonType.System);
        button.SetTitle(title, UIControlState.Normal);
        button.TitleLabel!.Font = UIFont.GetPreferredFontForTextStyle(UIFontTextStyle.Body);
        button.TitleLabel.AdjustsFontForContentSizeCategory = true;
        button.TitleLabel.Lines = 0;
        button.HeightAnchor.ConstraintGreaterThanOrEqualTo(44).Active = true;
        button.TouchUpInside += (_, _) => action();
        return button;
    }

    protected static UIStackView Card(params UIView[] views)
    {
        var card = new UIStackView(views)
        {
            Axis = UILayoutConstraintAxis.Vertical,
            Spacing = 10,
            LayoutMarginsRelativeArrangement = true,
            DirectionalLayoutMargins = new NSDirectionalEdgeInsets(14, 14, 14, 14),
            BackgroundColor = UIColor.SecondarySystemGroupedBackground
        };
        card.Layer.CornerRadius = 14;
        return card;
    }

    protected static void Clear(UIStackView stack)
    {
        foreach (var view in stack.ArrangedSubviews)
        {
            stack.RemoveArrangedSubview(view);
            view.RemoveFromSuperview();
        }
    }

    protected UIStackView Times(DailyPrayerTimes day, PrayerEvent? current = null)
    {
        var rows = PrayerDay.Events(day, State.Hanafi).ToList();
        rows.Insert(1, new PrayerEvent(T("Solopgang", "Sunrise"), PrayerDay.AvailableSunrise(day)));
        var list = Card();
        if (current is not null && !rows.Contains(current))
        {
            list.AddArrangedSubview(HighlightCurrent(Label(T($"Nu · {current.Name}", $"Now · {current.Name}"), UIFontTextStyle.Headline)));
        }
        foreach (var prayer in rows)
        {
            var value = prayer.Time ?? "—";
            if (prayer.StartsAt is { } start && start.Date > day.Date)
            {
                value += T(" · Næste døgn", " · Next day");
            }
            var isCurrent = prayer == current;
            var title = isCurrent ? Label(T($"{prayer.Name} · Nu", $"{prayer.Name} · Now"), UIFontTextStyle.Headline) : Label(prayer.Name);
            var time = Label(value, UIFontTextStyle.Callout);
            time.TextAlignment = UITextAlignment.Right;
            time.SetContentHuggingPriority((float)UILayoutPriority.DefaultHigh, UILayoutConstraintAxis.Horizontal);
            var row = new UIStackView([title, time]) { Axis = UILayoutConstraintAxis.Horizontal, Spacing = 8 };
            list.AddArrangedSubview(isCurrent ? HighlightCurrent(row) : row);
        }
        if (day.HasUnavailableTimes)
        {
            list.AddArrangedSubview(Label(T("En streg markerer en solbegivenhed, der ikke kan beregnes for stedet og dagen.",
                "A dash marks a solar event that cannot be calculated for the place and date."), UIFontTextStyle.Footnote));
        }
        return list;
    }

    private static UIStackView HighlightCurrent(UIView content)
    {
        var highlight = Card(content);
        highlight.BackgroundColor = UIColor.TertiarySystemFill;
        highlight.DirectionalLayoutMargins = new NSDirectionalEdgeInsets(6, 8, 6, 8);
        highlight.Layer.CornerRadius = 8;
        return highlight;
    }

    protected string DateText(DateOnly date) =>
        date.ToString(T("dddd d. MMMM yyyy", "dddd d MMMM yyyy"), State.Culture);
}
