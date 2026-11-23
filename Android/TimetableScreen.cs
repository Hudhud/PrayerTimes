using Android.Graphics;
using Android.Views;
using Android.Widget;
using Application.Calendars;
using Domain.Models;

namespace SalahTimes.AndroidApp;

public sealed partial class MainActivity
{
    private TextView nextName = null!;
    private TextView nextTime = null!;
    private TextView countdown = null!;
    private PrayerEvent? nextPrayer;
    private DateTimeOffset refreshPrayerAt;

    private Button? openDateButton;
    private LinearLayout? openDatePanel;

    private string AsrMethod => hanafi ? "Hanafi" : "Standard";

    private void RenderToday()
    {
        if (!HasSelectedPlace)
        {
            RenderLocationRequired();
            return;
        }

        var now = clock.Now;
        var date = DateOnly.FromDateTime(now.Date);
        AddHeading(place.Name, Capitalize(date.ToString("dddd d. MMMM yyyy", Danish)));
        if (preferences.GetBoolean(PrayerPreferences.FoundByLocationKey, false))
        {
            content.AddView(Label("Valgt via placering", 12, Accent), MatchWidth(bottom: 8));
        }
        AddLocationPanel();

        var hero = Panel(AccentSurface, AccentOutline, 22);
        hero.SetPadding(Dp(16), Dp(13), Dp(16), Dp(14));
        hero.AddView(Label("Næste bøn", 15, Accent, bold: true, heading: true), MatchWidth());
        var headingRow = new LinearLayout(this);
        headingRow.SetGravity(GravityFlags.CenterVertical);
        nextName = Label("", 25, Text, bold: true);
        headingRow.AddView(nextName, new LinearLayout.LayoutParams(0, -2, 1));
        nextTime = Label("", 32, Accent, bold: true);
        nextTime.FontFeatureSettings = "tnum";
        headingRow.AddView(nextTime, new LinearLayout.LayoutParams(-2, -2) { LeftMargin = Dp(8) });
        hero.AddView(headingRow, MatchWidth(top: 2));
        countdown = Label("", 14, Muted);
        countdown.FontFeatureSettings = "tnum";
        hero.AddView(countdown, MatchWidth(top: 2));
        content.AddView(hero, MatchWidth(top: 2));
        RefreshNextPrayer(now);
        RenderCountdown(now);

        AddSectionTitle("Dagens tider");
        content.AddView(BuildPrayerTimes(Day(date)), MatchWidth());
        content.AddView(Label($"Dansk lokal tid   ·   Asr {AsrMethod}", 13, Muted), MatchWidth(top: 10));
    }

    private void RefreshNextPrayer(DateTimeOffset now)
    {
        (nextPrayer, refreshPrayerAt) = schedule.Next(now, hanafi);
        nextName.Text = nextPrayer?.Name ?? "Tidspunktet afhænger af lyse nætter";
        nextName.TextSize = nextPrayer is null ? 18 : 25;
        nextTime.Text = nextPrayer?.Time;
        nextTime.Visibility = nextPrayer is null ? ViewStates.Gone : ViewStates.Visible;
    }

    private void RenderCountdown(DateTimeOffset now)
    {
        if (nextPrayer?.StartsAt is not { } startsAt)
        {
            countdown.Text = "Se vejledningen under Indstillinger";
            return;
        }

        var seconds = Math.Max(0, (int)Math.Ceiling((startsAt - now).TotalSeconds));
        var hours = seconds / 3600;
        var minutes = seconds / 60 % 60;
        var duration = hours > 0 ? $"{hours} t. {minutes} min. {seconds % 60} sek." :
            minutes > 0 ? $"{minutes} min. {seconds % 60} sek." : $"{seconds} sek.";
        var tomorrow = startsAt.Date > now.Date ? "I morgen · " : "";
        countdown.Text = $"{tomorrow}Om {duration}";
    }

    private LinearLayout BuildPrayerTimes(DailyPrayerTimes day)
    {
        var panel = Panel(Surface, Outline);
        var prayers = PrayerDay.Events(day, hanafi);
        for (var i = 0; i < prayers.Count; i++)
        {
            var prayer = prayers[i];
            if (i > 0)
            {
                panel.AddView(Divider());
            }
            if (i == 1)
            {
                AddPrayerRow(panel, "Solopgang", day.SunriseTime, null, secondary: true);
                panel.AddView(Divider());
            }
            var note = prayer.StartsAt is null ? "Lyse nætter" :
                prayer.StartsAt.Value.Date > day.Date ? "Næste døgn" : null;
            AddPrayerRow(panel, prayer.Name, prayer.Time ?? "—", note);
        }
        if (day.FajrTimeUnavailable || day.IshaTimeUnavailable)
        {
            panel.AddView(Divider());
            var explanation = ActionButton("Læs om lyse nætter", OpenMethod);
            var spacing = MatchWidth(top: 8, bottom: 8);
            spacing.LeftMargin = Dp(12);
            spacing.RightMargin = Dp(12);
            panel.AddView(explanation, spacing);
        }
        return panel;
    }

    private void AddPrayerRow(LinearLayout panel, string name, string time, string? note, bool secondary = false)
    {
        var row = new LinearLayout(this);
        row.SetGravity(GravityFlags.CenterVertical);
        row.SetMinimumHeight(Dp(45));
        row.SetPadding(Dp(14), Dp(5), Dp(14), Dp(5));
        row.AddView(Label(name, 15, secondary ? Muted : Text, bold: !secondary), new LinearLayout.LayoutParams(0, -2, 1));
        if (note is not null)
        {
            row.AddView(Label(note, 12, Muted), new LinearLayout.LayoutParams(-2, -2) { RightMargin = Dp(10) });
        }
        var value = Label(time, 18, secondary ? Muted : Text, bold: !secondary);
        value.FontFeatureSettings = "tnum";
        row.AddView(value, new LinearLayout.LayoutParams(-2, -2));
        panel.AddView(row, MatchWidth());
    }

    private void RenderCalendar(bool jumpToSelected)
    {
        if (!HasSelectedPlace)
        {
            RenderLocationRequired();
            return;
        }

        AddHeading(Capitalize(month.ToString("MMMM yyyy", Danish)), $"{place.Name}   ·   Asr {AsrMethod}");
        AddLocationPanel();
        var navigation = new LinearLayout(this);
        navigation.AddView(ActionButton("‹ Forrige", () => ChangeMonth(-1), description: "Forrige måned"), new LinearLayout.LayoutParams(0, -2, 1));
        navigation.AddView(ActionButton("I dag", OpenTodayInCalendar), new LinearLayout.LayoutParams(0, -2, 1) { LeftMargin = Dp(6), RightMargin = Dp(6) });
        navigation.AddView(ActionButton("Næste ›", () => ChangeMonth(1), description: "Næste måned"), new LinearLayout.LayoutParams(0, -2, 1));
        content.AddView(navigation, MatchWidth(bottom: 12));

        openDateButton = null;
        openDatePanel = null;
        var list = Panel(Surface, Outline);
        for (var date = month; date < month.AddMonths(1); date = date.AddDays(1))
        {
            var day = date;
            var group = new LinearLayout(this) { Orientation = Orientation.Vertical };
            var button = ActionButton(DateLabel(day, expanded: false));
            button.Gravity = GravityFlags.CenterVertical | GravityFlags.Left;
            button.SetPadding(Dp(13), Dp(4), Dp(13), Dp(4));
            button.Background = Pressable(Surface, 12);
            button.SetTextColor(day == displayedToday ? Accent : Text);
            group.AddView(button, MatchWidth());
            var schedule = new LinearLayout(this) { Orientation = Orientation.Vertical };
            schedule.SetPadding(Dp(8), 0, Dp(8), Dp(7));
            schedule.Visibility = ViewStates.Gone;
            group.AddView(schedule, MatchWidth());
            button.Click += (_, _) => SelectDate(day, button, schedule);

            if (date == selectedDate)
            {
                OpenDate(day, button, schedule);
            }
            if (date != month)
            {
                list.AddView(Divider());
            }
            list.AddView(group, MatchWidth());
        }
        content.AddView(list, MatchWidth());

        if (jumpToSelected && openDateButton?.Parent is View selected)
        {
            var calendarScroll = scroll;
            var screenContent = content;
            calendarScroll.Post(() => calendarScroll.ScrollTo(0, list.Top + selected.Top - screenContent.PaddingTop));
        }
    }

    private void SelectDate(DateOnly date, Button button, LinearLayout panel)
    {
        var previousDate = selectedDate;
        if (previousDate is { } previous && openDateButton is not null && openDatePanel is not null)
        {
            openDateButton.Text = DateLabel(previous, expanded: false);
            openDateButton.Selected = false;
            openDatePanel.RemoveAllViews();
            openDatePanel.Visibility = ViewStates.Gone;
        }

        selectedDate = null;
        openDateButton = null;
        openDatePanel = null;
        if (previousDate != date)
        {
            OpenDate(date, button, panel);
        }
    }

    private void OpenDate(DateOnly date, Button button, LinearLayout panel)
    {
        selectedDate = date;
        openDateButton = button;
        openDatePanel = panel;
        button.Text = DateLabel(date, expanded: true);
        button.Selected = true;
        panel.AddView(BuildPrayerTimes(Day(date)), MatchWidth());
        panel.Visibility = ViewStates.Visible;
    }

    private string DateLabel(DateOnly date, bool expanded)
    {
        var today = date == displayedToday ? " · I dag" : "";
        return $"{Capitalize(date.ToString("dddd d. MMMM", Danish))}{today}  {(expanded ? "⌃" : "⌄")}";
    }

    private void ChangeMonth(int direction)
    {
        month = month.AddMonths(direction);
        var today = DateOnly.FromDateTime(clock.Now.Date);
        selectedDate = month == MonthOf(today) ? today : null;
        Show(Screen.Calendar);
    }

    private void OpenTodayInCalendar()
    {
        var today = DateOnly.FromDateTime(clock.Now.Date);
        month = MonthOf(today);
        selectedDate = today;
        Show(Screen.Calendar, jumpToSelected: true);
    }
}
