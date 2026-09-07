using System.Windows.Automation.Peers;
using System.Windows.Media.Animation;

namespace WorkGuard.Windows.Views;

// Decorative view of real session progress; timing and completion never depend on animation.
public sealed class ProgressRing : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(double), typeof(ProgressRing), new PropertyMetadata(0d, Changed));
    private static readonly DependencyProperty DisplayProperty = DependencyProperty.Register("Display", typeof(double), typeof(ProgressRing), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(nameof(Stroke), typeof(Brush), typeof(ProgressRing), new FrameworkPropertyMetadata(Brushes.SeaGreen, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty TrackProperty = DependencyProperty.Register(nameof(Track), typeof(Brush), typeof(ProgressRing), new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromArgb(30, 50, 105, 88)), FrameworkPropertyMetadataOptions.AffectsRender));
    public Brush Stroke { get => (Brush)GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
    public Brush Track { get => (Brush)GetValue(TrackProperty); set => SetValue(TrackProperty, value); }
    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public ProgressRing() { IsHitTestVisible = false; Unloaded += (_, _) => BeginAnimation(DisplayProperty, null); }
    protected override AutomationPeer? OnCreateAutomationPeer() => null;
    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ring = (ProgressRing)d; var target = Math.Clamp(double.IsFinite((double)e.NewValue) ? (double)e.NewValue : 0, 0, 1);
        var from = (double)ring.GetValue(DisplayProperty); ring.SetValue(DisplayProperty, target);
        Motion.TrackClock(ring, DisplayProperty);
        ring.BeginAnimation(DisplayProperty, Motion.Enabled && ring.IsVisible && target > from + .000001 ? new DoubleAnimation(from, target, TimeSpan.FromMilliseconds(500))
        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop } : null);
    }
    protected override void OnRender(DrawingContext context)
    {
        base.OnRender(context);
        var radius = Math.Max(0, Math.Min(ActualWidth, ActualHeight) / 2 - 5);
        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        context.DrawEllipse(null, new Pen(Track, 3), center, radius, radius);
        var value = (double)GetValue(DisplayProperty); if (value <= 0 || radius <= 0) return;
        var pen = new Pen(Stroke, 4) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        if (value >= .99999) { context.DrawEllipse(null, pen, center, radius, radius); return; }
        var angle = value * Math.PI * 2;
        var end = new Point(center.X + Math.Sin(angle) * radius, center.Y - Math.Cos(angle) * radius);
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        { g.BeginFigure(new Point(center.X, center.Y - radius), false, false); g.ArcTo(end, new Size(radius, radius), 0, value > .5, SweepDirection.Clockwise, true, false); }
        geometry.Freeze(); context.DrawGeometry(null, pen, geometry);
    }
}
