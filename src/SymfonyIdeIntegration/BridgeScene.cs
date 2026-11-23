using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Media;

namespace SymfonyIdeIntegration;

internal sealed class BridgeScene : Control
{
    private readonly Bitmap bridge = LoadArt("Bridge");
    private readonly Bitmap head = LoadArt("Head");
    private readonly Bitmap headLeft = LoadArt("HeadLeft");
    private readonly Bitmap headRight = LoadArt("HeadRight");
    private readonly Bitmap shirt = LoadArt("Shirt");
    private readonly PilotArm arm;
    private readonly Bitmap hand = LoadArt("Hand");
    private readonly Bitmap seat = LoadArt("Seat");
    private readonly Stream theme = typeof(BridgeScene).Assembly.GetManifestResourceStream("SymfonyIdeIntegration.BridgeTheme.wav")!;
    private readonly SoundPlayer music;
    private readonly Stopwatch clock = new();
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 16 };
    private readonly Font display = new("Consolas", 8, FontStyle.Bold, GraphicsUnit.Pixel);
    private readonly Font code = new("Consolas", 5.8f, GraphicsUnit.Pixel);
    private readonly StringFormat textFormat = StringFormat.GenericTypographic;
    private readonly (float X, float Y, double Phase, double Speed)[] stars =
        new (float, float, double, double)[120];

    private static Bitmap LoadArt(string name)
    {
        using var stream = typeof(BridgeScene).Assembly.GetManifestResourceStream($"SymfonyIdeIntegration.Art.{name}.png")!;
        using var source = new Bitmap(stream);
        return source.Clone(new Rectangle(Point.Empty, source.Size), PixelFormat.Format32bppPArgb);
    }

    internal BridgeScene()
    {
        using var skin = LoadArt("Forearm");
        arm = new PilotArm(skin);
        DoubleBuffered = true;
        TabStop = false;
        AccessibleRole = AccessibleRole.Graphic;
        AccessibleName = "Starship pilot working at two monitors";
        music = new SoundPlayer(theme);
        music.Load();
        var random = new Random(431);
        for (int i = 0; i < stars.Length; i++)
        {
            double angle = random.NextDouble() * Math.Tau;
            stars[i] = ((float)Math.Cos(angle), (float)Math.Sin(angle), random.NextDouble(), .12 + .16 * random.NextDouble());
        }
        timer.Tick += (_, _) => Invalidate();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        timer.Enabled = Visible;
        if (Visible)
        {
            music.PlayLooping();
            clock.Restart();
        }
        else
        {
            music.Stop();
            clock.Stop();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (ClientSize.Width == 0 || ClientSize.Height == 0) return;
        var g = e.Graphics;
        g.ScaleTransform(ClientSize.Width / 800f, ClientSize.Height / 420f);
        Render(g, clock.Elapsed.TotalSeconds);
    }

    private void Render(Graphics g, double t)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBilinear;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        g.DrawImage(bridge, new Rectangle(0, 0, 800, 420));
        DrawStars(g, t);
        DrawMonitor(g, t, true);
        DrawMonitor(g, t, false);
        DrawKeyboard(g);
        DrawPilot(g, t);
    }

    private void DrawStars(Graphics g, double t)
    {
        var state = g.Save();
        using var window = new GraphicsPath();
        window.AddPolygon([new PointF(105, 52), new(692, 52), new(752, 182), new(703, 245), new(90, 245), new(49, 174)]);
        g.SetClip(window, CombineMode.Intersect);
        using var trail = new Pen(Color.White);
        foreach (var star in stars)
        {
            double z = (star.Phase + t * star.Speed) % 1;
            float r = (float)(11 + 450 * z * z);
            float x = star.X * r, y = star.Y * r * .60f;
            trail.Color = Color.FromArgb((int)(15 + z * z * 156), 192, 226, 255);
            trail.Width = (float)(.45 + z * .75);
            float tail = (float)(1 - .10 * z);
            g.DrawLine(trail, 408 + x * tail, 132 + y * tail, 408 + x, 132 + y);
        }
        g.Restore(state);
    }

    private static Color Rgb(int hex) => Color.FromArgb(unchecked((int)0xff000000) | hex);

    private static void Gradient(Graphics g, RectangleF bounds, int top, int bottom)
    {
        using var fill = new LinearGradientBrush(bounds, Rgb(top), Rgb(bottom), 90f);
        g.FillRectangle(fill, bounds);
    }

    private static void Stroke(Graphics g, Color color, float width, PointF a, PointF b)
    {
        using var pen = new Pen(color, width) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(pen, a, b);
    }

    private void TextAt(Graphics g, string text, Font font, int color, float x, float y)
    {
        using var ink = new SolidBrush(Rgb(color));
        g.DrawString(text, font, ink, x, y, textFormat);
    }

    private static readonly string[] SourceLines = [
        "namespace App\\Controller;", "use App\\Service\\Orbit;", "", "#[Route('/bridge')]",
        "public function index(): Response", "{", "    $orbit = $this->orbit;", "    return $this->render(",
        "        'bridge/index.html.twig',", "        ['orbit' => $orbit]", "    );", "}"
    ];
    private static readonly int[] Growth = [12, 15, 13, 19, 17, 9, 6, 11, 14, 18, 16, 21];
    private static readonly int[] Inflation = [6, 8, 12, 10, 15, 19, 23, 21, 16, 12, 9, 8];

    private void DrawMonitor(Graphics g, double t, bool left)
    {
        var state = g.Save();
        const float inwardSlope = .025f;
        using var perspective = new Matrix(1, left ? -inwardSlope : inwardSlope, 0, 1,
            left ? 151 : 437, left ? 162 + 212 * inwardSlope : 162);
        g.MultiplyTransform(perspective);
        using var shadow = new SolidBrush(Color.FromArgb(95, 3, 12, 20));
        g.FillEllipse(shadow, 56, 119, 110, 10);
        Gradient(g, new(93, 92, 17, 30), 0x111922, 0x475567);
        Gradient(g, new(72, 119, 62, 4), 0x758491, 0x17232c);
        Gradient(g, new(0, 0, 212, 104), 0x809099, 0x101b25);
        Gradient(g, new(1.6f, 1.5f, 209, 100), 0x18202b, 0x080e19);
        Gradient(g, new(5, 5, 202, 91), left ? 0x162d3e : 0x252c44, left ? 0x091624 : 0x10182a);
        Stroke(g, Rgb(left ? 0x67c5ce : 0xdcae85), .8f, new(11, 19), new(200, 19));
        if (left)
        {
            TextAt(g, "BridgeController.php", display, 0x99e6dd, 10, 7);
            int offset = (int)(t / 2) % SourceLines.Length;
            for (int i = 0; i < 9; i++)
            {
                int row = (i + offset) % SourceLines.Length;
                TextAt(g, (row + 1).ToString("00"), code, 0x638896, 10, 24 + i * 7.4f);
                TextAt(g, SourceLines[row], code, row % 3 == 0 ? 0xebbd99 : 0xb7d8de, 25, 24 + i * 7.4f);
            }
        }
        else
        {
            bool growth = t % 10 < 5;
            TextAt(g, growth ? "GDP GROWTH" : "CPI INFLATION", display, 0xe6e4de, 12, 7);
            TextAt(g, "YoY %", code, 0xbac5d1, 177, 9);
            int[] data = growth ? Growth : Inflation;
            float reveal = (float)Ease(t % 5 / 1.15);
            for (int i = 0; i < 4; i++)
            {
                int y = 84 - i * 17;
                TextAt(g, i.ToString(), code, 0x8da8bb, 11, y - 3);
                Stroke(g, Rgb(0x3d4d64), .6f, new(22, y), new(198, y));
            }
            for (int i = 0; i < data.Length; i++)
            {
                int x = 24 + 15 * i;
                if (growth && reveal > 0)
                {
                    float height = data[i] * 2.3f * reveal;
                    // GDI+ can fail to fill a near-zero gradient as a new chart appears.
                    if (height < .5f) continue;
                    Gradient(g, new(x, 84 - height, 9, height), 0x72e7dd, 0x326477);
                }
                else if (!growth && i > 0 && reveal * 11 > i - 1)
                {
                    float part = Math.Clamp(reveal * 11 - i + 1, 0, 1);
                    float from = 84 - data[i - 1] * 2.3f;
                    Stroke(g, Rgb(0xffbd88), 1.8f, new(x - 15, from), new(x - 15 + part * 15, from + part * (data[i - 1] - data[i]) * 2.3f));
                }
            }
        }
        Stroke(g, Rgb(0x85edef), 1, new(192, 99), new(197, 99));
        g.Restore(state);
    }

    private static void DrawKeyboard(Graphics g)
    {
        var state = g.Save();
        using var skew = new Matrix(1, 0, -.22f, 1, 293, 305);
        g.MultiplyTransform(skew);
        Gradient(g, new(0, 0, 211, 29), 0x516779, 0x131d29);
        for (int row = 0; row < 4; row++)
        {
            for (int col = 0; col < 18; col++)
            {
                float x = 4 + col * 11.3f + row;
                Gradient(g, new(x, 2 + row * 6, 9, 4.6f), 0x82939b, 0x2d3847);
            }
        }
        g.Restore(state);
    }

    private static double Ease(double value)
    {
        double u = Math.Clamp(value, 0, 1);
        return u * u * (3 - 2 * u);
    }

    private static float Mix(float a, float b, double amount) => a + (float)((b - a) * amount);

    private static float ScreenGaze(double time)
    {
        // Dwell on each screen, with two quick checks among the slower turns.
        double t = time % 16;
        return (float)(-1 + 2 * (Ease((t - 2.6) / .58) - Ease((t - 6.2) / .24)
            + Ease((t - 8.2) / .32) - Ease((t - 13.1) / .68)));
    }

    private void DrawPilot(Graphics g, double t)
    {
        double beat = t * Math.Tau * 125 / 60;
        float sway = (float)(3.4 * Math.Sin(beat / 2) + .7 * Math.Sin(beat));
        float bob = (float)(1.5 * Math.Sin(beat) + .6 * Math.Sin(t * 1.7));
        double cycle = t % 16 - 8;
        double grip = Ease(cycle / .7) - Ease((cycle - 3.6) / .7);
        double lift = Ease((cycle - .7) / .9) - Ease((cycle - 2.7) / .9);
        double tip = Ease((cycle - 1.55) / .35) - Ease((cycle - 2.5) / .35);
        float torsoX = 400 + sway + (float)(grip * 4);
        float look = ScreenGaze(t);
        float headX = torsoX + look * 3;
        double mouseTime = t % 8;
        double reach = Ease((mouseTime - 5) / .4) - Ease((mouseTime - 7.1) / .4);
        float mouseX = 523 + (float)(reach * 3.5 * Math.Sin(t * 6));
        using var mouseFill = new LinearGradientBrush(new RectangleF(mouseX - 9, 307, 19, 18), Rgb(0x7a8b95), Rgb(0x1a222d), 75f);
        g.FillEllipse(mouseFill, mouseX - 9, 307, 19, 18);
        Stroke(g, Rgb(0x84e9ed), 1.2f, new(mouseX, 309), new(mouseX, 313));

        float glassX = Mix(549, headX + 32, lift);
        float glassY = Mix(309, 226 + bob, lift) - (float)(8 * Math.Sin(Math.PI * lift));
        var rightHand = new PointF(Mix(Mix(482, mouseX, reach), glassX + 8, grip),
            Mix(315 + (float)((1 - reach) * 1.1 * Math.Sin(t * 13)), glassY + 5, grip));
        var leftHand = new PointF(310 + (float)Math.Sin(t * 2.2) * 2, 315 + (float)Math.Sin(t * 15));
        var leftElbow = new PointF(torsoX - 86, 347 + bob);
        var rightElbow = new PointF(torsoX + 86 + (float)(grip * 14), 347 + bob - (float)(lift * 66));

        arm.Draw(g, new(torsoX - 67, 303 + bob), leftElbow, leftHand);
        arm.Draw(g, new(torsoX + 67, 303 + bob), rightElbow, rightHand);
        DrawHand(g, leftHand, t, false, 0);
        DrawGlass(g, glassX, glassY, (float)tip);
        DrawHand(g, rightHand, t, true, Math.Max(reach, grip));
        // Pin the hips to the seat and let the shoulder line lead the dance.
        PointF[] shirtPlane = [new(torsoX - 83, 238 + bob - sway * .15f),
            new(torsoX + 83, 238 + bob + sway * .15f), new(317, 426)];
        g.DrawImage(shirt, shirtPlane);
        PointF[] headPlane = [new(headX - 30 + look * 2, 164 + bob - look),
            new(headX + 30 + look * 2, 164 + bob + look), new(torsoX - 30, 243 + bob)];
        // Each strip holds twelve intermediate angles from the rear view to a monitor.
        const int turnFrames = 12;
        int pose = (int)MathF.Round(MathF.Abs(look) * turnFrames);
        if (pose == 0) g.DrawImage(head, headPlane);
        else
        {
            var strip = look < 0 ? headLeft : headRight;
            int width = strip.Width / turnFrames;
            g.DrawImage(strip, headPlane, new Rectangle((pose - 1) * width, 0, width, strip.Height), GraphicsUnit.Pixel);
        }
        g.DrawImage(seat, new RectangleF(316, 337, 168, 163));
    }

    private void DrawHand(Graphics g, PointF wrist, double t, bool right, double holding)
    {
        var state = g.Save();
        g.TranslateTransform(wrist.X, wrist.Y);
        g.RotateTransform(right ? (float)(-12 - 48 * holding) : 13);
        if (!right) g.ScaleTransform(-1, 1);
        float height = 19 + (float)((1 - holding) * 1.6 * Math.Sin(t * 17));
        g.DrawImage(hand, new RectangleF(-9, 3 - height, 18, height));
        g.Restore(state);
    }

    private static void DrawGlass(Graphics g, float x, float y, float tip)
    {
        var state = g.Save();
        g.TranslateTransform(x, y);
        g.RotateTransform(-32 * tip);
        using var wall = new LinearGradientBrush(new RectangleF(-12, -15, 24, 32),
            Color.FromArgb(72, 211, 247, 255), Color.FromArgb(20, 130, 165, 178), 6f);
        PointF[] outline = [new(-12, -13), new(12, -13), new(10, 14), new(8, 16), new(-8, 16), new(-10, 14)];
        g.FillPolygon(wall, outline);
        using var cola = new LinearGradientBrush(new RectangleF(-11, -6, 22, 21), Rgb(0x955132), Rgb(0x321d22), 68f);
        g.FillPolygon(cola, new PointF[] { new(-10.7f, -tip * 6), new(10.7f, tip * 6), new(9, 12), new(-9, 12) });
        Stroke(g, Rgb(0xde9c5a), 1, new(-10, -tip * 6), new(10, tip * 6));
        using var edge = new Pen(Color.FromArgb(185, 198, 224, 230), 1);
        g.DrawLines(edge, outline[1..]);
        g.DrawLine(edge, outline[^1], outline[0]);
        g.DrawEllipse(edge, -12, -15, 24, 4);
        Stroke(g, Color.FromArgb(165, 238, 246, 245), 1.3f, new(-9, -10), new(-7.8f, 8));
        g.Restore(state);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            timer.Dispose();
            music.Stop();
            music.Dispose();
            theme.Dispose();
            bridge.Dispose();
            head.Dispose();
            headLeft.Dispose();
            headRight.Dispose();
            shirt.Dispose();
            seat.Dispose();
            arm.Dispose();
            hand.Dispose();
            display.Dispose();
            code.Dispose();
            textFormat.Dispose();
        }
        base.Dispose(disposing);
    }
}
