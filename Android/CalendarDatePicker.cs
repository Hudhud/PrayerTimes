using Android.App;
using Android.Content;
using Android.Text;
using Android.Widget;

namespace SalahTimes.AndroidApp;

public sealed partial class MainActivity
{
    private static readonly DateOnly FirstCalendarMonth = new(2, 1, 1);
    private static readonly DateOnly LastCalendarMonth = new(9999, 11, 1);

    private void OpenCalendarDatePicker()
    {
        var picked = selectedDate ?? month;
        var fields = new LinearLayout(this) { Orientation = Orientation.Vertical };
        fields.SetPadding(Dp(24), Dp(8), Dp(24), Dp(12));
        var monthLabel = T("Måned", "Month");
        var monthPicker = new Spinner(this) { ContentDescription = monthLabel };
        var months = Enumerable.Range(1, 12).Select(UiCulture.DateTimeFormat.GetMonthName).ToArray();
        var monthOptions = new ArrayAdapter<string>(this, global::Android.Resource.Layout.SimpleSpinnerItem, months);
        monthOptions.SetDropDownViewResource(global::Android.Resource.Layout.SimpleSpinnerDropDownItem);
        monthPicker.Adapter = monthOptions;
        monthPicker.SetSelection(picked.Month - 1);
        fields.AddView(Label(monthLabel, 14, Muted, bold: true), MatchWidth());
        fields.AddView(monthPicker, MatchWidth(bottom: 10));

        var yearLabel = T("År", "Year");
        var year = new EditText(this) { Text = picked.Year.ToString(UiCulture), ContentDescription = yearLabel, InputType = InputTypes.ClassNumber };
        year.SetSingleLine();
        fields.AddView(Label(yearLabel, 14, Muted, bold: true), MatchWidth());
        fields.AddView(year, MatchWidth(bottom: 10));

        var dayLabel = T("Dag", "Day");
        var day = new NumberPicker(this) { MinValue = 1, MaxValue = DateTime.DaysInMonth(picked.Year, picked.Month),
            Value = picked.Day, WrapSelectorWheel = false, ContentDescription = dayLabel };
        fields.AddView(Label(dayLabel, 14, Muted, bold: true), MatchWidth());
        fields.AddView(day, MatchWidth());

        void UpdateDays()
        {
            if (int.TryParse(year.Text, out var selectedYear) && selectedYear is >= 2 and <= 9999)
            {
                var limit = DateTime.DaysInMonth(selectedYear, monthPicker.SelectedItemPosition + 1);
                var previous = day.Value;
                day.MaxValue = limit;
                day.Value = Math.Min(previous, limit);
            }
        }

        monthPicker.ItemSelected += (_, _) => UpdateDays();
        year.TextChanged += (_, _) => UpdateDays();
        var pickerContent = new ScrollView(this) { FillViewport = true };
        pickerContent.AddView(fields);
        var dialog = new AlertDialog.Builder(this)
            .SetTitle(T("Vælg dato", "Choose date"))!
            .SetView(pickerContent)!
            .SetNegativeButton(T("Annuller", "Cancel"), (_, _) => { })!
            .SetPositiveButton(T("Vis bønnetider", "Show prayer times"), (_, _) => { })!
            .Show()!;
        dialog.GetButton((int)DialogButtonType.Positive)!.Click += (_, _) =>
        {
            var selectedMonth = monthPicker.SelectedItemPosition + 1;
            if (!int.TryParse(year.Text, out var selectedYear) || selectedYear is < 2 or > 9999
                || selectedYear == LastCalendarMonth.Year && selectedMonth > LastCalendarMonth.Month)
            {
                year.Error = T("Vælg en dato fra januar i år 2 til november 9999.", "Choose a date between January of year 2 and November 9999.");
                return;
            }
            day.ClearFocus();
            var date = new DateOnly(selectedYear, selectedMonth, Math.Min(day.Value, DateTime.DaysInMonth(selectedYear, selectedMonth)));
            dialog.Dismiss();
            OpenCalendarDate(date);
        };
    }
}
