using System.Globalization;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using Application.Calendars;
using Domain.Models;
using Color = Android.Graphics.Color;
using ColorStateList = Android.Content.Res.ColorStateList;

namespace SalahTimes.AndroidApp;

[Activity(Exported = true, MainLauncher = true)]
public sealed partial class MainActivity : Activity
{
    private enum Screen { Today, Calendar, Settings, Places, Method }

    private static readonly CultureInfo Danish = CultureInfo.GetCultureInfo("da-DK");
    private static readonly Color Background = Color.ParseColor("#14181C");
    private static readonly Color Surface = Color.ParseColor("#1D2328");
    private static readonly Color Outline = Color.ParseColor("#364049");
    private static readonly Color Text = Color.ParseColor("#F6F0E7");
    private static readonly Color Muted = Color.ParseColor("#BEC5C9");
    private static readonly Color Accent = Color.ParseColor("#F5CE78");
    private static readonly Color AccentSurface = Color.ParseColor("#2B2417");
    private static readonly Color AccentOutline = Color.ParseColor("#685536");

    private readonly DanishTime clock = new(TimeProvider.System);
    private LocalPrayerSchedule schedule = null!;
    private readonly Handler handler = new(Looper.MainLooper!);
    private Java.Lang.Runnable tick = null!;
    private ISharedPreferences preferences = null!;
    private PrayerPlaceDirectory places = null!;
    private PrayerCity place = PrayerCities.Default;
    private bool hanafi;
    private bool foreground;
    private Screen screen;
    private Screen returnScreen;
    private DateOnly displayedToday;
    private DateOnly month;
    private DateOnly? selectedDate;

    private LinearLayout root = null!;
    private LinearLayout content = null!;
    private ScrollView scroll = null!;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        places = PrayerPreferences.Places(this);
        preferences = PrayerPreferences.Open(this);
        place = PrayerPreferences.SelectedPlace(this, preferences);
        hanafi = preferences.GetBoolean(PrayerPreferences.HanafiKey, false);
        schedule = new LocalPrayerSchedule(place);
        var today = DateOnly.FromDateTime(clock.Now.Date);
        month = MonthOf(today);
        selectedDate = today;
        if (savedInstanceState is not null)
        {
            screen = (Screen)savedInstanceState.GetInt("screen");
            locationPermissionPending = savedInstanceState.GetBoolean("location-permission-pending");
            if (DateOnly.TryParseExact(savedInstanceState.GetString("month"), "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var savedMonth))
            {
                month = savedMonth;
                selectedDate = DateOnly.TryParseExact(savedInstanceState.GetString("day"), "yyyy-MM-dd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var savedDay) ? savedDay : null;
            }
        }

        tick = new Java.Lang.Runnable(UpdateClock);
        root = new SafeArea(this) { Orientation = Orientation.Vertical };
        root.SetBackgroundColor(Background);
        if (OperatingSystem.IsAndroidVersionAtLeast(30) && !OperatingSystem.IsAndroidVersionAtLeast(35))
        {
            Window?.SetDecorFitsSystemWindows(false);
        }
        Window?.SetSoftInputMode(SoftInput.AdjustResize);
        SetContentView(root);
        Show(screen);
    }

    protected override void OnStart()
    {
        base.OnStart();
        foreground = true;
        UpdateClock();
    }

    protected override void OnResume()
    {
        base.OnResume();
        RefreshPrayerNotificationSettings();
        TryAutomaticLocation();
    }

    protected override void OnStop()
    {
        foreground = false;
        handler.RemoveCallbacks(tick);
        PauseLocationLookup();
        base.OnStop();
    }

    protected override void OnSaveInstanceState(Bundle outState)
    {
        var primary = screen is Screen.Places or Screen.Method ? returnScreen : screen;
        outState.PutInt("screen", (int)primary);
        outState.PutBoolean("location-permission-pending", locationPermissionPending);
        outState.PutString("month", month.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        outState.PutString("day", selectedDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        base.OnSaveInstanceState(outState);
    }

    public override void OnBackPressed()
    {
        if (screen is Screen.Places or Screen.Method)
        {
            if (screen == Screen.Places)
            {
                CancelLocationLookup();
            }
            HideKeyboard();
            Show(returnScreen);
        }
        else if (screen != Screen.Today)
        {
            Show(Screen.Today);
        }
        else
        {
            Finish();
        }
    }

    private void Show(Screen target, bool jumpToSelected = false)
    {
        screen = target;
        locationPanel = null;
        root.RemoveAllViews();
        root.AddView(BuildHeader(), MatchWidth());
        scroll = new ScrollView(this) { FillViewport = true };
        content = new LinearLayout(this) { Orientation = Orientation.Vertical };
        content.SetPadding(Dp(20), Dp(8), Dp(20), Dp(24));
        scroll.AddView(content, new ScrollView.LayoutParams(-1, -2));
        root.AddView(scroll, new LinearLayout.LayoutParams(-1, 0, 1));
        displayedToday = DateOnly.FromDateTime(clock.Now.Date);

        switch (screen)
        {
            case Screen.Today: RenderToday(); break;
            case Screen.Calendar: RenderCalendar(jumpToSelected); break;
            case Screen.Settings: RenderSettings(); break;
            case Screen.Places: RenderPlaces(); break;
            case Screen.Method: RenderMethod(); break;
        }

        if (screen is Screen.Today or Screen.Calendar or Screen.Settings)
        {
            root.AddView(BuildNavigation(), MatchWidth());
        }
    }

    private View BuildHeader()
    {
        var row = new LinearLayout(this);
        row.SetGravity(GravityFlags.CenterVertical);
        row.SetPadding(Dp(20), Dp(8), Dp(20), Dp(8));
        if (screen is Screen.Places or Screen.Method)
        {
            row.AddView(ActionButton("‹ Tilbage", () => OnBackPressed(), description: "Tilbage"), new LinearLayout.LayoutParams(-2, -2));
            return row;
        }

        var icon = new ImageView(this) { ImportantForAccessibility = ImportantForAccessibility.No };
        icon.SetImageResource(Resource.Drawable.app_icon);
        row.AddView(icon, new LinearLayout.LayoutParams(Dp(32), Dp(32)) { RightMargin = Dp(9) });
        var brand = Label("Salah Times", 16, Text, bold: true);
        row.AddView(brand, new LinearLayout.LayoutParams(0, -2, 1));
        if (screen != Screen.Settings)
        {
            row.AddView(ActionButton("Skift sted", OpenPlaces), new LinearLayout.LayoutParams(-2, -2) { LeftMargin = Dp(8) });
        }
        return row;
    }

    private View BuildNavigation()
    {
        var band = new LinearLayout(this) { Orientation = Orientation.Vertical };
        band.SetBackgroundColor(Surface);
        band.AddView(Divider());
        var row = new LinearLayout(this);
        row.SetPadding(Dp(12), Dp(4), Dp(12), Dp(4));
        row.AddView(NavigationTab(Screen.Today, "I dag", Resource.Drawable.nav_today), new LinearLayout.LayoutParams(0, -2, 1));
        row.AddView(NavigationTab(Screen.Calendar, "Kalender", Resource.Drawable.nav_calendar), new LinearLayout.LayoutParams(0, -2, 1));
        row.AddView(NavigationTab(Screen.Settings, "Indstillinger", Resource.Drawable.nav_settings), new LinearLayout.LayoutParams(0, -2, 1));
        band.AddView(row, MatchWidth());
        return band;
    }

    private LinearLayout NavigationTab(Screen target, string text, int iconResource)
    {
        var selected = screen == target;
        var tab = new LinearLayout(this)
        {
            Orientation = Orientation.Vertical,
            Focusable = true, Selected = selected, ContentDescription = text
        };
        tab.SetGravity(GravityFlags.Center);
        tab.SetPadding(Dp(3), Dp(5), Dp(3), Dp(4));
        tab.SetMinimumHeight(Dp(56));
        tab.Background = Pressable(selected ? AccentSurface : Surface, 15);
        var icon = new ImageView(this) { ImportantForAccessibility = ImportantForAccessibility.No };
        icon.SetImageResource(iconResource);
        icon.SetColorFilter(selected ? Accent : Muted);
        tab.AddView(icon, new LinearLayout.LayoutParams(Dp(20), Dp(20)));
        var label = Label(text, 11, selected ? Accent : Muted, bold: selected);
        label.ImportantForAccessibility = ImportantForAccessibility.No;
        label.Gravity = GravityFlags.Center;
        tab.AddView(label, MatchWidth(top: 3));
        tab.Click += (_, _) => { if (screen != target) Show(target); };
        return tab;
    }

    private void UpdateClock()
    {
        if (!foreground)
        {
            return;
        }

        var now = clock.Now;
        var today = DateOnly.FromDateTime(now.Date);
        if (today != displayedToday && screen is Screen.Today or Screen.Calendar)
        {
            if (month == MonthOf(displayedToday) && selectedDate == displayedToday)
            {
                month = MonthOf(today);
                selectedDate = today;
            }
            var position = scroll.ScrollY;
            Show(screen);
            var currentScroll = scroll;
            currentScroll.Post(() => currentScroll.ScrollTo(0, position));
        }
        else if (screen == Screen.Today && HasSelectedPlace)
        {
            if (now >= refreshPrayerAt)
            {
                RefreshNextPrayer(now);
            }
            RenderCountdown(now);
        }

        handler.PostDelayed(tick, 1000 - now.Millisecond);
    }

    private DailyPrayerTimes Day(DateOnly date) => schedule.Day(date);

    private void OpenPlaces()
    {
        CancelLocationLookup();
        returnScreen = screen;
        Show(Screen.Places);
    }

    private void OpenMethod()
    {
        returnScreen = screen;
        Show(Screen.Method);
    }

    private void HideKeyboard()
    {
        if (GetSystemService(InputMethodService) is InputMethodManager service && CurrentFocus?.WindowToken is { } token)
        {
            service.HideSoftInputFromWindow(token, HideSoftInputFlags.None);
        }
    }

    private int Dp(float value) => (int)Math.Round(value * Resources!.DisplayMetrics!.Density);

    private LinearLayout.LayoutParams MatchWidth(int top = 0, int bottom = 0) =>
        new(-1, -2) { TopMargin = Dp(top), BottomMargin = Dp(bottom) };

    private TextView Label(string text, float size, Color color, bool bold = false, bool heading = false)
    {
        var view = new TextView(this) { Text = text, TextSize = size };
        if (heading && OperatingSystem.IsAndroidVersionAtLeast(28))
        {
            view.AccessibilityHeading = true;
        }
        view.SetTextColor(color);
        if (bold)
        {
            view.SetTypeface(Typeface.Default, TypefaceStyle.Bold);
        }
        return view;
    }

    private void AddHeading(string text, string? description = null)
    {
        content.AddView(Label(text, 31, Text, bold: true, heading: true), MatchWidth(bottom: 4));
        if (description is not null)
        {
            content.AddView(Label(description, 14, Muted), MatchWidth(bottom: 14));
        }
    }

    private void AddSectionTitle(string text, int top = 18) =>
        content.AddView(Label(text, 17, Text, bold: true, heading: true), MatchWidth(top: top, bottom: 8));

    private GradientDrawable Rounded(Color color, int radius, Color? stroke = null)
    {
        var background = new GradientDrawable();
        background.SetColor(color);
        background.SetCornerRadius(Dp(radius));
        if (stroke is { } border)
        {
            background.SetStroke(Dp(1), border);
        }
        return background;
    }

    private RippleDrawable Pressable(Color color, int radius, Color? stroke = null) =>
        new(ColorStateList.ValueOf(Color.Argb(35, 245, 206, 120)), Rounded(color, radius, stroke), null);

    private LinearLayout Panel(Color color, Color stroke, int radius = 18)
    {
        var panel = new LinearLayout(this) { Orientation = Orientation.Vertical };
        panel.Background = Rounded(color, radius, stroke);
        return panel;
    }

    private Button ActionButton(string title, Action? action = null, bool selected = false, string? description = null)
    {
        var button = new Button(this)
        {
            Text = title, TextSize = 14, Gravity = GravityFlags.Center,
            BackgroundTintList = null, Selected = selected
        };
        button.SetAllCaps(false);
        button.SetMinimumWidth(0);
        button.SetMinimumHeight(Dp(48));
        button.SetPadding(Dp(12), Dp(5), Dp(12), Dp(5));
        button.SetTextColor(selected ? Accent : Text);
        button.SetTypeface(Typeface.Default, TypefaceStyle.Bold);
        button.Background = Pressable(selected ? AccentSurface : Surface, 13, selected ? AccentOutline : Outline);
        if (description is not null)
        {
            button.ContentDescription = description;
        }
        if (action is not null)
        {
            button.Click += (_, _) => action();
        }
        return button;
    }

    private View Divider()
    {
        var line = new View(this) { LayoutParameters = new LinearLayout.LayoutParams(-1, Dp(1)) };
        line.SetBackgroundColor(Outline);
        return line;
    }

    private static DateOnly MonthOf(DateOnly date) => new(date.Year, date.Month, 1);

    private static string Capitalize(string text) => char.ToUpper(text[0], Danish) + text[1..];

    private sealed class SafeArea(Context context) : LinearLayout(context)
    {
        public override WindowInsets? OnApplyWindowInsets(WindowInsets? insets)
        {
            if (insets is not null && OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                var bars = insets.GetInsets(WindowInsets.Type.SystemBars() | WindowInsets.Type.DisplayCutout());
                var keyboard = insets.GetInsets(WindowInsets.Type.Ime());
                SetPadding(bars.Left, bars.Top, bars.Right, Math.Max(bars.Bottom, keyboard.Bottom));
            }
            return base.OnApplyWindowInsets(insets);
        }
    }
}
