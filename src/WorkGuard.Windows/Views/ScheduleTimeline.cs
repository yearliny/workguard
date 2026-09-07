using System.Globalization;
using System.Windows.Media;

namespace WorkGuard.Windows.Views;

// A native, static 24-hour calendar strip. No animation or health-score inference.
public sealed class ScheduleTimeline : FrameworkElement
{
    private Preferences _preferences = new();
    private DateTime _now;
    public void Update(Preferences preferences, DateTime localNow)
    {
        var minute = new DateTime(localNow.Year, localNow.Month, localNow.Day, localNow.Hour, localNow.Minute, 0);
        if (_preferences == preferences && _now == minute) return;
        _preferences = preferences; _now = minute; InvalidateVisual();
    }
    protected override void OnRender(DrawingContext drawing)
    {
        base.OnRender(drawing);
        var left = 8d; var width = Math.Max(0, ActualWidth - 16);
        var track = new Rect(left, 12, width, 22);
        drawing.DrawRoundedRectangle(Brush("#EDF0E9"), null, track, 8, 8);
        drawing.PushClip(new RectangleGeometry(track, 8, 8));
        var start = 0; var previous = KindAt(0);
        for (var minute = 1; minute <= 1440; minute++)
        {
            var kind = minute == 1440 ? -1 : KindAt(minute);
            if (kind == previous) continue;
            if (previous > 0)
                drawing.DrawRectangle(Brush(previous == 1 ? "#709782" : "#EAC9A6"), null,
                    new Rect(left + start / 1440d * width, 12, (minute - start) / 1440d * width, 22));
            start = minute; previous = kind;
        }
        drawing.Pop();
        var x = left + _now.TimeOfDay.TotalMinutes / 1440 * width;
        drawing.DrawLine(new Pen(Brush("#203B34"), 2), new Point(x, 6), new Point(x, 40));
        drawing.DrawEllipse(Brush("#203B34"), null, new Point(x, 6), 3, 3);
        foreach (var hour in new[] { 0, 6, 12, 18, 24 })
        {
            var text = new FormattedText($"{hour:00}:00", CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), 12, Brush("#64736D"), VisualTreeHelper.GetDpi(this).PixelsPerDip);
            var pos = left + hour / 24d * width;
            drawing.DrawText(text, new Point(Math.Clamp(pos - text.Width / 2, 0, Math.Max(0, ActualWidth - text.Width)), 50));
        }
    }
    private int KindAt(int minute)
    {
        var time = _now.Date.AddMinutes(minute);
        if (!WorkSchedule.Allows(_preferences, time)) return 0;
        return _preferences.IsQuietTime(TimeOnly.FromDateTime(time)) ? 2 : 1;
    }
    private static SolidColorBrush Brush(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)); brush.Freeze(); return brush;
    }
}
