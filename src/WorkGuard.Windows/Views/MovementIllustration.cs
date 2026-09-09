using System.Diagnostics;
using System.Windows.Threading;

namespace WorkGuard.Windows.Views;

// Schematic vector demonstration, not pose assessment. Its animation never drives session time.
public sealed class MovementIllustration : FrameworkElement
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(40) };
    private readonly Stopwatch _clock = new();
    private MaintenanceExercise? _exercise;
    private bool _playing;
    private int _side = 1;
    internal bool IsAnimating => _timer.IsEnabled;
    public MovementIllustration()
    {
        _timer.Tick += (_, _) => { if (!Motion.Enabled) { Stop(); } InvalidateVisual(); };
        Unloaded += (_, _) => Stop();
        Loaded += (_, _) => UpdateTimer();
        IsVisibleChanged += (_, _) => UpdateTimer();
        IsHitTestVisible = false;
    }
    public void Set(MaintenanceExercise exercise, bool playing, int side = 1)
    {
        side = side == 2 ? 2 : 1;
        if (_exercise?.Id != exercise.Id || _side != side) _clock.Reset();
        _exercise = exercise; _playing = playing; _side = side;
        System.Windows.Automation.AutomationProperties.SetName(this, exercise.Title + "动作示意。" +
            (exercise.Bilateral ? (side == 1 ? "左侧。" : "右侧。") : "") + exercise.Setup + exercise.Cue);
        UpdateTimer(); InvalidateVisual();
    }
    private void UpdateTimer()
    {
        if (IsLoaded && IsVisible && _playing && Motion.Enabled) { _clock.Start(); _timer.Start(); }
        else Stop();
    }
    private void Stop() { _timer.Stop(); _clock.Stop(); }
    private static SolidColorBrush Brush(string hex) { var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); b.Freeze(); return b; }
    private static readonly Brush Skin = Brush("#D5A487"), Shirt = Brush("#789487"), Trousers = Brush("#445951"), Hair = Brush("#3C4943"), Line = Brush("#ADB7A8");
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (_exercise is null || ActualWidth <= 0 || ActualHeight <= 0) return;
        var scale = Math.Min(ActualWidth / 520, Math.Max(1, ActualHeight - 24) / 290);
        var offsetX = (ActualWidth - 520 * scale) / 2;
        dc.PushTransform(new TranslateTransform(offsetX, (ActualHeight - 24 - 290 * scale) / 2));
        dc.PushTransform(new ScaleTransform(scale, scale));
        // A static preview needs distinct positions even before the animation has ever run.
        var animatedFrame = Motion.Enabled && _clock.Elapsed > TimeSpan.Zero;
        var phase = animatedFrame ? (1 - Math.Cos(_clock.Elapsed.TotalSeconds * Math.PI / 3)) / 2 : 1;
        var march = _exercise.Demo == MovementDemo.March;
        var side = march && animatedFrame ? ((int)(_clock.Elapsed.TotalSeconds / 6) % 2) + 1 : _side;
        Figure(dc, 130, march && !animatedFrame ? 1 : 0, .42, march && !animatedFrame ? 1 : side);
        Figure(dc, 380, phase, 1, march && !animatedFrame ? 2 : side);
        dc.Pop(); dc.Pop();
        // Keep captions readable when the figure shrinks for a compact window.
        Label(dc, march && !animatedFrame ? "一侧抬起" : "起始姿势", offsetX + 130 * scale, ActualHeight - 20);
        Label(dc, march ? (animatedFrame ? "放下后，两侧交替" : "放下后，换另一侧") :
            _exercise.Bilateral ? (_side == 1 ? "左侧 · 再回正" : "右侧 · 再回正") : "活动位置 · 再回正", offsetX + 380 * scale, ActualHeight - 20);
    }
    private static void Label(DrawingContext dc, string value, double x, double y)
    {
        var text = new FormattedText(value, System.Globalization.CultureInfo.GetCultureInfo("zh-CN"), FlowDirection.LeftToRight,
            new Typeface("Microsoft YaHei UI"), 13, SystemParameters.HighContrast ? SystemColors.WindowTextBrush : Trousers, 1);
        dc.DrawText(text, new Point(x - text.Width / 2, y));
    }
    private static void Limb(DrawingContext dc, Brush color, double width, params Point[] points)
    {
        var pen = new Pen(SystemParameters.HighContrast ? SystemColors.WindowTextBrush : color, width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        for (var i = 1; i < points.Length; i++) dc.DrawLine(pen, points[i - 1], points[i]);
    }
    private void Figure(DrawingContext dc, double center, double t, double opacity, int side)
    {
        dc.PushOpacity(opacity);
        dc.PushTransform(new TranslateTransform(center, 0));
        // Mirror only the body drawing; labels retain their normal reading direction.
        dc.PushTransform(new ScaleTransform(side == 2 ? -1 : 1, 1));
        var demo = _exercise!.Demo;
        var standing = _exercise.Standing;
        if (demo is MovementDemo.Reach or MovementDemo.OpenChest or MovementDemo.HipExtension or MovementDemo.Hamstring or MovementDemo.Ankle or MovementDemo.SitStand or MovementDemo.Calf)
        {
            SideFigure(dc, demo, standing, t); dc.Pop(); dc.Pop(); dc.Pop(); return;
        }
        var rise = demo == MovementDemo.SitStand ? 45 * t : 0;
        var ankleRise = demo == MovementDemo.Calf ? 10 * t : 0;
        var hipY = (standing ? 195 : 195) - rise - ankleRise;
        var lean = demo == MovementDemo.Hamstring ? 12 * t : demo == MovementDemo.OpenChest ? -4 * t : 0;
        var shoulderY = hipY - 75;
        var head = new Point(lean, shoulderY - 39 + (demo == MovementDemo.Nod ? 4 * t : 0));
        dc.DrawEllipse(Brush("#DCE3D5"), null, new Point(0, 285), 76, 8);
        if (!standing || demo == MovementDemo.SitStand)
        {
            Limb(dc, Line, 5, new(-42, 153), new(-42, 213), new(42, 213));
            Limb(dc, Line, 5, new(-32, 214), new(-32, 282));
            Limb(dc, Line, 5, new(32, 214), new(32, 282));
        }
        if (standing && demo != MovementDemo.SitStand)
        {
            Limb(dc, Line, 5, new(62, 143), new(88, 143));
            Limb(dc, Line, 5, new(81, 143), new(81, 282));
        }
        var kneeLeft = new Point(-22, standing ? 236 - rise : 220 - rise / 2);
        var kneeRight = new Point(25, standing ? 236 - rise : 220 - rise / 2);
        var footLeft = new Point(-23, 275);
        var footRight = new Point(28, 275);
        if (demo == MovementDemo.March)
        {
            kneeLeft = new(-24 - 8 * t, kneeLeft.Y - 26 * t);
            footLeft = new(-28 - 8 * t, footLeft.Y - 27 * t);
        }
        if (demo == MovementDemo.HipExtension) { kneeLeft = new(-22 - 22 * t, 233); footLeft = new(-23 - 35 * t, 272); }
        if (demo is MovementDemo.Hamstring or MovementDemo.Ankle) { kneeLeft = new(-34, 224); footLeft = new(-67, 268); }
        Limb(dc, Trousers, 19, new(-14, hipY), kneeLeft, footLeft);
        Limb(dc, Trousers, 19, new(15, hipY), kneeRight, footRight);
        Limb(dc, Hair, 10, footLeft, new(footLeft.X - 13, footLeft.Y - (demo == MovementDemo.Ankle ? 12 * t : 0)));
        Limb(dc, Hair, 10, footRight, new(footRight.X + 13, footRight.Y));
        var shoulderWidth = demo == MovementDemo.Rotate ? 29 - 10 * t : 29;
        var torso = new StreamGeometry();
        using (var g = torso.Open())
        {
            g.BeginFigure(new(-shoulderWidth + lean, shoulderY), true, true);
            g.BezierTo(new(-17 + lean, shoulderY - 13), new(17 + lean, shoulderY - 13), new(shoulderWidth + lean, shoulderY), true, false);
            g.LineTo(new(27, hipY), true, false); g.QuadraticBezierTo(new(0, hipY + 10), new(-27, hipY), true, false);
        }
        dc.DrawGeometry(SystemParameters.HighContrast ? SystemColors.HighlightBrush : Shirt, null, torso);
        Limb(dc, Skin, 14, new(lean, shoulderY - 15), new(lean, shoulderY - 3));
        dc.DrawEllipse(Skin, null, head, 19, 24);
        dc.DrawGeometry(Hair, null, Geometry.Parse(FormattableString.Invariant($"M {head.X-19},{head.Y-4} Q {head.X-26},{head.Y-29} {head.X},{head.Y-27} Q {head.X+24},{head.Y-27} {head.X+19},{head.Y-4} Q {head.X+9},{head.Y-15} {head.X-19},{head.Y-4}")));
        var look = demo is MovementDemo.NeckTurn or MovementDemo.Rotate ? 10 * t : 0;
        dc.DrawEllipse(Hair, null, new Point(head.X - 6 + look, head.Y + 1), 1.5, 1.5);
        dc.DrawEllipse(Hair, null, new Point(head.X + 6 + look, head.Y + 1), 1.5, 1.5);
        var leftHand = new Point(-30, hipY - 3); var rightHand = new Point(32, hipY - 3);
        var leftElbow = new Point(-38 + lean, shoulderY + 40); var rightElbow = new Point(38 + lean, shoulderY + 40);
        if (demo == MovementDemo.Reach) { leftHand = new(-18 - 11 * t, shoulderY + 15 - 8 * t); rightHand = new(18 + 11 * t, shoulderY + 15 - 8 * t); leftElbow = new(-42 - 7 * t, shoulderY + 24); rightElbow = new(42 + 7 * t, shoulderY + 24); }
        if (demo == MovementDemo.Rotate) { leftHand = new(18 - 12 * t, shoulderY + 9); rightHand = new(-18 - 12 * t, shoulderY + 9); leftElbow = new(-35, shoulderY + 30); rightElbow = new(32 - 10 * t, shoulderY + 30); }
        if (demo == MovementDemo.Shoulders) { leftElbow.X += 9 * t; rightElbow.X -= 9 * t; leftHand.X += 6 * t; rightHand.X -= 6 * t; }
        if (standing && demo != MovementDemo.SitStand) { rightElbow = new(47, shoulderY + 28); rightHand = new(71, 138); }
        Limb(dc, Skin, 12, new(-shoulderWidth + lean, shoulderY + 3), leftElbow, leftHand);
        Limb(dc, Skin, 12, new(shoulderWidth + lean, shoulderY + 3), rightElbow, rightHand);
        if (demo == MovementDemo.OpenChest)
            Limb(dc, Brush("#BCD0BD"), 3, new(-10, shoulderY + 21 - t * 4), new(0, shoulderY + 17 - t * 4), new(10, shoulderY + 21 - t * 4));
        dc.Pop(); dc.Pop(); dc.Pop();
    }
    private static void SideFigure(DrawingContext dc, MovementDemo demo, bool standing, double t)
    {
        var sitStand = demo == MovementDemo.SitStand;
        var rise = sitStand ? 55 * t : 0;
        var heelLift = demo == MovementDemo.Calf ? 9 * t : 0;
        var hip = new Point(sitStand ? 26 * t : 0, (standing && !sitStand ? 195 : 211) - rise - heelLift);
        var lean = demo == MovementDemo.Hamstring ? 20 * t : demo == MovementDemo.OpenChest ? -6 * t : sitStand ? 10 * Math.Sin(t * Math.PI) : 0;
        var shoulder = new Point(hip.X + lean, hip.Y - 76);
        var head = new Point(shoulder.X + lean * .2, shoulder.Y - 36);
        dc.DrawEllipse(Brush("#DCE3D5"), null, new Point(12, 285), 80, 8);
        if (!standing || sitStand)
        {
            Limb(dc, Line, 5, new(-28, 158), new(-28, 218), new(39, 218));
            Limb(dc, Line, 5, new(-21, 218), new(-21, 282));
            Limb(dc, Line, 5, new(32, 218), new(32, 282));
        }
        if (standing && !sitStand)
        {
            Limb(dc, Line, 5, new(49, 145), new(84, 145));
            Limb(dc, Line, 5, new(76, 145), new(76, 282));
        }
        var knee = standing && !sitStand ? new Point(0, 238 - heelLift) : new Point(43, 219 + rise * .26);
        var foot = standing && !sitStand ? new Point(0, 276 - heelLift) : new Point(48, 276);
        Limb(dc, Trousers, 17, hip, knee, foot);
        Limb(dc, Hair, 10, foot, new(foot.X + 16, 278));
        var movingKnee = knee; var movingFoot = foot;
        if (demo == MovementDemo.HipExtension) { movingKnee.X -= 19 * t; movingFoot.X -= 38 * t; movingFoot.Y -= 5 * t; }
        if (demo is MovementDemo.Hamstring or MovementDemo.Ankle) { movingKnee = new(42, 231); movingFoot = new(77, 270); }
        Limb(dc, Trousers, 19, new(hip.X + 4, hip.Y), movingKnee, movingFoot);
        var toe = new Point(movingFoot.X + 17, movingFoot.Y + 2 - (demo == MovementDemo.Ankle ? t * 14 : 0));
        Limb(dc, Hair, 10, movingFoot, toe);
        Limb(dc, Shirt, 44, hip, shoulder);
        Limb(dc, Skin, 12, new(shoulder.X, shoulder.Y - 6), new(head.X, head.Y + 16));
        dc.DrawEllipse(Skin, null, head, 19, 24);
        dc.DrawEllipse(Hair, null, new Point(head.X - 5, head.Y - 10), 17, 16);
        dc.DrawEllipse(Hair, null, new Point(head.X + 12, head.Y - 1), 1.5, 1.5);
        var elbow = new Point(shoulder.X + 12, shoulder.Y + 40);
        var hand = new Point(hip.X + 39, hip.Y + 1);
        if (demo == MovementDemo.Reach)
        {
            // Side view makes the forward scapular glide legible without implying a wide arm swing.
            elbow = new(shoulder.X + 31 + 7 * t, shoulder.Y + 10 - 4 * t);
            hand = new(shoulder.X + 60 + 12 * t, shoulder.Y + 6 - 4 * t);
        }
        if (standing && !sitStand) { elbow = new(shoulder.X + 31, shoulder.Y + 23); hand = new(65, 140); }
        Limb(dc, Skin, 12, new(shoulder.X + 5, shoulder.Y + 4), elbow, hand);
    }

}
