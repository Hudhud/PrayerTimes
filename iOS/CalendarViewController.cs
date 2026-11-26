using UIKit;

namespace SalahTimes.iOS;

internal sealed class CalendarViewController(PrayerAppState state) : PrayerViewController(state)
{
    private DateOnly? selected;

    protected override void Render()
    {
        var today = State.Today;
        selected ??= today;
        var date = selected.Value;
        var month = new DateOnly(date.Year, date.Month, 1);
        Content.AddArrangedSubview(Label(State.Place.Name, UIFontTextStyle.Title2));
        Content.AddArrangedSubview(Button(T("Skift sted", "Change location"), OpenPlaces));

        var nav = new UIStackView([
            Button(T("‹ Forrige", "‹ Previous"), () => ChangeMonth(month, -1)),
            Button(T("I dag", "Today"), () => Select(today)),
            Button(T("Næste ›", "Next ›"), () => ChangeMonth(month, 1))
        ]) { Axis = UILayoutConstraintAxis.Horizontal, Distribution = UIStackViewDistribution.FillEqually, Spacing = 6 };
        Content.AddArrangedSubview(nav);
        Content.AddArrangedSubview(Label(month.ToString("MMMM yyyy", State.Culture), UIFontTextStyle.Headline));

        var days = Card();
        var dayButtons = new UIButton[DateTime.DaysInMonth(date.Year, date.Month)];
        var detail = new UIStackView { Axis = UILayoutConstraintAxis.Vertical, Spacing = 10 };

        void ShowDay(DateOnly value)
        {
            selected = value;
            foreach (var (button, index) in dayButtons.Select((button, index) => (button, index)))
            {
                button.Selected = index + 1 == value.Day;
            }
            Clear(detail);
            detail.AddArrangedSubview(Label(DateText(value), UIFontTextStyle.Headline));
            detail.AddArrangedSubview(Times(State.Schedule.Day(value)));
        }

        // Seven compact dates per row leave room for the selected day's timetable below.
        for (var row = 0; row < (dayButtons.Length + 6) / 7; row++)
        {
            var week = new UIStackView { Axis = UILayoutConstraintAxis.Horizontal,
                Distribution = UIStackViewDistribution.FillEqually };
            for (var column = 0; column < 7; column++)
            {
                var day = row * 7 + column;
                if (day >= dayButtons.Length)
                {
                    week.AddArrangedSubview(new UIView());
                    continue;
                }
                var value = month.AddDays(day);
                var button = Button(value.Day.ToString(State.Culture), () => ShowDay(value));
                button.AccessibilityLabel = DateText(value);
                button.SetTitleColor(UIColor.Label, UIControlState.Selected);
                dayButtons[day] = button;
                week.AddArrangedSubview(button);
            }
            days.AddArrangedSubview(week);
        }
        Content.AddArrangedSubview(days);
        Content.AddArrangedSubview(detail);
        ShowDay(date);
    }

    private void Select(DateOnly date)
    {
        selected = date;
        Refresh();
    }

    private void ChangeMonth(DateOnly month, int direction)
    {
        if ((direction < 0 && month.Year == 1 && month.Month == 1)
            || (direction > 0 && month.Year == 9999 && month.Month == 12))
        {
            return;
        }
        Select(month.AddMonths(direction));
    }
}
