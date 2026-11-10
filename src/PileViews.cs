using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace FtnEditor;

public static class PileRender
{
    static readonly (double t, Color c)[] Ramp = { (0, Ui.C("#4A3213")), (0.3, Ui.C("#96681F")), (0.62, Ui.C("#D9A53C")), (0.85, Ui.C("#F4CF6B")), (1, Ui.C("#FFF0B8")) };
    static Color RampAt(double t)
    {
        t = Math.Clamp(t, 0, 1);
        for (int i = 1; i < Ramp.Length; i++)
            if (t <= Ramp[i].t)
            {
                double f = (t - Ramp[i - 1].t) / (Ramp[i].t - Ramp[i - 1].t); Color a = Ramp[i - 1].c, b = Ramp[i].c;
                return Color.FromRgb((byte)(a.R + (b.R - a.R) * f), (byte)(a.G + (b.G - a.G) * f), (byte)(a.B + (b.B - a.B) * f));
            }
        return Ramp[^1].c;
    }

    public static float MaxHeight(SaveGame s) => Math.Max(0.5f, Math.Max(s.Heights?.Length > 0 ? s.Heights.Max() : 0, s.Dome?.Length > 0 ? s.Dome.Max() : 0));

    /// <summary>Height map as a shaded image: hay colour ramp, hill shading, 1 m contour lines, footprint of what was dug away.</summary>
    public static BitmapSource MakeBitmap(SaveGame s, bool radar)
    {
        int n = s.Grid; var h = s.Heights; var dome = s.Dome;
        if (h == null || n < 2) return null;
        float max = MaxHeight(s);
        byte[] px = new byte[n * n * 4];
        for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                int i = z * n + x, o = i * 4; float v = h[i];
                if (v < 0.02f)
                {
                    if (dome != null && i < dome.Length && dome[i] > 0.05f) { byte a = 46; px[o] = (byte)(radar ? 30 * a / 255 : 26 * a / 255); px[o + 1] = (byte)(radar ? 120 * a / 255 : 96 * a / 255); px[o + 2] = (byte)(radar ? 40 * a / 255 : 150 * a / 255); px[o + 3] = a; }
                    continue;
                }
                float dx = h[z * n + Math.Min(n - 1, x + 1)] - h[z * n + Math.Max(0, x - 1)], dz = h[Math.Min(n - 1, z + 1) * n + x] - h[Math.Max(0, z - 1) * n + x];
                double nx = -dx, ny = 0.5, nz = -dz, len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                double lum = 0.42 + 0.72 * Math.Max(0, (nx * -0.5 + ny * 0.7 + nz * -0.5) / (len * 0.995));
                bool contour = (x + 1 < n && (int)h[i + 1] != (int)v) || (z + 1 < n && (int)h[i + n] != (int)v);
                if (contour) lum *= 0.7;
                Color c = RampAt(v / max);
                double r = c.R * lum, g = c.G * lum, b = c.B * lum;
                if (radar) { double l = (r * 0.3 + g * 0.6 + b * 0.1); r = l * 0.12; g = l * 0.95; b = l * 0.5; }
                px[o] = (byte)Math.Min(255, b); px[o + 1] = (byte)Math.Min(255, g); px[o + 2] = (byte)Math.Min(255, r); px[o + 3] = 255;
            }
        var bmp = BitmapSource.Create(n, n, 96, 96, PixelFormats.Pbgra32, null, px, n * 4);
        bmp.Freeze();
        return bmp;
    }

    public static (double fx, double fz, double x, double y, double z) Player(SaveGame s)
    {
        var p = s.Root["player"]?.Floats;
        if (p == null || p.Length < 12) return (0, -1, 0, 0, 0);
        double fx = -p[2], fz = -p[8], l = Math.Sqrt(fx * fx + fz * fz);
        return l < 1e-6 ? (0, -1, p[9], p[10], p[11]) : (fx / l, fz / l, p[9], p[10], p[11]);
    }
}

// =====================================================================================  2D top view + radar
public sealed class TopView : FrameworkElement
{
    public SaveGame S;
    public int Selected = -1;
    public bool ShowBuildings = true, ShowBelts = true, ShowProps = true;
    public event Action<int> NeedleClicked;
    public event Action<string> Hover;

    bool radar; BitmapSource bmp;
    double scale = 14; Point center; double sweep;
    Point dragFrom; Point dragCenter; bool dragging, moved;
    readonly DispatcherTimer timer = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) }; // 30 fps, radar only
    static readonly Typeface Face = new(new FontFamily("Cascadia Mono, Consolas"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public TopView()
    {
        ClipToBounds = true; Focusable = false;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.Linear);
        timer.Tick += (_, _) => { sweep = (sweep + 0.045) % (Math.PI * 2); InvalidateVisual(); };
        IsVisibleChanged += (_, _) => UpdateTimer();
        Unloaded += (_, _) => timer.Stop();
        SizeChanged += (_, e) => { if (e.PreviousSize.Width < 1) Fit(); };
    }

    public bool Radar { get => radar; set { bool ch = radar != value; radar = value; Rebuild(); UpdateTimer(); if (ch) Fit(); } }
    void UpdateTimer() { if (radar && IsVisible) timer.Start(); else timer.Stop(); }
    public void Stop() => timer.Stop();
    public void Rebuild() { bmp = S == null ? null : PileRender.MakeBitmap(S, radar); InvalidateVisual(); }
    public void Fit(bool everything = false)
    {
        if (S == null || ActualWidth < 10) return;
        double ext = S.Extent + 2; center = new Point(0, 0);
        if (radar && !everything)
        {
            // radar sweeps from the player: keep both the player and the pile in view
            var pp = PileRender.Player(S);
            double x0 = Math.Min(-ext, pp.x - 3), x1 = Math.Max(ext, pp.x + 3), z0 = Math.Min(-ext, pp.z - 3), z1 = Math.Max(ext, pp.z + 3);
            center = new Point((x0 + x1) / 2, (z0 + z1) / 2); scale = Math.Min(ActualWidth / (x1 - x0), ActualHeight / (z1 - z0));
            InvalidateVisual(); return;
        }
        if (everything)
        {
            double minX = -ext, maxX = ext, minZ = -ext, maxZ = ext;
            foreach (var b in S.Buildings) foreach (var k in new[] { "position", "a", "b" }) if (b[k]?.Floats is { Length: 3 } p) { minX = Math.Min(minX, p[0]); maxX = Math.Max(maxX, p[0]); minZ = Math.Min(minZ, p[2]); maxZ = Math.Max(maxZ, p[2]); }
            var pl = PileRender.Player(S); minX = Math.Min(minX, pl.x); maxX = Math.Max(maxX, pl.x); minZ = Math.Min(minZ, pl.z); maxZ = Math.Max(maxZ, pl.z);
            center = new Point((minX + maxX) / 2, (minZ + maxZ) / 2);
            scale = Math.Min(ActualWidth / (maxX - minX + 6), ActualHeight / (maxZ - minZ + 6));
        }
        else scale = Math.Min(ActualWidth, ActualHeight) / (ext * 2);
        InvalidateVisual();
    }

    Point W2S(double x, double z) => new(ActualWidth / 2 + (x - center.X) * scale, ActualHeight / 2 + (z - center.Y) * scale);
    Point S2W(Point p) => new((p.X - ActualWidth / 2) / scale + center.X, (p.Y - ActualHeight / 2) / scale + center.Y);
    static Pen P(Color c, double w, double a = 1) { var p = new Pen(new SolidColorBrush(Color.FromArgb((byte)(a * 255), c.R, c.G, c.B)), w); p.Freeze(); return p; }
    static SolidColorBrush B(Color c, double a = 1) { var b = new SolidColorBrush(Color.FromArgb((byte)(a * 255), c.R, c.G, c.B)); b.Freeze(); return b; }
    FormattedText Txt(string s, double size, Brush b) => new(s, Inv, FlowDirection.LeftToRight, Face, size, b, VisualTreeHelper.GetDpi(this).PixelsPerDip);

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, hgt = ActualHeight;
        Color accent = radar ? Ui.C("#52F2A0") : Ui.C("#F2B544");
        dc.DrawRectangle(B(radar ? Ui.C("#050D09") : Ui.C("#0A0B0E")), null, new Rect(0, 0, w, hgt));
        if (S == null) return;

        // floor grid
        double step = scale * 5 >= 26 ? 5 : scale * 10 >= 26 ? 10 : 50;
        var gridPen = P(radar ? Ui.C("#1E5A3C") : Ui.C("#FFFFFF"), 1, radar ? 0.35 : 0.045);
        Point tl = S2W(new Point(0, 0)), br = S2W(new Point(w, hgt));
        for (double x = Math.Floor(tl.X / step) * step; x <= br.X; x += step) { double sx = Math.Round(W2S(x, 0).X) + 0.5; dc.DrawLine(gridPen, new Point(sx, 0), new Point(sx, hgt)); }
        for (double z = Math.Floor(tl.Y / step) * step; z <= br.Y; z += step) { double sz = Math.Round(W2S(0, z).Y) + 0.5; dc.DrawLine(gridPen, new Point(0, sz), new Point(w, sz)); }

        // pile
        double ext = S.Extent, half = S.Cell / 2;
        var pileRect = new Rect(W2S(-ext - half, -ext - half), W2S(ext + half, ext + half));
        var dash = new Pen(B(accent, 0.22), 1) { DashStyle = new DashStyle(new double[] { 4, 5 }, 0) };
        dc.DrawRectangle(null, dash, new Rect(W2S(-ext, -ext), W2S(ext, ext)));
        if (bmp != null) dc.DrawImage(bmp, pileRect);

        // buildings
        foreach (var b in S.Buildings)
        {
            string type = b["type"]?.Str; var info = GameData.Building(type); Color c = radar ? Ui.C("#2FA36B") : Ui.C(info.Color);
            if (b["a"]?.Floats is { Length: 3 } a && b["b"]?.Floats is { Length: 3 } bb)
            {
                if (ShowBelts) dc.DrawLine(P(c, Math.Max(1.5, scale * 0.42), radar ? 0.5 : 0.75), W2S(a[0], a[2]), W2S(bb[0], bb[2]));
            }
            else if (ShowBuildings && b["position"]?.Floats is { Length: 3 } p)
            {
                Point sp = W2S(p[0], p[2]); double r = Math.Max(2.5, scale * (type == "power_pole" ? 0.22 : 0.48));
                bool off = b["off"]?.Bool == true;
                if (type == "power_pole") dc.DrawEllipse(B(c, 0.9), null, sp, r, r);
                else
                {
                    dc.PushTransform(new RotateTransform(-(b["yaw"]?.Num ?? 0) * 180 / Math.PI, sp.X, sp.Y));
                    dc.DrawRoundedRectangle(B(c, off ? 0.15 : 0.85), P(c, 1, off ? 0.7 : 0), new Rect(sp.X - r, sp.Y - r, r * 2, r * 2), r * 0.3, r * 0.3);
                    dc.Pop();
                }
            }
        }
        if (ShowProps)
            foreach (var pr in S.Props) if (pr["xform"]?.Floats is { Length: 12 } x) dc.DrawEllipse(B(radar ? Ui.C("#2FA36B") : Ui.C("#D8D2C4"), 0.75), null, W2S(x[9], x[11]), Math.Max(1.5, scale * 0.14), Math.Max(1.5, scale * 0.14));

        // player
        var pl = PileRender.Player(S); Point pp = W2S(pl.x, pl.z);
        if (radar)
        {
            double maxR = Math.Sqrt(w * w + hgt * hgt);
            var ring = P(accent, 1, 0.22);
            for (double r = 5; r * scale < maxR; r += 5)
            {
                dc.DrawEllipse(null, ring, pp, r * scale, r * scale);
                if (r % 10 == 0) dc.DrawText(Txt($"{r:0} m", 9.5, B(accent, 0.5)), new Point(pp.X + 4, pp.Y - r * scale - 13));
            }
            // sweep: fading wedge behind the beam
            const int slices = 28; double span = 0.9;
            for (int i = 0; i < slices; i++)
            {
                double a0 = sweep - span * i / slices, a1 = sweep - span * (i + 1) / slices;
                var g = new StreamGeometry();
                using (var ctx = g.Open())
                {
                    ctx.BeginFigure(pp, true, true);
                    ctx.LineTo(new Point(pp.X + Math.Cos(a0) * maxR, pp.Y + Math.Sin(a0) * maxR), false, false);
                    ctx.LineTo(new Point(pp.X + Math.Cos(a1) * maxR, pp.Y + Math.Sin(a1) * maxR), false, false);
                }
                g.Freeze();
                dc.DrawGeometry(B(accent, 0.20 * Math.Pow(1 - (double)i / slices, 1.6)), null, g);
            }
            dc.DrawLine(P(accent, 1.6, 0.9), pp, new Point(pp.X + Math.Cos(sweep) * maxR, pp.Y + Math.Sin(sweep) * maxR));
        }
        var tri = new StreamGeometry();
        using (var ctx = tri.Open())
        {
            double r = Math.Max(7, scale * 0.5); double ang = Math.Atan2(pl.fz, pl.fx);
            Point Pt(double a, double d) => new(pp.X + Math.Cos(ang + a) * d, pp.Y + Math.Sin(ang + a) * d);
            ctx.BeginFigure(Pt(0, r * 1.25), true, true); ctx.LineTo(Pt(2.45, r), true, false); ctx.LineTo(Pt(Math.PI, r * 0.35), true, false); ctx.LineTo(Pt(-2.45, r), true, false);
        }
        tri.Freeze();
        dc.DrawGeometry(B(Ui.C("#FFFFFF")), P(Ui.C("#0A0B0E"), 1.5), tri);

        // needles
        int n = NeedleOps.Count(S);
        for (int i = 0; i < n; i++)
        {
            var (x, y, z) = NeedleOps.Pos(S, i); Point sp = W2S(x, z);
            bool found = NeedleOps.IsFound(S, i); Color c = GameData.NeedleColor(NeedleOps.TypeOf(S, i));
            double vis = 1;
            if (radar)
            {
                c = accent;
                double bearing = Math.Atan2(sp.Y - pp.Y, sp.X - pp.X); double lag = ((sweep - bearing) % (Math.PI * 2) + Math.PI * 2) % (Math.PI * 2);
                vis = found ? 0.18 : Math.Max(0.12, Math.Exp(-lag * 0.75));
                if (!found && lag < 0.5) dc.DrawEllipse(null, P(c, 1.5, 1 - lag * 2), sp, 8 + lag * 46, 8 + lag * 46);
            }
            if (found && !radar) { dc.DrawEllipse(null, P(Ui.C("#7C808B"), 1.5), sp, 6, 6); dc.DrawLine(P(Ui.C("#7C808B"), 1.5), new Point(sp.X - 3, sp.Y), new Point(sp.X + 3, sp.Y)); }
            else
            {
                var glow = new RadialGradientBrush(Color.FromArgb((byte)(150 * vis), c.R, c.G, c.B), Color.FromArgb(0, c.R, c.G, c.B)); glow.Freeze();
                dc.DrawEllipse(glow, null, sp, 22, 22);
                dc.PushTransform(new RotateTransform(45, sp.X, sp.Y));
                dc.DrawRectangle(B(c, vis), P(Ui.C("#0A0B0E"), 1.5), new Rect(sp.X - 5.5, sp.Y - 5.5, 11, 11));
                dc.Pop();
            }
            if (i == Selected) dc.DrawEllipse(null, P(Ui.C("#FFFFFF"), 1.5, 0.9), sp, 14, 14);
            double depth = NeedleOps.Depth(S, i);
            string label = radar ? $"{i + 1} · {Math.Sqrt(Math.Pow(x - pl.x, 2) + Math.Pow(z - pl.z, 2)):0.0} m" : $"{i + 1}" + (found ? "" : depth <= 0.02 ? " · frei" : $" · {depth:0.0} m tief");
            var ft = Txt(label, 10.5, B(radar ? accent : Ui.C("#FFFFFF"), radar ? Math.Max(0.35, vis) : found ? 0.45 : 0.95));
            dc.DrawRoundedRectangle(B(Ui.C("#0A0B0E"), 0.72), null, new Rect(sp.X + 12, sp.Y - 9, ft.Width + 10, 18), 4, 4);
            dc.DrawText(ft, new Point(sp.X + 17, sp.Y - ft.Height / 2));
        }

        // scale bar
        double barM = scale * 10 < 160 ? 10 : scale * 5 < 160 ? 5 : 1;
        dc.DrawLine(P(Ui.C("#FFFFFF"), 2, 0.6), new Point(16, hgt - 18), new Point(16 + barM * scale, hgt - 18));
        dc.DrawText(Txt($"{barM:0} m", 10, B(Ui.C("#FFFFFF"), 0.6)), new Point(16, hgt - 36));
        if (radar) dc.DrawText(Txt($"RADAR · {Enumerable.Range(0, n).Count(i => !NeedleOps.IsFound(S, i))} SIGNALE", 11, B(accent, 0.85)), new Point(16, 58));
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        Point m = e.GetPosition(this), before = S2W(m);
        scale = Math.Clamp(scale * (e.Delta > 0 ? 1.18 : 1 / 1.18), 2, 220);
        Point after = S2W(m); center = new Point(center.X + before.X - after.X, center.Y + before.Y - after.Y);
        InvalidateVisual();
    }
    protected override void OnMouseDown(MouseButtonEventArgs e) { dragFrom = e.GetPosition(this); dragCenter = center; dragging = true; moved = false; CaptureMouse(); }
    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        ReleaseMouseCapture(); dragging = false;
        if (moved || S == null) return;
        Point m = e.GetPosition(this); int best = -1; double bd = 18;
        for (int i = 0; i < NeedleOps.Count(S); i++) { var (x, _, z) = NeedleOps.Pos(S, i); double d = (W2S(x, z) - m).Length; if (d < bd) { bd = d; best = i; } }
        NeedleClicked?.Invoke(best);
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        Point m = e.GetPosition(this);
        if (dragging)
        {
            if ((m - dragFrom).Length > 3) moved = true;
            center = new Point(dragCenter.X - (m.X - dragFrom.X) / scale, dragCenter.Y - (m.Y - dragFrom.Y) / scale); InvalidateVisual();
        }
        if (S == null) return;
        Point wp = S2W(m); bool inside = Math.Abs(wp.X) <= S.Extent && Math.Abs(wp.Y) <= S.Extent;
        Hover?.Invoke($"X {wp.X:0.0}   Z {wp.Y:0.0}" + (inside ? $"   Heu {S.HeightAt(wp.X, wp.Y):0.00} m" : ""));
    }
}

// =====================================================================================  3D view
public sealed class Pile3D : Grid
{
    readonly Viewport3D vp = new() { ClipToBounds = true };
    readonly PerspectiveCamera cam = new() { FieldOfView = 45, NearPlaneDistance = 0.2, FarPlaneDistance = 3000, UpDirection = new Vector3D(0, 1, 0) };
    readonly ModelVisual3D visual = new();
    readonly Dictionary<Model3D, int> needleModels = new();
    double yaw = 0.7, pitch = 0.62, dist = 40; Point3D target;
    Point last; bool moved; bool camInit;
    public event Action<int> NeedleClicked;

    public Pile3D()
    {
        Background = Ui.Hex("#0A0B0E"); ClipToBounds = true;
        vp.Camera = cam; vp.Children.Add(visual);
        RenderOptions.SetEdgeMode(vp, EdgeMode.Unspecified);
        Children.Add(vp);
    }

    void UpdateCamera()
    {
        pitch = Math.Clamp(pitch, 0.04, 1.54); dist = Math.Clamp(dist, 2, 600);
        var off = new Vector3D(Math.Cos(pitch) * Math.Sin(yaw), Math.Sin(pitch), Math.Cos(pitch) * Math.Cos(yaw)) * dist;
        cam.Position = target + off; cam.LookDirection = -off;
    }
    public void ResetCamera(SaveGame s)
    {
        // Fit the pile's bounding sphere into the narrower of the two view angles (WPF's FieldOfView is horizontal).
        double h = Math.Max(1, PileRender.MaxHeight(s)), r = Math.Sqrt(s.Extent * s.Extent + h * h / 4);
        double aspect = ActualWidth > 50 && ActualHeight > 50 ? ActualWidth / ActualHeight : 1.4;
        double hHalf = cam.FieldOfView * Math.PI / 360, vHalf = Math.Atan(Math.Tan(hHalf) / aspect);
        target = new Point3D(0, h * 0.5, 0); dist = r / Math.Sin(Math.Min(hHalf, vHalf)) * 1.08;
        yaw = 0.7; pitch = h > s.Extent * 1.5 ? 0.42 : 0.62; camInit = true; UpdateCamera();
    }

    static Material Mat(Color c, double a = 1, bool glow = false)
    {
        var b = new SolidColorBrush(c) { Opacity = a }; b.Freeze();
        if (!glow) { var m = new DiffuseMaterial(b); m.Freeze(); return m; }
        var g = new MaterialGroup(); g.Children.Add(new DiffuseMaterial(new SolidColorBrush(Color.FromArgb((byte)(a * 255), 0, 0, 0)))); g.Children.Add(new EmissiveMaterial(b)); g.Freeze(); return g;
    }
    static void Quad(MeshGeometry3D m, Point3D a, Point3D b, Point3D c, Point3D d)
    {
        int i = m.Positions.Count; m.Positions.Add(a); m.Positions.Add(b); m.Positions.Add(c); m.Positions.Add(d);
        m.TriangleIndices.Add(i); m.TriangleIndices.Add(i + 1); m.TriangleIndices.Add(i + 2); m.TriangleIndices.Add(i); m.TriangleIndices.Add(i + 2); m.TriangleIndices.Add(i + 3);
    }
    /// <summary>Box from a centre and three half-axis vectors.</summary>
    static void Box(MeshGeometry3D m, Point3D c, Vector3D u, Vector3D v, Vector3D w)
    {
        Point3D P(int su, int sv, int sw) => c + u * su + v * sv + w * sw;
        Quad(m, P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), P(-1, 1, -1)); Quad(m, P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1));
        Quad(m, P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1)); Quad(m, P(1, -1, -1), P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1));
        Quad(m, P(1, -1, 1), P(1, -1, -1), P(1, 1, -1), P(1, 1, 1)); Quad(m, P(-1, -1, -1), P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1));
    }
    static MeshGeometry3D Sphere(Point3D c, double r, int seg = 14)
    {
        var m = new MeshGeometry3D();
        for (int la = 0; la <= seg; la++)
            for (int lo = 0; lo <= seg; lo++)
            {
                double t = Math.PI * la / seg, p = 2 * Math.PI * lo / seg;
                m.Positions.Add(new Point3D(c.X + r * Math.Sin(t) * Math.Cos(p), c.Y + r * Math.Cos(t), c.Z + r * Math.Sin(t) * Math.Sin(p)));
            }
        for (int la = 0; la < seg; la++)
            for (int lo = 0; lo < seg; lo++)
            {
                int a = la * (seg + 1) + lo, b = a + seg + 1;
                m.TriangleIndices.Add(a); m.TriangleIndices.Add(a + 1); m.TriangleIndices.Add(b); m.TriangleIndices.Add(a + 1); m.TriangleIndices.Add(b + 1); m.TriangleIndices.Add(b);
            }
        return m;
    }
    static GeometryModel3D Model(MeshGeometry3D mesh, Material mat, bool twoSided = true) { mesh.Freeze(); var g = new GeometryModel3D(mesh, mat); if (twoSided) g.BackMaterial = mat; return g; }

    static MeshGeometry3D HeightMesh(SaveGame s, float[] h, double lift = 0)
    {
        int n = s.Grid, stepN = n > 220 ? 2 : 1; var m = new MeshGeometry3D();
        int cols = (n - 1) / stepN + 1; var index = new int[cols * cols];
        var pos = new Point3DCollection(cols * cols); var uv = new PointCollection(cols * cols);
        for (int j = 0; j < cols; j++)
            for (int i = 0; i < cols; i++)
            {
                int gx = Math.Min(n - 1, i * stepN), gz = Math.Min(n - 1, j * stepN);
                pos.Add(new Point3D(s.WorldOf(gx), h[gz * n + gx] + lift, s.WorldOf(gz))); uv.Add(new Point((gx + 0.5) / n, (gz + 0.5) / n));
            }
        var idx = new Int32Collection();
        for (int j = 0; j < cols - 1; j++)
            for (int i = 0; i < cols - 1; i++)
            {
                int a = j * cols + i, b = a + 1, c = a + cols, d = c + 1;
                if (pos[a].Y - lift < 0.02 && pos[b].Y - lift < 0.02 && pos[c].Y - lift < 0.02 && pos[d].Y - lift < 0.02) continue;
                idx.Add(a); idx.Add(c); idx.Add(b); idx.Add(b); idx.Add(c); idx.Add(d);
            }
        m.Positions = pos; m.TextureCoordinates = uv; m.TriangleIndices = idx;
        return m;
    }

    public void Build(SaveGame s, bool xray, bool showBuildings, bool showBelts, bool showProps, int selected)
    {
        needleModels.Clear();
        var group = new Model3DGroup();
        group.Children.Add(new AmbientLight(Ui.C(xray ? "#9A9A9A" : "#6E6E6E")));
        group.Children.Add(new DirectionalLight(Ui.C("#D8D2C4"), new Vector3D(-0.45, -1, -0.55)));
        float maxH = PileRender.MaxHeight(s); double ext = s.Extent;

        // floor with 5 m grid
        double far = ext + 8;
        foreach (var b in s.Buildings) foreach (var k in new[] { "position", "a", "b" }) if (b[k]?.Floats is { Length: 3 } p) far = Math.Max(far, Math.Max(Math.Abs(p[0]), Math.Abs(p[2])) + 6);
        var tile = new DrawingGroup();
        using (var dc = tile.Open()) { dc.DrawRectangle(Ui.Hex("#101216"), null, new Rect(0, 0, 1, 1)); dc.DrawRectangle(Ui.Hex("#1C1F27"), null, new Rect(0, 0, 1, 0.02)); dc.DrawRectangle(Ui.Hex("#1C1F27"), null, new Rect(0, 0, 0.02, 1)); }
        var floorBrush = new DrawingBrush(tile) { TileMode = TileMode.Tile, Viewport = new Rect(0, 0, 1, 1), ViewportUnits = BrushMappingMode.Absolute };
        var floor = new MeshGeometry3D();
        Quad(floor, new Point3D(-far, -0.02, -far), new Point3D(-far, -0.02, far), new Point3D(far, -0.02, far), new Point3D(far, -0.02, -far));
        foreach (var p in floor.Positions) floor.TextureCoordinates.Add(new Point(p.X / 5, p.Z / 5));
        group.Children.Add(Model(floor, new DiffuseMaterial(floorBrush)));

        // buildings, batched per colour
        var batches = new Dictionary<string, MeshGeometry3D>();
        MeshGeometry3D Batch(string color) => batches.TryGetValue(color, out var m) ? m : batches[color] = new MeshGeometry3D();
        foreach (var b in s.Buildings)
        {
            string type = b["type"]?.Str; string color = GameData.Building(type).Color;
            if (b["a"]?.Floats is { Length: 3 } a && b["b"]?.Floats is { Length: 3 } bb)
            {
                if (!showBelts) continue;
                var pa = new Point3D(a[0], a[1], a[2]); var pb = new Point3D(bb[0], bb[1], bb[2]); var dir = pb - pa; double len = dir.Length; if (len < 0.01) continue; dir /= len;
                var side = Vector3D.CrossProduct(dir, new Vector3D(0, 1, 0)); if (side.Length < 1e-4) side = new Vector3D(1, 0, 0); side.Normalize();
                var up = Vector3D.CrossProduct(side, dir);
                Box(Batch(color), pa + dir * (len / 2), dir * (len / 2), up * 0.05, side * 0.24);
            }
            else if (showBuildings && b["position"]?.Floats is { Length: 3 } p)
            {
                double yawB = b["yaw"]?.Num ?? 0; bool pole = type == "power_pole";
                double hw = pole ? 0.09 : 0.46, hh = pole ? 1.6 : type is "hay_silo" ? 1.3 : 0.45;
                var u = new Vector3D(Math.Cos(yawB), 0, -Math.Sin(yawB)) * hw; var w = new Vector3D(Math.Sin(yawB), 0, Math.Cos(yawB)) * hw;
                Box(Batch(b["off"]?.Bool == true ? "#4A4E59" : color), new Point3D(p[0], p[1] + hh, p[2]), u, new Vector3D(0, hh, 0), w);
            }
        }
        if (showProps)
            foreach (var pr in s.Props) if (pr["xform"]?.Floats is { Length: 12 } x) Box(Batch("#D8D2C4"), new Point3D(x[9], x[10] + 0.12, x[11]), new Vector3D(0.14, 0, 0), new Vector3D(0, 0.12, 0), new Vector3D(0, 0, 0.14));
        foreach (var kv in batches) group.Children.Add(Model(kv.Value, Mat(Ui.C(kv.Key))));

        // player marker
        var pl = PileRender.Player(s); var pm = new MeshGeometry3D();
        Box(pm, new Point3D(pl.x, pl.y + 0.9, pl.z), new Vector3D(0.2, 0, 0), new Vector3D(0, 0.9, 0), new Vector3D(0, 0, 0.2));
        Box(pm, new Point3D(pl.x + pl.fx * 0.55, pl.y + 1.5, pl.z + pl.fz * 0.55), new Vector3D(pl.fx * 0.35, 0, pl.fz * 0.35), new Vector3D(0, 0.06, 0), new Vector3D(-pl.fz * 0.06, 0, pl.fx * 0.06));
        group.Children.Add(Model(pm, Mat(Colors.White, 1, true)));

        // needles: glowing bead at the real position + a marker beam through the hay
        int n = NeedleOps.Count(s);
        for (int i = 0; i < n; i++)
        {
            var (x, y, z) = NeedleOps.Pos(s, i); bool found = NeedleOps.IsFound(s, i);
            Color c = found ? Ui.C("#6C707B") : GameData.NeedleColor(NeedleOps.TypeOf(s, i));
            double r = (i == selected ? 0.34 : 0.22) * Math.Max(1, ext / 16);
            var bead = Model(Sphere(new Point3D(x, y, z), r), Mat(c, 1, true));
            needleModels[bead] = i; group.Children.Add(bead);
            if (!found)
            {
                var beam = new MeshGeometry3D(); double top = maxH + 3, bw = (i == selected ? 0.07 : 0.035) * Math.Max(1, ext / 16);
                Box(beam, new Point3D(x, (y + top) / 2, z), new Vector3D(bw, 0, 0), new Vector3D(0, (top - y) / 2, 0), new Vector3D(0, 0, bw));
                var bm = Model(beam, Mat(c, i == selected ? 0.95 : 0.55, true)); needleModels[bm] = i; group.Children.Add(bm);
                var cap = Model(Sphere(new Point3D(x, top, z), r * 0.7, 10), Mat(c, 1, true)); needleModels[cap] = i; group.Children.Add(cap);
            }
        }

        // the pile itself – last, so everything shows through it in x-ray mode
        var bmp = PileRender.MakeBitmap(s, false);
        if (bmp != null)
        {
            var brush = new ImageBrush(bmp) { ViewportUnits = BrushMappingMode.Absolute, Viewport = new Rect(0, 0, 1, 1), Opacity = xray ? 0.34 : 1 };
            RenderOptions.SetBitmapScalingMode(brush, BitmapScalingMode.Linear);
            Material mat = new DiffuseMaterial(brush);
            if (xray) { var mg = new MaterialGroup(); mg.Children.Add(mat); mg.Children.Add(new EmissiveMaterial(new SolidColorBrush(Ui.C("#F2B544")) { Opacity = 0.05 })); mat = mg; }
            group.Children.Add(Model(HeightMesh(s, s.Heights), mat));
            if (xray && s.Dome != null)
            {
                // faint shell of the original pile, drawn after the hay so it never hides it
                var ghost = new SolidColorBrush(Ui.C("#5CC8E8")) { Opacity = 0.06 };
                group.Children.Add(Model(HeightMesh(s, s.Dome, 0.03), new DiffuseMaterial(ghost)));
            }
        }
        visual.Content = group;
        if (!camInit) ResetCamera(s);
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e) { dist *= e.Delta > 0 ? 1 / 1.15 : 1.15; UpdateCamera(); }
    protected override void OnMouseDown(MouseButtonEventArgs e) { last = e.GetPosition(this); moved = false; CaptureMouse(); }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (!IsMouseCaptured) return;
        Point m = e.GetPosition(this); Vector d = m - last; if (d.Length < 0.5) return; last = m; moved = true;
        if (e.RightButton == MouseButtonState.Pressed || e.MiddleButton == MouseButtonState.Pressed)
        {
            var look = cam.LookDirection; look.Normalize(); var right = Vector3D.CrossProduct(look, cam.UpDirection); right.Normalize(); var up = Vector3D.CrossProduct(right, look);
            target += (-right * d.X + up * d.Y) * dist * 0.0016;
        }
        else { yaw -= d.X * 0.008; pitch += d.Y * 0.008; }
        UpdateCamera();
    }
    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        ReleaseMouseCapture();
        if (moved || e.ChangedButton != MouseButton.Left) return;
        int hit = -1;
        VisualTreeHelper.HitTest(vp, null, r => { if (r is RayMeshGeometry3DHitTestResult h && needleModels.TryGetValue(h.ModelHit, out int i)) { hit = i; return HitTestResultBehavior.Stop; } return HitTestResultBehavior.Continue; }, new PointHitTestParameters(e.GetPosition(vp)));
        NeedleClicked?.Invoke(hit);
    }
}
