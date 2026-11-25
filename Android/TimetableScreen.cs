using Android.Graphics;
using Android.Graphics.Drawables;
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
    private Button nextHelp = null!;
    private int todayColumns;
    private TextView todayStatus = null!;
    private LinearLayout todayTimes = null!;
    private PrayerEvent? nextPrayer;
    private DateTimeOffset refreshPrayerAt;

    private Button? openDateButton;
    private LinearLayout? openDatePanel;
    private LinearLayout? calendarDays;

    private string AsrMethod => hanafi ? "Hanafi" : "Standard";

    private void RenderToday()
    {
        if (!HasSelectedPlace)
        {
            RenderLocationRequired();
            return;
        }

        var now = clock.NowIn(place.TimeZoneId);
        var date = DateOnly.FromDateTime(now.Date);
        AddHeading(place.Name, Capitalize(date.ToString(T("dddd d. MMMM yyyy", "dddd d MMMM yyyy"), UiCulture)));
        if (preferences.GetBoolean(PrayerPreferences.FoundByLocationKey, false))
        {
            content.AddView(Label(T("Valgt via placering", "Selected using your location"), 12, Accent), MatchWidth(bottom: 8));
        }
        AddLocationPanel();

        var hero = Panel(AccentSurface, AccentOutline, 22);
        var heroBackground = new GradientDrawable(GradientDrawable.Orientation.TlBr,
            [Color.ParseColor("#244337").ToArgb(), Color.ParseColor("#25271B").ToArgb()]);
        heroBackground.SetCornerRadius(Dp(22));
        heroBackground.SetStroke(Dp(1), AccentOutline);
        hero.Background = heroBackground;
        hero.SetPadding(Dp(16), Dp(14), Dp(16), Dp(15));
        hero.AddView(Label(T("Næste bøn", "Next prayer"), 14, Accent, bold: true, heading: true), MatchWidth());
        var headingRow = new LinearLayout(this);
        headingRow.SetGravity(GravityFlags.CenterVertical);
        nextName = Label("", 29, Text, bold: true);
        headingRow.AddView(nextName, new LinearLayout.LayoutParams(0, -2, 1));
        nextTime = Label("", 36, Accent, bold: true);
        nextTime.FontFeatureSettings = "tnum";
        headingRow.AddView(nextTime, new LinearLayout.LayoutParams(-2, -2) { LeftMargin = Dp(8) });
        hero.AddView(headingRow, MatchWidth(top: 2));
        countdown = Label("", 14, Text);
        countdown.FontFeatureSettings = "tnum";
        hero.AddView(countdown, MatchWidth(top: 2));
        nextHelp = ActionButton(T("Læs om manglende tider", "About unavailable times"), OpenSolarEventHelp);
        hero.AddView(nextHelp, MatchWidth(top: 6));
        content.AddView(hero, MatchWidth(top: 2));
        AddSectionTitle(T("Dagens tider", "Today’s times"));
        todayStatus = Label("", 13, CurrentAccent, bold: true);
        todayStatus.Background = Rounded(CurrentSurface, 8, CurrentOutline);
        todayStatus.SetPadding(Dp(10), Dp(5), Dp(10), Dp(5));
        content.AddView(todayStatus, new LinearLayout.LayoutParams(-2, -2) { BottomMargin = Dp(6) });
        todayTimes = new LinearLayout(this) { Orientation = Orientation.Vertical };
        content.AddView(todayTimes, MatchWidth());
        todayColumns = 3;
        RefreshTodayTimes(now);
        RenderCountdown(now);
        AfterLayout(todayTimes, () =>
        {
            var minimum = TodayTileWidth();
            var fit = Math.Max(1, (todayTimes.Width + Dp(6)) / (minimum + Dp(6)));
            var columns = fit >= 6 ? 6 : fit >= 3 ? 3 : fit;
            if (screen == Screen.Today && columns != todayColumns)
            {
                todayColumns = columns;
                RefreshTodayTimes(clock.NowIn(place.TimeZoneId));
            }
        });
        content.AddView(ActionButton(T("Se månedskalenderen", "View monthly timetable"), OpenTodayInCalendar), MatchWidth(top: 12));
        content.AddView(Label($"{(place.CountryCode == "DK" ? T("Dansk lokal tid", "Local time in Denmark") : place.TimeZoneId)}   ·   Asr {AsrMethod}", 13, Muted), MatchWidth(top: 10));
    }

    private void RefreshTodayTimes(DateTimeOffset now)
    {
        var (current, next, refreshAt) = schedule.Status(now, hanafi);
        nextPrayer = next;
        refreshPrayerAt = refreshAt;
        nextName.Text = nextPrayer is null ? T("Næste bønnetid er ikke tilgængelig", "Next prayer time unavailable")
            : nextPrayer.StartsAt?.Date > now.Date ? T($"{nextPrayer.Name} i morgen", $"{nextPrayer.Name} tomorrow") : nextPrayer.Name;
        nextName.TextSize = nextPrayer is null ? 18 : 29;
        nextTime.Text = nextPrayer?.Time;
        nextTime.Visibility = nextPrayer is null ? ViewStates.Gone : ViewStates.Visible;
        nextHelp.Visibility = nextPrayer is null ? ViewStates.Visible : ViewStates.Gone;
        countdown.Visibility = nextPrayer is null ? ViewStates.Gone : ViewStates.Visible;

        var day = Day(DateOnly.FromDateTime(now.Date));
        var times = PrayerDay.Events(day, hanafi).ToList();
        times.Insert(1, new PrayerEvent("Solopgang", PrayerDay.AvailableSunrise(day)));
        todayStatus.Text = current is not null && !times.Contains(current) ? T($"Nu · {current.Name}", $"Now · {current.Name}") : "";
        todayStatus.Visibility = string.IsNullOrEmpty(todayStatus.Text) ? ViewStates.Gone : ViewStates.Visible;

        todayTimes.RemoveAllViews();
        foreach (var rowTimes in times.Chunk(todayColumns))
        {
            var row = new LinearLayout(this) { BaselineAligned = false };
            foreach (var prayer in rowTimes)
            {
                var isCurrent = prayer == current;
                var isNext = prayer == next;
                var tile = Panel(isCurrent ? CurrentSurface : isNext ? AccentSurface : Surface,
                    isCurrent ? CurrentOutline : isNext ? AccentOutline : Outline, 12);
                tile.SetMinimumHeight(Dp(79));
                tile.SetPadding(Dp(8), Dp(7), Dp(8), Dp(6));
                var caption = isCurrent ? T("Nu", "Now") : isNext ? T("Næste", "Next") : prayer.Time is null ? T("Ikke tilgængelig", "Time unavailable") :
                    prayer.StartsAt?.Date > day.Date ? T("Næste døgn", "Next day") : null;
                var tint = isCurrent ? CurrentAccent : isNext ? Accent : Muted;
                tile.AddView(Label(prayer.Name == "Solopgang" ? T("Solopgang", "Sunrise") : prayer.Name, 13, tint, bold: isCurrent || isNext), MatchWidth());
                var time = Label(prayer.Time ?? "—", 22, prayer.Name == "Solopgang" ? Muted : Text, bold: true);
                time.FontFeatureSettings = "tnum";
                tile.AddView(time, MatchWidth());
                if (caption is not null)
                {
                    tile.AddView(Label(caption, 10, tint, bold: isCurrent || isNext), MatchWidth());
                }
                row.AddView(tile, new LinearLayout.LayoutParams(0, -1, 1) { RightMargin = Dp(prayer == rowTimes[^1] ? 0 : 6) });
            }
            todayTimes.AddView(row, MatchWidth(bottom: 6));
        }
        if (day.HasUnavailableTimes)
        {
            todayTimes.AddView(ActionButton(T("Læs om manglende tider", "About unavailable times"), OpenSolarEventHelp), MatchWidth());
        }
    }

    private int TodayTileWidth()
    {
        using var name = Label("", 13, Text);
        using var time = Label("", 22, Text, bold: true);
        return (int)Math.Ceiling(Math.Max(name.Paint!.MeasureText(T("Solopgang", "Sunrise")),
            time.Paint!.MeasureText("00:00"))) + Dp(18);
    }

    private void RenderCountdown(DateTimeOffset now)
    {
        if (nextPrayer?.StartsAt is not { } startsAt)
        {
            return;
        }

        var seconds = Math.Max(0, (int)Math.Ceiling((startsAt - now).TotalSeconds));
        var hours = seconds / 3600;
        var minutes = seconds / 60 % 60;
        var duration = hours > 0 ? T($"{hours} t. {minutes} min. {seconds % 60} sek.", $"{hours} hr {minutes} min {seconds % 60} sec") :
            minutes > 0 ? T($"{minutes} min. {seconds % 60} sek.", $"{minutes} min {seconds % 60} sec") : T($"{seconds} sek.", $"{seconds} sec");
        countdown.Text = T($"Om {duration}", $"In {duration}");
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
                AddPrayerRow(panel, T("Solopgang", "Sunrise"), string.IsNullOrEmpty(day.SunriseTime) ? "—" : day.SunriseTime, null, secondary: true);
                panel.AddView(Divider());
            }
            var note = prayer.StartsAt is null ? T("Ikke tilgængelig", "Time unavailable") :
                prayer.StartsAt.Value.Date > day.Date ? T("Næste døgn", "Next day") : null;
            AddPrayerRow(panel, prayer.Name, prayer.Time ?? "—", note);
        }
        if (day.HasUnavailableTimes)
        {
            panel.AddView(Divider());
            var explanation = ActionButton(T("Læs om manglende tider", "About unavailable times"), OpenSolarEventHelp);
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

    private LinearLayout BuildCalendarHeader()
    {
        var header = new LinearLayout(this) { Orientation = Orientation.Vertical };
        header.SetPadding(Dp(20), Dp(8), Dp(20), 0);
        var row = new LinearLayout(this);
        row.SetGravity(GravityFlags.CenterVertical);
        var title = Capitalize(month.ToString("MMMM yyyy", UiCulture));
        var date = ActionButton(title + " ⌄", OpenCalendarDatePicker,
            description: T($"Vælg dato, {title}", $"Choose date, {title}"));
        date.Gravity = GravityFlags.CenterVertical | GravityFlags.Left;
        row.AddView(date, new LinearLayout.LayoutParams(0, -2, 1));
        row.AddView(ActionButton(T("Skift sted", "Change location"), OpenPlaces), new LinearLayout.LayoutParams(-2, -2) { LeftMargin = Dp(8) });
        header.AddView(row, MatchWidth());
        header.AddView(Label($"{place.Name}   ·   Asr {AsrMethod}", 14, Muted), MatchWidth(top: 5, bottom: 7));

        var navigation = new LinearLayout(this);
        var previous = ActionButton(T("‹ Forrige", "‹ Previous"), () => ChangeMonth(-1), description: T("Forrige måned", "Previous month"));
        previous.Enabled = month > FirstCalendarMonth;
        navigation.AddView(previous, new LinearLayout.LayoutParams(0, -2, 1));
        navigation.AddView(ActionButton(T("I dag", "Today"), OpenTodayInCalendar),
            new LinearLayout.LayoutParams(0, -2, 1) { LeftMargin = Dp(6), RightMargin = Dp(6) });
        var next = ActionButton(T("Næste ›", "Next ›"), () => ChangeMonth(1), description: T("Næste måned", "Next month"));
        next.Enabled = month < LastCalendarMonth;
        navigation.AddView(next, new LinearLayout.LayoutParams(0, -2, 1));
        header.AddView(navigation, MatchWidth(bottom: 7));
        return header;
    }

    private void RenderCalendar(bool jumpToSelected)
    {
        if (!HasSelectedPlace)
        {
            RenderLocationRequired();
            return;
        }

        AddLocationPanel();
        openDateButton = null;
        openDatePanel = null;
        var list = calendarDays = Panel(Surface, Outline);
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

        if (jumpToSelected)
        {
            ScrollToCalendarDate();
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
            ScrollToCalendarDate();
        }
    }

    private void ScrollToCalendarDate()
    {
        if (openDateButton?.Parent is View selected && calendarDays is { } list)
        {
            var calendarScroll = scroll;
            var padding = content.PaddingTop;
            AfterLayout(calendarScroll, () =>
            {
                if (openDateButton?.Parent == selected)
                {
                    calendarScroll.ScrollTo(0, list.Top + selected.Top - padding);
                }
            });
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
        var today = date == displayedToday ? T(" · I dag", " · Today") : "";
        return $"{Capitalize(date.ToString(T("dddd d. MMMM", "dddd d MMMM"), UiCulture))}{today}  {(expanded ? "⌃" : "⌄")}";
    }

    private void ChangeMonth(int direction)
    {
        month = month.AddMonths(direction);
        var today = DateOnly.FromDateTime(clock.NowIn(place.TimeZoneId).Date);
        selectedDate = month == MonthOf(today) ? today : null;
        Show(Screen.Calendar, jumpToSelected: selectedDate.HasValue);
    }

    private void OpenTodayInCalendar() => OpenCalendarDate(DateOnly.FromDateTime(clock.NowIn(place.TimeZoneId).Date));

    private void OpenCalendarDate(DateOnly date)
    {
        month = MonthOf(date);
        selectedDate = date;
        Show(Screen.Calendar, jumpToSelected: true);
    }
}
