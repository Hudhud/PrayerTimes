using Application.Calendars;
using Foundation;
using UIKit;

namespace SalahTimes.iOS;

internal sealed class TodayViewController(PrayerAppState state) : PrayerViewController(state)
{
    private UILabel nextLabel = null!;
    private UILabel countdown = null!;
    private DateTimeOffset refreshAt;
    private PrayerEvent? next;
    private NSTimer? timer;

    public override void ViewWillAppear(bool animated)
    {
        base.ViewWillAppear(animated);
        Refresh();
        timer ??= NSTimer.CreateRepeatingScheduledTimer(TimeSpan.FromSeconds(1), _ => Tick());
    }

    public override void ViewDidDisappear(bool animated)
    {
        timer?.Invalidate();
        timer?.Dispose();
        timer = null;
        base.ViewDidDisappear(animated);
    }

    protected override void Render()
    {
        var now = State.Now;
        var date = DateOnly.FromDateTime(now.Date);
        Content.AddArrangedSubview(Label(State.Place.Name, UIFontTextStyle.Title1));
        Content.AddArrangedSubview(Label($"{DateText(date)} · {State.Place.TimeZoneId}", UIFontTextStyle.Subheadline));
        Content.AddArrangedSubview(Button(T("Skift sted", "Change location"), OpenPlaces));

        nextLabel = Label("", UIFontTextStyle.Title2);
        countdown = Label("", UIFontTextStyle.Callout);
        var (current, upcoming, refresh) = State.Schedule.Status(now, State.Hanafi);
        next = upcoming;
        refreshAt = refresh;
        Content.AddArrangedSubview(Card(Label(T("Næste bøn", "Next prayer"), UIFontTextStyle.Headline), nextLabel, countdown));
        Content.AddArrangedSubview(Times(State.Schedule.Day(date), current));
        Content.AddArrangedSubview(Label(State.Hanafi ? "Asr · Hanafi" : "Asr · Standard", UIFontTextStyle.Footnote));
        nextLabel.Text = next?.StartsAt is { } start
            ? $"{next.Name} · {next.Time}{(start.Date > now.Date ? T(" · I morgen", " · Tomorrow") : "")}"
            : T("Tidspunktet er ikke tilgængeligt", "Time unavailable");
        UpdateCountdown(now);
    }

    private void Tick()
    {
        var now = State.Now;
        if (now >= refreshAt)
        {
            Refresh();
        }
        else
        {
            UpdateCountdown(now);
        }
    }

    private void UpdateCountdown(DateTimeOffset now)
    {
        if (next?.StartsAt is not { } start)
        {
            countdown.Hidden = true;
            return;
        }
        var left = TimeSpan.FromSeconds(Math.Max(0, Math.Ceiling((start - now).TotalSeconds)));
        var value = left.ToString(@"hh\:mm\:ss", State.Culture);
        countdown.Text = T($"Om {value}", $"In {value}");
        countdown.Hidden = false;
    }
}
