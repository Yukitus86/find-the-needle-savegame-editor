using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace FtnEditor;

/// <summary>Small factory helpers so the pages can be composed in code.</summary>
public static class Ui
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    public static Brush R(string key) => (Brush)Application.Current.Resources[key];
    public static FontFamily Font(string key) => (FontFamily)Application.Current.Resources[key];
    public static Brush Bg0 => R("Bg0"); public static Brush Bg1 => R("Bg1"); public static Brush Bg2 => R("Bg2"); public static Brush Bg3 => R("Bg3"); public static Brush Bg4 => R("Bg4");
    public static Brush Line => R("Line"); public static Brush Text => R("Text"); public static Brush Dim => R("Dim"); public static Brush Faint => R("Faint");
    public static Brush Hay => R("Hay"); public static Brush Steel => R("Steel"); public static Brush Good => R("Good"); public static Brush Bad => R("Bad");

    public static Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex);
    public static SolidColorBrush Hex(string hex, double opacity = 1)
    {
        var b = new SolidColorBrush(C(hex)) { Opacity = opacity }; b.Freeze(); return b;
    }
    public static SolidColorBrush Alpha(Brush b, double a)
    {
        var c = ((SolidColorBrush)b).Color; var r = new SolidColorBrush(Color.FromArgb((byte)(a * 255), c.R, c.G, c.B)); r.Freeze(); return r;
    }

    // ---------- formatting ----------
    public static string Num(double v, int decimals = 0)
    {
        if (double.IsNaN(v) || double.IsInfinity(v)) return "–";
        string s = v.ToString(decimals == 0 ? "#,0" : "#,0." + new string('#', decimals), Inv);
        return s.Replace(',', ' ');
    }
    public static string Short(double v)
    {
        double a = Math.Abs(v);
        if (a >= 1e9) return (v / 1e9).ToString("0.##", Inv) + " Mrd";
        if (a >= 1e6) return (v / 1e6).ToString("0.##", Inv) + " Mio";
        if (a >= 1e4) return (v / 1e3).ToString("0.#", Inv) + " k";
        return Num(v, a < 100 && v != Math.Floor(v) ? 2 : 0);
    }
    public static string Time(double secs)
    {
        if (secs < 0) return "–";
        var t = TimeSpan.FromSeconds(secs);
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours}h {t.Minutes:00}m" : $"{t.Minutes}m {t.Seconds:00}s";
    }
    public static bool TryParse(string s, out double v)
    {
        s = (s ?? "").Trim().Replace(" ", "").Replace(" ", "").Replace("$", "");
        double mul = 1;
        if (s.EndsWith("k", StringComparison.OrdinalIgnoreCase)) { mul = 1e3; s = s[..^1]; }
        else if (s.EndsWith("m", StringComparison.OrdinalIgnoreCase)) { mul = 1e6; s = s[..^1]; }
        if (s.Contains(',') && !s.Contains('.')) s = s.Replace(',', '.'); else s = s.Replace(",", "");
        bool ok = double.TryParse(s, NumberStyles.Float, Inv, out v); v *= mul; return ok;
    }
    public static string Pretty(string id)
    {
        if (string.IsNullOrEmpty(id)) return "–";
        var parts = id.Replace('_', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", parts.Select(p => p.Length <= 3 && p.StartsWith("mk") ? p.ToUpperInvariant() : char.ToUpperInvariant(p[0]) + p[1..]));
    }

    // ---------- text ----------
    public static TextBlock T(string text, double size = 12.5, Brush fg = null, bool bold = false, string font = "UiFont")
        => new() { Text = text, FontSize = size, Foreground = fg ?? Text, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, FontFamily = Font(font), TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
    public static TextBlock Head(string text, double size = 20, Brush fg = null) => T(text, size, fg, true, "HeadFont");
    public static TextBlock Mono(string text, double size = 12.5, Brush fg = null) => T(text, size, fg, false, "MonoFont");
    public static TextBlock Caps(string text, Brush fg = null)
    {
        var t = T(text.ToUpperInvariant(), 10.5, fg ?? Dim, true); TextOptions.SetTextFormattingMode(t, TextFormattingMode.Ideal);
        t.Typography.Capitals = FontCapitals.Normal; return t;
    }
    public static TextBlock Icon(string glyph, double size = 15, Brush fg = null)
        => new() { Text = glyph, FontFamily = Font("IconFont"), FontSize = size, Foreground = fg ?? Dim, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
    public static TextBlock Wrap(this TextBlock t) { t.TextWrapping = TextWrapping.Wrap; t.TextTrimming = TextTrimming.None; return t; }

    // ---------- layout ----------
    public static T M<T>(this T e, double l, double t = 0, double r = 0, double b = 0) where T : FrameworkElement { e.Margin = new Thickness(l, t, r, b); return e; }
    public static T W<T>(this T e, double w) where T : FrameworkElement { e.Width = w; return e; }
    public static T Tip<T>(this T e, string tip) where T : FrameworkElement { e.ToolTip = tip; ToolTipService.SetInitialShowDelay(e, 350); return e; }
    public static T Col<T>(this T e, int c, int span = 1) where T : UIElement { Grid.SetColumn(e, c); if (span > 1) Grid.SetColumnSpan(e, span); return e; }
    public static T Row<T>(this T e, int r, int span = 1) where T : UIElement { Grid.SetRow(e, r); if (span > 1) Grid.SetRowSpan(e, span); return e; }
    public static T DockTo<T>(this T e, System.Windows.Controls.Dock d) where T : UIElement { DockPanel.SetDock(e, d); return e; }
    public static T Right<T>(this T e) where T : FrameworkElement { e.HorizontalAlignment = HorizontalAlignment.Right; return e; }
    public static T Left<T>(this T e) where T : FrameworkElement { e.HorizontalAlignment = HorizontalAlignment.Left; return e; }
    public static T VCenter<T>(this T e) where T : FrameworkElement { e.VerticalAlignment = VerticalAlignment.Center; return e; }

    public static StackPanel H(params UIElement[] kids) { var p = new StackPanel { Orientation = Orientation.Horizontal }; foreach (var k in kids) if (k != null) p.Children.Add(k); return p; }
    public static StackPanel V(params UIElement[] kids) { var p = new StackPanel(); foreach (var k in kids) if (k != null) p.Children.Add(k); return p; }
    public static WrapPanel Flow(params UIElement[] kids) { var p = new WrapPanel(); foreach (var k in kids) if (k != null) p.Children.Add(k); return p; }
    public static Grid Cols(string spec, params UIElement[] kids)
    {
        var g = new Grid();
        foreach (var s in spec.Split(','))
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = s == "*" ? new GridLength(1, GridUnitType.Star) : s == "a" ? GridLength.Auto : s.EndsWith('*') ? new GridLength(double.Parse(s[..^1], Inv), GridUnitType.Star) : new GridLength(double.Parse(s, Inv)) });
        for (int i = 0; i < kids.Length; i++) if (kids[i] != null) { Grid.SetColumn(kids[i], i); g.Children.Add(kids[i]); }
        return g;
    }
    public static Grid Rows(string spec, params UIElement[] kids)
    {
        var g = new Grid();
        foreach (var s in spec.Split(','))
            g.RowDefinitions.Add(new RowDefinition { Height = s == "*" ? new GridLength(1, GridUnitType.Star) : s == "a" ? GridLength.Auto : new GridLength(double.Parse(s, Inv)) });
        for (int i = 0; i < kids.Length; i++) if (kids[i] != null) { Grid.SetRow(kids[i], i); g.Children.Add(kids[i]); }
        return g;
    }

    public static Border Box(UIElement child, Brush bg = null, double radius = 10, double pad = 16, Brush border = null)
        => new() { Child = child, Background = bg ?? Bg2, CornerRadius = new CornerRadius(radius), Padding = new Thickness(pad), BorderBrush = border ?? Line, BorderThickness = new Thickness(1), SnapsToDevicePixels = true };

    /// <summary>Titled card.</summary>
    public static Border Card(string title, UIElement content, UIElement headerRight = null, string icon = null)
    {
        var head = new DockPanel { Margin = new Thickness(0, 0, 0, 12), LastChildFill = false };
        if (headerRight != null) head.Children.Add(((FrameworkElement)headerRight).DockTo(System.Windows.Controls.Dock.Right));
        if (icon != null) head.Children.Add(Icon(icon, 13, Hay).M(0, 0, 8, 0));
        head.Children.Add(Caps(title));
        return Box(V(head, content));
    }

    public static ScrollViewer Scroll(UIElement content, double pad = 26)
        => new() { Content = new Border { Child = content, Padding = new Thickness(pad, 4, pad, pad) }, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false };

    // ---------- controls ----------
    public static Button Btn(string text, Action click, string style = "Btn", string icon = null)
    {
        var b = new Button { Style = (Style)Application.Current.Resources[style] };
        if (icon != null)
        {
            var ic = Icon(icon, 12); ic.SetBinding(TextBlock.ForegroundProperty, new System.Windows.Data.Binding("Foreground") { Source = b });
            var tx = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(text == "" ? 0 : 7, 0, 0, 0) };
            b.Content = H(ic, tx);
        }
        else b.Content = text;
        if (click != null) b.Click += (_, _) => click();
        return b;
    }
    public static CheckBox Switch(string label, bool value, Action<bool> changed)
    {
        var c = new CheckBox { Style = (Style)Application.Current.Resources["Switch"], Content = label, IsChecked = value };
        c.Click += (_, _) => changed(c.IsChecked == true);
        return c;
    }
    public static RadioButton Seg(string text, string group, bool on, Action click, string style = "Seg")
    {
        var r = new RadioButton { Style = (Style)Application.Current.Resources[style], Content = text, GroupName = group, IsChecked = on };
        r.Checked += (_, _) => click();
        return r;
    }
    public static Border SegHost(params UIElement[] segs) => new() { Child = H(segs), Background = Hex("#101216"), CornerRadius = new CornerRadius(8), BorderBrush = Line, BorderThickness = new Thickness(1), Padding = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Left };

    public static Border Pill(string text, Brush color, bool filled = false)
    {
        var t = T(text, 10.5, filled ? Hex("#14100A") : color, true);
        return new Border { Child = t, Background = filled ? color : Alpha(color, 0.13), CornerRadius = new CornerRadius(9), Padding = new Thickness(8, 2, 8, 3), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left };
    }

    public static FrameworkElement Bar(double frac, Brush color, double height = 6, Brush track = null)
    {
        frac = double.IsNaN(frac) ? 0 : Math.Clamp(frac, 0, 1);
        var g = new Grid { Height = height };
        g.Children.Add(new Border { Background = track ?? Bg4, CornerRadius = new CornerRadius(height / 2) });
        var cols = Cols($"{frac.ToString(Inv)}*,{(1 - frac).ToString(Inv)}*", new Border { Background = color, CornerRadius = new CornerRadius(height / 2) });
        g.Children.Add(cols);
        return g;
    }

    /// <summary>Number field that commits on Enter / focus loss.</summary>
    public static TextBox NumBox(double value, Action<double> set, int decimals = 0, double min = double.MinValue, double max = double.MaxValue, double width = 120)
    {
        var tb = new TextBox { Text = Num(value, decimals), Width = width, FontFamily = Font("MonoFont"), HorizontalContentAlignment = HorizontalAlignment.Right };
        double last = value;
        void Commit()
        {
            if (TryParse(tb.Text, out double v))
            {
                v = Math.Clamp(decimals == 0 ? Math.Round(v) : v, min, max);
                if (v != last) { last = v; set(v); }
            }
            tb.Text = Num(last, decimals);
        }
        tb.LostKeyboardFocus += (_, _) => Commit();
        tb.GotKeyboardFocus += (_, _) => tb.Dispatcher.BeginInvoke(new Action(tb.SelectAll));
        tb.KeyDown += (_, e) => { if (e.Key == Key.Enter) { Commit(); Keyboard.ClearFocus(); e.Handled = true; } else if (e.Key == Key.Escape) { tb.Text = Num(last, decimals); Keyboard.ClearFocus(); } };
        return tb;
    }
    public static TextBox TextBoxFor(string value, Action<string> set, double width = 200)
    {
        var tb = new TextBox { Text = value, Width = width };
        tb.LostKeyboardFocus += (_, _) => { if (tb.Text != value) { value = tb.Text; set(value); } };
        tb.KeyDown += (_, e) => { if (e.Key == Key.Enter) Keyboard.ClearFocus(); };
        return tb;
    }

    /// <summary>Big clickable record tile: click the value to edit.</summary>
    public static Border Tile(string label, double value, Action<double> set, string icon = null, Brush accent = null, int decimals = 0, Func<double, string> fmt = null, double width = 190, string hint = null)
    {
        accent ??= Hay;
        fmt ??= v => Short(v);
        var valText = Head(fmt(value), 22);
        var host = new Grid();
        host.Children.Add(valText);
        var top = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 0, 0, 8) };
        if (icon != null) top.Children.Add(new Border { Width = 26, Height = 26, CornerRadius = new CornerRadius(7), Background = Alpha(accent, 0.13), Child = Icon(icon, 13, accent), Margin = new Thickness(0, 0, 9, 0) });
        top.Children.Add(T(label, 12, Dim));
        var sub = T(hint ?? Num(value, decimals == 0 ? 0 : 2), 10.5, Faint, false, "MonoFont");
        var box = Box(V(top, host, sub.M(0, 3, 0, 0)), pad: 14);
        box.Width = width; box.Margin = new Thickness(0, 0, 10, 10);
        if (set == null) return box;
        box.Cursor = Cursors.IBeam;
        box.MouseEnter += (_, _) => box.BorderBrush = Hex("#444A5A");
        box.MouseLeave += (_, _) => box.BorderBrush = Line;
        box.MouseLeftButtonUp += (_, _) =>
        {
            if (host.Children.Count > 1) return;
            valText.Visibility = Visibility.Hidden;
            TextBox ed = null;
            ed = NumBox(value, v => { value = v; set(v); valText.Text = fmt(v); if (hint == null) sub.Text = Num(v, decimals == 0 ? 0 : 2); }, decimals == 0 ? 0 : 4, width: double.NaN);
            ed.FontSize = 15; ed.HorizontalContentAlignment = HorizontalAlignment.Left;
            ed.LostKeyboardFocus += (_, _) => { host.Children.Remove(ed); valText.Visibility = Visibility.Visible; };
            host.Children.Add(ed);
            ed.Dispatcher.BeginInvoke(new Action(() => { ed.Focus(); ed.SelectAll(); }), System.Windows.Threading.DispatcherPriority.Input);
        };
        return box;
    }

    public static FrameworkElement Stepper(double value, double min, double max, Action<double> set, double width = 44, Func<double, string> fmt = null)
    {
        fmt ??= v => Num(v);
        var t = Mono(fmt(value), 13); t.Width = width; t.TextAlignment = TextAlignment.Center;
        void Go(double d) { double n = Math.Clamp(value + d, min, max); if (n == value) return; value = n; t.Text = fmt(n); set(n); }
        var minus = Btn("", () => Go(-1), "Btn", ""); minus.Padding = new Thickness(7, 6, 7, 6);
        var plus = Btn("", () => Go(1), "Btn", ""); plus.Padding = new Thickness(7, 6, 7, 6);
        return H(minus, t, plus);
    }

    public static ComboBox Combo(IEnumerable<string> items, string selected, Action<string> changed, Func<string, string> label = null, double width = 160)
    {
        var cb = new ComboBox { Width = width };
        foreach (var it in items) cb.Items.Add(new ComboBoxItem { Content = label?.Invoke(it) ?? it, Tag = it, IsSelected = it == selected });
        cb.SelectionChanged += (_, _) => { if (cb.SelectedItem is ComboBoxItem i) changed((string)i.Tag); };
        return cb;
    }

    public static Effect Glow(Color c, double radius = 18, double opacity = 0.6) => new DropShadowEffect { Color = c, BlurRadius = radius, ShadowDepth = 0, Opacity = opacity };

    /// <summary>Donut gauge.</summary>
    public static FrameworkElement Ring(double frac, double size, double thickness, Brush color, UIElement center = null)
    {
        frac = Math.Clamp(double.IsNaN(frac) ? 0 : frac, 0, 0.9999);
        var g = new Grid { Width = size, Height = size };
        g.Children.Add(new Ellipse { Stroke = Bg4, StrokeThickness = thickness });
        double r = (size - thickness) / 2, cx = size / 2, a = frac * 2 * Math.PI;
        var fig = new PathFigure { StartPoint = new Point(cx, cx - r) };
        fig.Segments.Add(new ArcSegment(new Point(cx + r * Math.Sin(a), cx - r * Math.Cos(a)), new Size(r, r), 0, frac > 0.5, SweepDirection.Clockwise, true));
        g.Children.Add(new Path { Data = new PathGeometry(new[] { fig }), Stroke = color, StrokeThickness = thickness, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round });
        if (center != null) g.Children.Add(new Viewbox { Child = center, Margin = new Thickness(thickness + 8), StretchDirection = StretchDirection.DownOnly });
        return g;
    }
}
