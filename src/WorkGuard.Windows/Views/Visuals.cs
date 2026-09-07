using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace WorkGuard.Windows.Views;

// Icons supplement readable labels; decorative glyphs are excluded from screen readers.
public sealed class Symbol : TextBlock
{
    private static readonly FontFamily IconFont = new(
        Fonts.SystemFontFamilies.Any(f => f.Source == "Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets");
    public Symbol()
    {
        FontFamily = IconFont;
        FontSize = 18;
        VerticalAlignment = VerticalAlignment.Center;
        HorizontalAlignment = HorizontalAlignment.Center;
        IsHitTestVisible = false;
        Focusable = false;
    }
    protected override AutomationPeer? OnCreateAutomationPeer() => null;
}

public static class Visuals
{
    public static readonly DependencyProperty GlyphProperty = DependencyProperty.RegisterAttached(
        "Glyph", typeof(string), typeof(Visuals), new PropertyMetadata(""));
    public static string GetGlyph(DependencyObject target) => (string)target.GetValue(GlyphProperty);
    public static void SetGlyph(DependencyObject target, string value) => target.SetValue(GlyphProperty, value);
}
