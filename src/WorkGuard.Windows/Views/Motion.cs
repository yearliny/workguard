using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace WorkGuard.Windows.Views;

// Short, finite animations only. No background rendering loop or delayed application actions.
internal static class Motion
{
    private static readonly List<WeakReference<UIElement>> Targets = [];
    private static readonly List<(WeakReference<DependencyObject> Target, DependencyProperty Property)> Clocks = [];
    private static bool _reduced;
    internal static bool? SystemAnimationOverride { get; set; }
    public static bool Enabled => !_reduced && (SystemAnimationOverride ?? SystemParameters.ClientAreaAnimation) && !SystemParameters.HighContrast;
    public static void Configure(bool reduced)
    {
        _reduced = reduced;
        if (Enabled) return;
        foreach (var reference in Targets)
            if (reference.TryGetTarget(out var target)) Reset(target);
        Targets.Clear();
        foreach (var (reference, property) in Clocks)
            if (reference.TryGetTarget(out var target)) ((IAnimatable)target).BeginAnimation(property, null);
        Clocks.Clear();
    }
    public static readonly DependencyProperty EnterProperty = DependencyProperty.RegisterAttached("Enter", typeof(bool), typeof(Motion), new PropertyMetadata(false, EnterChanged));
    public static bool GetEnter(DependencyObject value) => (bool)value.GetValue(EnterProperty);
    public static void SetEnter(DependencyObject value, bool enabled) => value.SetValue(EnterProperty, enabled);
    private static void EnterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element) return;
        if ((bool)e.NewValue) { element.Loaded += Entered; element.Unloaded += Left; }
        else { element.Loaded -= Entered; element.Unloaded -= Left; Reset(element); }
    }
    private static void Entered(object sender, RoutedEventArgs e) => Reveal((FrameworkElement)sender);
    private static void Left(object sender, RoutedEventArgs e) => Reset((UIElement)sender);
    public static void Reveal(FrameworkElement element)
    {
        Reset(element);
        if (!Enabled || !element.IsVisible) return;
        var translate = new TranslateTransform(); element.RenderTransform = translate;
        Track(element);
        element.BeginAnimation(UIElement.OpacityProperty, Animation(0, 1, 320));
        translate.BeginAnimation(TranslateTransform.YProperty, Animation(12, 0, 380));
    }
    private static DoubleAnimation Animation(double from, double to, int milliseconds) => new(from, to, TimeSpan.FromMilliseconds(milliseconds))
    { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop };
    public static void TrackClock(DependencyObject target, DependencyProperty property)
    {
        Clocks.RemoveAll(x => !x.Target.TryGetTarget(out var old) || old == target && x.Property == property);
        Clocks.Add((new(target), property));
    }
    private static void Track(UIElement element)
    {
        Targets.RemoveAll(w => !w.TryGetTarget(out var existing) || existing == element);
        Targets.Add(new(element));
    }
    private static void Reset(UIElement element)
    {
        element.BeginAnimation(UIElement.OpacityProperty, null);
        if (element.RenderTransform is TranslateTransform translate) translate.BeginAnimation(TranslateTransform.YProperty, null);
        if (element.RenderTransform is ScaleTransform scale)
        { scale.BeginAnimation(ScaleTransform.ScaleXProperty, null); scale.BeginAnimation(ScaleTransform.ScaleYProperty, null); scale.ScaleX = scale.ScaleY = 1; }
    }
    public static readonly DependencyProperty InteractiveProperty = DependencyProperty.RegisterAttached("Interactive", typeof(bool), typeof(Motion), new PropertyMetadata(false, InteractiveChanged));
    public static bool GetInteractive(DependencyObject value) => (bool)value.GetValue(InteractiveProperty);
    public static void SetInteractive(DependencyObject value, bool enabled) => value.SetValue(InteractiveProperty, enabled);
    private static void InteractiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Button button) return;
        if ((bool)e.NewValue)
        {
            button.MouseEnter += Hover; button.MouseLeave += Hover;
            button.PreviewMouseLeftButtonDown += Press; button.PreviewMouseLeftButtonUp += Press;
            button.Unloaded += Left;
        }
        else
        {
            button.MouseEnter -= Hover; button.MouseLeave -= Hover;
            button.PreviewMouseLeftButtonDown -= Press; button.PreviewMouseLeftButtonUp -= Press;
            button.Unloaded -= Left; Reset(button);
        }
    }
    private static void Hover(object sender, System.Windows.Input.MouseEventArgs e) => Scale((Button)sender, ((Button)sender).IsMouseOver ? 1.015 : 1);
    private static void Press(object sender, System.Windows.Input.MouseButtonEventArgs e) => Scale((Button)sender, e.ButtonState == System.Windows.Input.MouseButtonState.Pressed ? .98 : 1);
    private static void Scale(Button button, double target)
    {
        if (!Enabled || !button.IsEnabled) { Reset(button); return; }
        if (button.RenderTransform is not ScaleTransform)
        { button.RenderTransform = new ScaleTransform(1, 1); button.RenderTransformOrigin = new Point(.5, .5); }
        var scale = (ScaleTransform)button.RenderTransform;
        var from = scale.ScaleX;
        scale.ScaleX = scale.ScaleY = target;
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, Animation(from, target, 160));
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, Animation(from, target, 160)); Track(button);
    }
}
