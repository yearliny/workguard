namespace WorkGuard.Windows.Views;

// Original, offline vector scenes. Cached and frozen; no image decoding or animation loop.
internal static class RestArtwork
{
    private static readonly Dictionary<RestScene, ImageSource> Scenes = Enum.GetValues<RestScene>()
        .ToDictionary(scene => scene, Create);

    public static ImageSource For(RestScene scene) => Scenes[scene];

    private static ImageSource Create(RestScene scene)
    {
        var group = new DrawingGroup();
        using (var c = group.Open())
        {
            var colors = scene switch
            {
                RestScene.Distance => ("#E1EBE9", "#FAF5E9", "#648C89"),
                RestScene.Shoulders => ("#EBDAC8", "#FBF0DE", "#A87A60"),
                RestScene.Hands => ("#E4DFEB", "#F9F1E9", "#91819D"),
                _ => ("#DCE6D3", "#F9F4DE", "#718568")
            };
            c.DrawRectangle(Gradient(colors.Item2, colors.Item1), null, new Rect(0, 0, 600, 720));
            // A quiet architectural frame and fine inset line give each composition a shared language.
            Shape(c, "#FFFFFF", "M40,510 L40,250 C40,100 135,48 300,48 C465,48 560,100 560,250 L560,510 Z", .26);
            var arch = Geometry.Parse("M76,510 L76,260 C76,130 164,84 300,84 C436,84 524,130 524,260 L524,510 Z");
            c.PushClip(arch);
            c.DrawRectangle(Gradient(colors.Item2, colors.Item1), null, new Rect(76, 84, 448, 440));
            switch (scene)
            {
                case RestScene.Distance:
                    Sun(c, 380, 205, 49, "#F2C780");
                    Shape(c, "#B6CACA", "M50,355 C130,255 205,285 285,340 C360,265 460,262 555,323 L555,530 L50,530 Z");
                    Shape(c, "#829F9A", "M50,390 C145,350 195,367 282,398 C366,334 457,342 555,383 L555,530 L50,530 Z");
                    Shape(c, "#D7E3DB", "M50,445 C210,385 399,425 555,411 L555,535 L50,535 Z");
                    Line(c, "#F7F6E7", "M170,456 C260,436 350,448 435,438", 2);
                    Line(c, "#F7F6E7", "M232,475 C306,463 358,472 408,464", 2);
                    break;
                case RestScene.Movement:
                    Sun(c, 207, 203, 46, "#E9C57C");
                    Shape(c, "#BECBA5", "M55,340 C170,280 263,339 340,320 C430,290 483,303 555,340 L555,530 L55,530 Z");
                    Shape(c, "#8EA483", "M55,422 C160,348 300,351 555,417 L555,530 L55,530 Z");
                    Shape(c, "#F5E9CD", "M335,333 C235,390 398,392 331,443 C305,463 246,478 192,530 L420,530 C470,437 255,405 350,336 Z");
                    Tree(c, 450, 340, .82, "#55745C");
                    Tree(c, 145, 380, .60, "#728B69");
                    break;
                case RestScene.Shoulders:
                    Sun(c, 365, 230, 72, "#EDC28E");
                    Shape(c, "#D5B9A0", "M60,421 C180,341 288,342 548,395 L548,535 L60,535 Z");
                    Shape(c, "#AB8E76", "M60,480 C227,381 402,448 550,423 L550,535 L60,535 Z");
                    // Soft suspended folds, an abstract cue to release tension rather than an exercise diagram.
                    Shape(c, "#FCF5E8", "M90,70 L170,70 C133,199 197,223 156,340 C139,389 155,438 186,502 L86,502 Z", .88);
                    Line(c, "#DCCBB8", "M135,105 C99,229 169,257 129,356 C115,402 141,454 150,477", 2);
                    Line(c, "#FAECD5", "M264,412 C310,392 361,394 405,405", 2);
                    break;
                default:
                    Sun(c, 352, 226, 66, "#D5C1CC");
                    Shape(c, "#B7B1C1", "M65,391 C221,323 410,348 550,394 L550,535 L65,535 Z");
                    Shape(c, "#DDD3D3", "M65,450 C242,405 430,444 550,420 L550,535 L65,535 Z");
                    // A ceramic cup and a branch: the hands can leave the keyboard.
                    c.DrawEllipse(Brush("#A59BAA"), null, new Point(316, 474), 92, 13);
                    c.DrawEllipse(null, new Pen(Brush("#FCF6EB"), 13), new Point(369, 428), 28, 25);
                    Shape(c, "#FAF4E9", "M247,395 L361,395 L350,455 Q304,485 258,455 Z");
                    c.DrawEllipse(Brush("#D8C3B5"), null, new Point(304, 395), 57, 10);
                    Line(c, "#FBF7ED", "M290,367 C272,347 303,337 288,316 M321,365 C309,347 338,333 324,315", 3);
                    break;
            }
            c.Pop();
            c.DrawGeometry(null, new Pen(Brush("#FFFAEF"), 2), arch);
            Shape(c, "#405C4C", "M50,562 C210,535 400,554 552,568 L552,579 C410,563 188,552 50,575 Z", .08);
            // Foreground botanical shapes stay below the focal point and above the caption.
            Leaf(c, 94, 565, colors.Item3);
            c.DrawRoundedRectangle(Brush("#E5D4BE"), null, new Rect(67, 540, 56, 52), 10, 10);
            Line(c, "#FCF8EE", "M164,563 L481,563", 1);
        }
        group.Freeze();
        var image = new DrawingImage(group); image.Freeze(); return image;
    }

    private static SolidColorBrush Brush(string hex) => new((Color)ColorConverter.ConvertFromString(hex));
    private static LinearGradientBrush Gradient(string top, string bottom) => new(
        (Color)ColorConverter.ConvertFromString(top), (Color)ColorConverter.ConvertFromString(bottom), 90);
    private static void Shape(DrawingContext c, string color, string path, double opacity = 1)
    { c.PushOpacity(opacity); c.DrawGeometry(Brush(color), null, Geometry.Parse(path)); c.Pop(); }
    private static void Line(DrawingContext c, string color, string path, double width)
    { c.DrawGeometry(null, new Pen(Brush(color), width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, Geometry.Parse(path)); }
    private static void Sun(DrawingContext c, double x, double y, double radius, string color)
    {
        c.PushOpacity(.20); c.DrawEllipse(Brush(color), null, new Point(x, y), radius + 15, radius + 15); c.Pop();
        c.DrawEllipse(Brush(color), null, new Point(x, y), radius, radius);
    }
    private static void Tree(DrawingContext c, double x, double y, double scale, string color)
    {
        c.PushTransform(new TranslateTransform(x, y)); c.PushTransform(new ScaleTransform(scale, scale));
        c.DrawRoundedRectangle(Brush("#8A8467"), null, new Rect(-3, -80, 6, 107), 3, 3);
        c.DrawEllipse(Brush(color), null, new Point(0, -102), 38, 77); c.Pop(); c.Pop();
    }
    private static void Leaf(DrawingContext c, double x, double y, string color)
    {
        c.PushTransform(new TranslateTransform(x, y));
        Line(c, color, "M0,0 C-4,-45 4,-90 18,-132", 3);
        Shape(c, color, "M1,-35 C-50,-33 -60,-65 -52,-83 C-15,-80 4,-58 1,-35 Z");
        Shape(c, color, "M5,-63 C42,-62 62,-88 53,-106 C24,-107 5,-83 5,-63 Z");
        Shape(c, color, "M11,-99 C-21,-105 -34,-133 -23,-151 C4,-143 15,-119 11,-99 Z");
        Shape(c, color, "M18,-132 C45,-137 47,-162 39,-176 C17,-165 14,-147 18,-132 Z");
        c.Pop();
    }
}
