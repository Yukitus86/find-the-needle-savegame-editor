using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using static FtnEditor.Ui;

namespace FtnEditor;

public static class NeedleOps
{
    public static int Count(SaveGame s) => s.State.Get("needle_taken")?.Bytes?.Length ?? 0;
    public static bool IsFound(SaveGame s, int i) => s.State.Get("needle_taken").Bytes[i] != 0;
    public static int TypeOf(SaveGame s, int i) { var t = s.State.Get("needle_type")?.Bytes; return t != null && i < t.Length ? t[i] : 0; }
    public static (double x, double y, double z) Pos(SaveGame s, int i) { var p = s.State.Get("needle_positions").F32; return (p[i * 3], p[i * 3 + 1], p[i * 3 + 2]); }
    /// <summary>Hay above the needle in metres (≤ 0 = lying open).</summary>
    public static double Depth(SaveGame s, int i) { var (x, y, z) = Pos(s, i); return s.HeightAt(x, z) - y; }

    static void Bump(SaveGame s, string key, int idx, int d) { var a = s.State.Get(key)?.I32; if (a != null && idx < a.Length) a[idx] = Math.Max(0, a[idx] + d); }

    /// <summary>Marks a pile needle as found / not found and keeps every counter in step, like the game does.</summary>
    public static void SetFound(SaveGame s, int i, bool found)
    {
        var taken = s.State.Get("needle_taken").Bytes;
        if ((taken[i] != 0) == found) return;
        taken[i] = (byte)(found ? 1 : 0);
        int type = TypeOf(s, i), d = found ? 1 : -1;
        var disc = s.State.Get("discovered")?.Bytes;
        if (found && disc != null && type < disc.Length) disc[type] = 1;
        Bump(s, "needle_stock", type, d); Bump(s, "needles_by_type", type, d);
        s.State.SetNum("needles_found", Math.Max(0, s.State.Num("needles_found") + d));
        s.State.SetNum("pile_needles_found", Math.Max(0, s.State.Num("pile_needles_found") + d));
        if (found && s.State.Num("first_needle_secs") < 0) s.State.SetNum("first_needle_secs", s.State.Num("run_secs"));
    }

    /// <summary>Digs a cone-shaped pit down to the needle.</summary>
    public static void Excavate(SaveGame s, int i, double radius = 1.8)
    {
        var (nx, ny, nz) = Pos(s, i);
        var h = s.Heights; int n = s.Grid; if (h == null) return;
        double before = s.HeightSum(), floor = Math.Max(0, ny - 0.12);
        for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                double d = Math.Sqrt(Math.Pow(s.WorldOf(x) - nx, 2) + Math.Pow(s.WorldOf(z) - nz, 2));
                if (d > radius + 6) continue;
                double target = floor + Math.Max(0, d - radius * 0.35) * 0.9;
                if (h[z * n + x] > target) h[z * n + x] = (float)target;
            }
        s.ApplyVolumeChange(before);
    }

    public static void TeleportPlayer(SaveGame s, double x, double z)
    {
        var p = s.Root["player"]?.Floats; if (p == null || p.Length < 12) return;
        p[9] = x; p[11] = z; p[10] = s.HeightAt(x, z) + 0.6;
    }
}

public static partial class Pages
{
    static void Dirty() => MainWindow.I.MarkDirty();
    static void Reload() => MainWindow.I.Refresh();
    static GdVar NewLike(GdVar sample, long v) => new() { Header = sample != null && sample.Type == GdVar.INT ? sample.Header : (uint)GdVar.INT, Value = v };
    static string Z(string s) => (s ?? "").TrimEnd('\0');

    static Border StTile(SaveGame s, string key, string label, string icon, Brush accent = null, int dec = 0, Func<double, string> fmt = null, string hint = null)
        => Tile(label, s.State.Num(key), v => { s.State.SetNum(key, v); Dirty(); }, icon, accent, dec, fmt, hint: hint);
    static CheckBox StSwitch(SaveGame s, string key, string label, string tip = null)
    {
        var c = Switch(label, s.State.Get(key)?.Bool == true, v => { var g = s.State.Get(key); if (g != null) g.Bool = v; else s.State.Set(key, GdVar.NewBool(v)); Dirty(); });
        if (tip != null) c.Tip(tip); return c.M(0, 0, 22, 10);
    }

    // =====================================================================  RUN & MONEY
    public static FrameworkElement Run(SaveGame s)
    {
        var st = s.State;
        double money = st.Num("money");

        // --- hero: balance
        var moneyBox = NumBox(money, v => { st.SetNum("money", v); if (v > st.Num("money_peak")) st.SetNum("money_peak", v); Dirty(); }, 2, 0, 1e15, 280);
        moneyBox.FontSize = 30; moneyBox.FontFamily = Font("HeadFont"); moneyBox.HorizontalContentAlignment = HorizontalAlignment.Left; moneyBox.Padding = new Thickness(12, 8, 12, 8); moneyBox.Foreground = Hay;
        void Add(double d, bool mul = false) { double v = mul ? st.Num("money") * d : st.Num("money") + d; st.SetNum("money", v); if (v > st.Num("money_peak")) st.SetNum("money_peak", v); Dirty(); Reload(); }
        var quick = Flow(
            Btn("+1 000", () => Add(1e3)).M(0, 0, 6, 6), Btn("+10 000", () => Add(1e4)).M(0, 0, 6, 6), Btn("+100 000", () => Add(1e5)).M(0, 0, 6, 6), Btn("+1 Mio", () => Add(1e6)).M(0, 0, 6, 6),
            Btn("× 2", () => Add(2, true)).M(0, 0, 6, 6), Btn("× 10", () => Add(10, true)).M(0, 0, 6, 6));
        double debt = st.Num("debt");
        var debtRow = H(T("Schulden", 12, Dim).M(0, 0, 10, 0), NumBox(debt, v => { st.SetNum("debt", v); Dirty(); }, 2, 0, 1e15, 130),
            debt > 0 ? Btn("Tilgen", () => { st.SetNum("debt", 0); Dirty(); Reload(); }, "Btn", "").M(8, 0, 0, 0) : Pill("schuldenfrei", Good).M(10, 0, 0, 0));
        var hero = Box(V(H(Icon("", 13, Hay).M(0, 0, 8, 0), Caps("Kontostand")), H(Head("$", 30, Faint).M(0, 0, 10, 0), moneyBox).M(0, 10, 0, 14), quick, debtRow.M(0, 8, 0, 0)), pad: 20);
        hero.Background = new LinearGradientBrush(C("#221C10"), C("#1A1D24"), 20);

        // --- pile gauge
        double init = st.Num("hay_initial"), total = st.Num("hay_total"), goneFrac = init > 0 ? 1 - total / init : 0;
        var ringCenter = V(Head($"{goneFrac * 100:0.0}%", 26), T("abgebaut", 11, Dim).M(0, 2, 0, 0));
        ((TextBlock)ringCenter.Children[0]).HorizontalAlignment = HorizontalAlignment.Center; ((TextBlock)ringCenter.Children[1]).HorizontalAlignment = HorizontalAlignment.Center;
        var pileInfo = V(
            H(Icon("", 13, Hay).M(0, 0, 8, 0), Caps("Heuhaufen")),
            T($"{GameData.PileName(s.Meta?.Str("pile_size"))} · {Ui.Pretty(s.Meta?.Str("map"))}", 15, Ui.Text, true).M(0, 10, 0, 10),
            Cols("110,*", T("Noch da", 12, Dim), NumBox(total, v => { st.SetNum("hay_total", v); Dirty(); }, 0, 0, 1e15, 150).Left()).M(0, 0, 0, 6),
            Cols("110,*", T("Zu Beginn", 12, Dim), NumBox(init, v => { st.SetNum("hay_initial", v); Dirty(); }, 0, 0, 1e15, 150).Left()).M(0, 0, 0, 6),
            Cols("110,*", T("Raster", 12, Dim), Mono($"{s.Grid} × {s.Grid} · {s.Cell:0.##} m", 12, Dim)));
        var pile = Box(Cols("a,*", Ring(goneFrac, 132, 11, Hay, ringCenter).M(0, 0, 22, 0), pileInfo), pad: 20);

        // --- records
        var records = Flow(
            StTile(s, "money_earned", "Insgesamt verdient", "", Hay, 2),
            StTile(s, "money_peak", "Höchster Kontostand", "", Hay, 2),
            StTile(s, "best_income", "Bestes Einkommen", "", Good, 2, hint: "$ pro Minute"),
            StTile(s, "best_sale", "Bester Verkauf", "", Good, 2),
            StTile(s, "best_sale_strands", "Halme im besten Verkauf", "", Good),
            StTile(s, "best_combo", "Beste Kombo", "", Hex("#FF8F5C")),
            StTile(s, "hay_dug", "Heu abgebaut", "", Steel),
            StTile(s, "hay_sold", "Heu verkauft", "", Steel),
            StTile(s, "hay_returned", "Heu zurückgelegt", "", Steel),
            StTile(s, "stacks_ordered", "Stapel bestellt", "", Steel));

        Func<double, string> tf = v => Time(v);
        var times = Flow(
            StTile(s, "run_secs", "Spielzeit", "", Hex("#B48CF2"), 1, tf, "Sekunden"),
            StTile(s, "first_needle_secs", "Erste Nadel nach", "", Hex("#B48CF2"), 1, tf, "Sekunden · -1 = noch nicht"),
            StTile(s, "first_clear_secs", "Haufen leer nach", "", Hex("#B48CF2"), 1, tf, "Sekunden · -1 = noch nicht"),
            StTile(s, "complete_secs", "Abgeschlossen nach", "", Hex("#B48CF2"), 1, tf, "Sekunden · -1 = noch nicht"));

        // --- gadgets
        double jet = st.Num("jetpack_fuel");
        var jetVal = Mono($"{jet:0.0} / 10", 12, Dim);
        var jetSlider = new Slider { Minimum = 0, Maximum = 10, Value = jet, Width = 210 };
        jetSlider.ValueChanged += (_, e) => { st.SetNum("jetpack_fuel", Math.Round(e.NewValue, 1)); jetVal.Text = $"{e.NewValue:0.0} / 10"; Dirty(); };
        double cool = st.Num("radar_cool");
        var gadgets = V(
            Cols("150,a,*", T("Jetpack-Treibstoff", 12.5), jetSlider, jetVal.M(12, 0, 0, 0)).M(0, 0, 0, 12),
            Cols("150,a,*", T("Radar-Abklingzeit", 12.5), NumBox(cool, v => { st.SetNum("radar_cool", v); Dirty(); }, 1, 0, 1e6, 90),
                H(T("s", 12, Dim).M(6, 0, 12, 0), cool > 0 ? Btn("Sofort bereit", () => { st.SetNum("radar_cool", 0); Dirty(); Reload(); }, "Btn", "") : Pill("bereit", Good))).M(0, 0, 0, 12),
            Cols("150,a,*", T("Grundstücks-Stufe", 12.5), Stepper(st.Num("lot_tier"), 0, 20, v => { st.SetNum("lot_tier", v); Dirty(); }).Left()));

        var flags = Flow(
            StSwitch(s, "pile_cleared", "Haufen geleert"),
            StSwitch(s, "floor_hatch", "Bodenluke"));
        var meta = s.Meta;
        var slotCard = meta == null ? null : Card("Slot", V(
            Cols("110,*", T("Name", 12, Dim), TextBoxFor(meta.Str("name"), v => { meta.Get("name").Str = v; Dirty(); }, 220).Left()).M(0, 0, 0, 10),
            Flow(Switch("Gesperrt", meta.Get("locked")?.Bool == true, v => { if (meta.Get("locked") is { } g) { g.Bool = v; Dirty(); } }).M(0, 0, 22, 0),
                 T(Loc.L("Zuletzt gespeichert:") + " " + Loc.Date(DateTimeOffset.FromUnixTimeSeconds((long)meta.Num("saved_at")).LocalDateTime), 12, Dim))), icon: "");

        var row1 = Cols("*,18,*", hero, null, pile);
        var row3 = Cols("*,18,*", Card("Gadgets", gadgets, icon: ""), null, V(Card("Lauf-Flags", flags, icon: ""), slotCard?.M(0, 14, 0, 0)));
        return Scroll(V(row1, Caps("Rekorde dieses Laufs · Wert anklicken zum Ändern").M(2, 24, 0, 10), records, Caps("Zeiten").M(2, 14, 0, 10), times, row3.M(0, 8, 0, 0)));
    }

    // =====================================================================  NEEDLES
    public static FrameworkElement Needles(SaveGame s)
    {
        var st = s.State;
        int n = NeedleOps.Count(s);
        int found = Enumerable.Range(0, n).Count(i => NeedleOps.IsFound(s, i));
        var center = V(Head($"{found}/{n}", 24));
        var summary = Box(Cols("a,*,a",
            Ring(n > 0 ? (double)found / n : 0, 84, 8, Steel, center).M(0, 0, 20, 0),
            V(Head(found == n && n > 0 ? "Alle Nadeln gefunden" : (n - found == 1 ? Loc.L("Noch 1 Nadel im Heu") : Loc.L("Noch {0} Nadeln im Heu", n - found)), 17),
              T("„Gefunden“ zählt wie im Spiel: Sammlung, Bestand und Zähler werden mitgeführt.", 12, Dim).Wrap().M(0, 5, 0, 0)).VCenter(),
            H(Btn("Alle finden", () => { for (int i = 0; i < n; i++) NeedleOps.SetFound(s, i, true); Dirty(); Reload(); }, "BtnAccent", "").M(0, 0, 8, 0),
              Btn("Alle zurück ins Heu", () => { for (int i = 0; i < n; i++) NeedleOps.SetFound(s, i, false); Dirty(); Reload(); }, "Btn", "")).VCenter()), pad: 18);

        var cards = new WrapPanel();
        double maxH = s.Heights?.Length > 0 ? s.Heights.Max() : 1;
        var typeIds = Enumerable.Range(0, Math.Max(24, st.Get("discovered")?.Bytes?.Length ?? 24)).Select(i => i.ToString()).ToArray();
        for (int i = 0; i < n; i++)
        {
            int idx = i; bool isFound = NeedleOps.IsFound(s, i); int type = NeedleOps.TypeOf(s, i);
            var (x, y, z) = NeedleOps.Pos(s, i); double depth = NeedleOps.Depth(s, i);
            var col = new SolidColorBrush(GameData.NeedleColor(type));
            var gem = new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(10), Background = Alpha(col, isFound ? 0.08 : 0.16), Child = NeedleGlyph(col, 22), Opacity = isFound ? 0.55 : 1 };
            if (!isFound) gem.Effect = Glow(col.Color, 16, 0.35);
            var head = Cols("a,*,a", gem.M(0, 0, 12, 0),
                V(T(Loc.L("Nadel {0}", i + 1), 14, Ui.Text, true), Combo(typeIds, type.ToString(), v => { st.Get("needle_type").Bytes[idx] = byte.Parse(v); Dirty(); Reload(); }, v => GameData.NeedleName(int.Parse(v)), 150).M(0, 4, 0, 0).Left()),
                Pill(isFound ? "gefunden" : "im Heu", isFound ? Good : Hay).VCenter());
            head.VerticalAlignment = VerticalAlignment.Top;
            string depthText = depth <= 0.02 ? Loc.L("liegt frei") : Loc.L("{0} m Heu darüber", depth.ToString("0.00"));
            var depthBar = V(Cols("*,a", T("Tiefe", 11.5, Dim), Mono(depthText, 11.5, depth <= 0.02 ? Good : Ui.Text)), Bar(depth / Math.Max(maxH, 0.1), col, 5).M(0, 5, 0, 0));
            var pos = Cols("*,*,*", Coord("X", x), Coord("Höhe", y), Coord("Z", z));
            var actions = Cols("*,a", Switch("Gefunden", isFound, v => { NeedleOps.SetFound(s, idx, v); Dirty(); Reload(); }),
                H(Btn("", () => { NeedleOps.Excavate(s, idx); Dirty(); Reload(); MainWindow.I.Toast(Loc.L("Nadel {0} freigelegt", idx + 1)); }, "Btn", "").Tip("Freilegen: gräbt einen Trichter bis zur Nadel").M(0, 0, 6, 0),
                  Btn("", () => { var p = NeedleOps.Pos(s, idx); NeedleOps.TeleportPlayer(s, p.x, p.z); Dirty(); MainWindow.I.Toast(Loc.L("Spieler steht jetzt über Nadel {0}", idx + 1)); }, "Btn", "").Tip("Spieler direkt über die Nadel stellen")));
            var card = Box(V(head, pos.M(0, 14, 0, 12), depthBar, new Border { Height = 1, Background = Line, Margin = new Thickness(0, 14, 0, 12) }, actions));
            card.Width = 300; card.Margin = new Thickness(0, 0, 12, 12);
            cards.Children.Add(card);
        }

        // collection
        var disc = st.Get("discovered")?.Bytes; var stock = st.Get("needle_stock")?.I32; var byType = st.Get("needles_by_type")?.I32;
        var coll = new WrapPanel();
        if (disc != null)
            for (int t = 0; t < disc.Length; t++)
            {
                int ti = t; var col = new SolidColorBrush(GameData.NeedleColor(t)); bool known = disc[t] != 0;
                var icon = new Border { Width = 30, Height = 30, CornerRadius = new CornerRadius(8), Background = Alpha(col, known ? 0.16 : 0.05), Child = NeedleGlyph(known ? col : Faint, 16) };
                var cell = Box(V(
                    Cols("a,*,a", icon.M(0, 0, 10, 0), T(GameData.NeedleName(t), 12.5, known ? Ui.Text : Dim, true), Switch("", known, v => { disc[ti] = (byte)(v ? 1 : 0); Dirty(); Reload(); }).Tip("Entdeckt")),
                    Cols("*,a", T("Bestand", 11.5, Dim), stock != null && t < stock.Length ? Stepper(stock[t], 0, 9999, v => { stock[ti] = (int)v; Dirty(); }, 34) : null).M(0, 10, 0, 4),
                    Cols("*,a", T("Insgesamt", 11.5, Dim), byType != null && t < byType.Length ? Stepper(byType[t], 0, 9999, v => { byType[ti] = (int)v; Dirty(); }, 34) : null)), pad: 12);
                cell.Width = 222; cell.Margin = new Thickness(0, 0, 10, 10);
                coll.Children.Add(cell);
            }
        int known2 = disc?.Count(b => b != 0) ?? 0;
        var collHead = Cols("*,a", Caps(Loc.L("Sammlung · {0} von {1} Typen entdeckt", known2, disc?.Length ?? 0)),
            H(Btn("Alle entdecken", () => { for (int t = 0; t < disc.Length; t++) disc[t] = 1; Dirty(); Reload(); }, "Btn", "").M(0, 0, 8, 0),
              Btn("Bestand +1 für alle", () => { for (int t = 0; t < stock.Length; t++) { stock[t]++; if (byType != null && t < byType.Length) byType[t]++; if (disc != null && t < disc.Length) disc[t] = 1; } Dirty(); Reload(); }, "Btn", "")));
        var total = Cols("150,a,*", T("Nadeln gefunden (Zähler)", 12, Dim), NumBox(st.Num("needles_found"), v => { st.SetNum("needles_found", v); Dirty(); }, 0, 0, 1e6, 80)).M(2, 0, 0, 16);
        ((Grid)total).ColumnDefinitions[0].Width = new GridLength(190);

        return Scroll(V(summary, Caps("Im Haufen versteckt").M(2, 22, 0, 10), cards, collHead.M(2, 12, 0, 12), disc == null ? null : coll, total.M(2, 8, 0, 0)));
    }

    static FrameworkElement Coord(string label, double v) => V(T(label, 10.5, Faint), Mono(v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), 13).M(0, 2, 0, 0));

    public static FrameworkElement NeedleGlyph(Brush color, double size)
    {
        // a sewing needle: slim diagonal body with an eye
        var g = new Canvas { Width = size, Height = size, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        g.Children.Add(new System.Windows.Shapes.Line { X1 = size * 0.12, Y1 = size * 0.88, X2 = size * 0.80, Y2 = size * 0.20, Stroke = color, StrokeThickness = Math.Max(1.6, size * 0.09), StrokeStartLineCap = PenLineCap.Triangle, StrokeEndLineCap = PenLineCap.Round });
        var eye = new System.Windows.Shapes.Ellipse { Width = size * 0.24, Height = size * 0.24, Stroke = color, StrokeThickness = Math.Max(1.3, size * 0.07) };
        Canvas.SetLeft(eye, size * 0.70); Canvas.SetTop(eye, size * 0.06); g.Children.Add(eye);
        return g;
    }

    // =====================================================================  GEAR
    public static FrameworkElement Gear(SaveGame s)
    {
        var st = s.State;
        // tools
        var owned = st.Get("owned_tools")?.List;
        var toolWrap = new WrapPanel();
        if (owned != null)
        {
            var all = GameData.Tools.Concat(owned.Select(o => o.Str)).Distinct().ToList();
            foreach (var id in all)
            {
                bool has = owned.Any(o => o.Str == id);
                var glyph = GameData.ToolGlyph.GetValueOrDefault(id, "");
                var iconBox = new Border { Width = 46, Height = 46, CornerRadius = new CornerRadius(12), Background = has ? Alpha(Hay, 0.16) : Bg3, Child = Icon(glyph, 20, has ? Hay : Faint) };
                if (has) iconBox.Effect = Glow(((SolidColorBrush)Hay).Color, 18, 0.3);
                var tile = Box(V(Cols("a,*,a", iconBox, null, has ? Pill("im Besitz", Good) : Pill("fehlt", Faint)), T(GameData.ToolName(id), 13.5, has ? Ui.Text : Dim, true).M(0, 12, 0, 1), T(id, 10.5, Faint, false, "MonoFont")), pad: 14);
                ((Grid)((StackPanel)tile.Child).Children[0]).VerticalAlignment = VerticalAlignment.Top;
                tile.Width = 178; tile.Margin = new Thickness(0, 0, 10, 10); tile.Cursor = Cursors.Hand; tile.BorderBrush = has ? Alpha(Hay, 0.4) : Line;
                tile.MouseLeftButtonUp += (_, _) => { if (has) owned.RemoveAll(o => o.Str == id); else owned.Add(GdVar.NewString(id)); Dirty(); Reload(); };
                tile.Tip("Klicken zum Geben / Wegnehmen");
                toolWrap.Children.Add(tile);
            }
        }
        var shop = s.Root["tech"]?["shop_tools"];
        var toolHead = Cols("*,a", Caps("Werkzeuge"), owned == null ? null : H(
            shop == null ? null : Switch("Werkzeug-Shop offen", shop.Bool, v => { shop.Bool = v; Dirty(); }).M(0, 0, 18, 0),
            Btn("Alle geben", () => { foreach (var id in GameData.Tools) if (!owned.Any(o => o.Str == id)) owned.Add(GdVar.NewString(id)); Dirty(); Reload(); }, "Btn", "").M(0, 0, 6, 0),
            Btn("Alle wegnehmen", () => { owned.Clear(); Dirty(); Reload(); }, "Btn", "")));

        // hotbar
        var hot = st.Get("hotbar")?.Strings;
        var known = GameData.Builds.Concat(hot?.Select(Z) ?? Array.Empty<string>()).Concat(st.Get("recent_builds")?.Strings?.Select(Z) ?? Array.Empty<string>())
            .Concat(st.Get("fresh_builds")?.Strings?.Select(Z) ?? Array.Empty<string>())
            .Concat(st.Get("purchase_log")?.List?.Where(p => p.List?.Count > 2 && p.List[1].Str == "build").Select(p => p.List[2].Str) ?? Array.Empty<string>())
            .Where(x => x != "").Distinct().OrderBy(x => x).Prepend("").ToList();
        var hotRow = new WrapPanel();
        if (hot != null)
            for (int i = 0; i < hot.Length; i++)
            {
                int idx = i; string cur = Z(hot[i]);
                var key = new Border { Width = 22, Height = 22, CornerRadius = new CornerRadius(6), Background = Bg4, Child = Mono((i + 1).ToString(), 11.5, Ui.Text) };
                ((TextBlock)key.Child).HorizontalAlignment = HorizontalAlignment.Center;
                var slot = Box(V(Cols("a,*", key, cur == "" ? T("leer", 11, Faint).M(8, 0, 0, 0) : null), Combo(known, cur, v => { hot[idx] = v + "\0"; Dirty(); Reload(); }, v => v == "" ? "(leer)" : Ui.Pretty(v), 128).M(0, 10, 0, 0)), pad: 10);
                slot.Margin = new Thickness(0, 0, 8, 8); slot.BorderBrush = cur == "" ? Line : Alpha(Steel, 0.35);
                hotRow.Children.Add(slot);
            }

        // gifts
        var gifts = st.Get("gifts")?.Dict;
        var giftPanel = new StackPanel();
        if (gifts != null)
        {
            foreach (var kv in gifts.Items.ToList())
            {
                string id = kv.Key.ToString(); var val = kv.Value;
                giftPanel.Children.Add(Cols("a,*,a,a", Icon("", 13, Hay).M(0, 0, 10, 0), T(Ui.Pretty(id), 12.5), Stepper(val.Num, 0, 999, v => { val.Num = v; Dirty(); }).M(0, 0, 8, 0),
                    Btn("", () => { gifts.Remove(id); Dirty(); Reload(); }, "BtnGhost", "")).M(0, 0, 0, 6));
            }
            if (gifts.Items.Count == 0) giftPanel.Children.Add(T("Keine Geschenke warten auf Abholung.", 12, Dim).M(0, 0, 0, 8));
            string pick = known.Skip(1).FirstOrDefault() ?? "belt";
            giftPanel.Children.Add(H(Combo(known.Skip(1), pick, v => pick = v, Ui.Pretty, 150), Btn("Geschenk hinzufügen", () => { if (gifts.Get(pick) is { } g) g.Num += 1; else gifts.Set(pick, GdVar.NewInt(1)); Dirty(); Reload(); }, "Btn", "").M(8, 0, 0, 0)).M(0, 6, 0, 0));
        }
        var fresh = st.Get("fresh_builds"); int freshN = fresh?.Strings?.Length ?? 0;
        var freshCard = Card("„Neu“-Abzeichen", V(
            T(freshN == 0 ? "Keine Bauteile sind als neu markiert." : Loc.L("{0} Bauteile sind im Baukatalog als neu markiert:", freshN), 12, Dim).Wrap(),
            freshN == 0 ? null : Flow(fresh.Strings.Select(f => (UIElement)Pill(Ui.Pretty(Z(f)), Steel).M(0, 0, 6, 6)).ToArray()).M(0, 10, 0, 4),
            freshN == 0 ? null : Btn("Abzeichen entfernen", () => { fresh.Value = Array.Empty<string>(); st.Get("fresh_since")?.Dict?.Items.Clear(); Dirty(); Reload(); }, "Btn", "").Left().M(0, 6, 0, 0)), icon: "");

        return Scroll(V(toolHead.M(2, 0, 10, 12), toolWrap,
            Caps("Bau-Leiste · Tasten 1–9").M(2, 14, 0, 12), hotRow,
            Cols("*,18,*", gifts == null ? null : Card("Geschenke", giftPanel, icon: ""), null, freshCard).M(0, 12, 0, 0)));
    }

    // =====================================================================  BUILDINGS
    public static FrameworkElement Buildings(SaveGame s)
    {
        var list = s.Buildings;
        if (list.Count == 0) return MainWindow.Empty("Noch nichts gebaut", "In diesem Spielstand stehen keine Gebäude.");
        bool HasOff(GdVar b) => b.Dict.Has("off");
        bool IsOn(GdVar b) => b["off"]?.Bool != true;
        var switchable = list.Where(HasOff).ToList();
        var gens = list.Where(b => b.Dict.Has("fuel")).ToList();
        double maxFuel = Math.Max(2500, Math.Ceiling((gens.Count > 0 ? gens.Max(g => g["fuel"].Num) : 0) / 500) * 500);
        double refuelTo = maxFuel;

        var overview = Flow(
            Tile("Gebäude gesamt", list.Count, null, "", Steel, hint: Loc.L("{0} Arten", list.Select(b => b["type"]?.Str).Distinct().Count())),
            Tile("Eingeschaltet", switchable.Count(IsOn), null, "", Good, hint: Loc.L("von {0} schaltbaren", switchable.Count)),
            Tile("Investiert", list.Sum(b => b.Dict.Num("paid")), null, "", Hay, hint: "$ Baukosten"),
            Tile("Treibstoff gesamt", gens.Sum(g => g["fuel"].Num), null, "", Hex("#FF8F5C"), hint: Loc.L("{0} Generatoren", gens.Count)));

        void All(bool on, IEnumerable<GdVar> which) { foreach (var b in which) if (b["off"] is { } o) o.Bool = !on; Dirty(); Reload(); }
        void Refuel(IEnumerable<GdVar> which) { foreach (var g in which) if (g["fuel"] is { } f) f.Num = Math.Max(f.Num, refuelTo); Dirty(); Reload(); }
        var actions = Box(Cols("a,a,*,a,a,a",
            Btn("Alle einschalten", () => All(true, switchable), "Btn", "").M(0, 0, 8, 0), Btn("Alle ausschalten", () => All(false, switchable), "Btn", ""), null,
            gens.Count == 0 ? null : T("Füllstand", 12, Dim).M(0, 0, 8, 0), gens.Count == 0 ? null : NumBox(refuelTo, v => refuelTo = v, 0, 0, 1e9, 90).M(0, 0, 8, 0),
            gens.Count == 0 ? null : Btn("Alle Generatoren auftanken", () => Refuel(gens), "BtnAccent", "")), pad: 12);

        var wrap = new WrapPanel();
        foreach (var grp in list.GroupBy(b => b["type"]?.Str ?? "?").OrderByDescending(g => g.Count()))
        {
            var info = GameData.Building(grp.Key); var color = Hex(info.Color); var items = grp.ToList();
            var sw = items.Where(HasOff).ToList(); int on = sw.Count(IsOn);
            var badge = new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(10), Background = Alpha(color, 0.14), Child = Icon(info.Icon, 17, color) };
            var head = Cols("a,*,a", badge.M(0, 0, 12, 0), V(T(info.Name, 14, Ui.Text, true), T(grp.Key, 10.5, Faint, false, "MonoFont")), Head("×" + items.Count, 20, color));
            var rows = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
            void Row(string label, UIElement right) => rows.Children.Add(Cols("*,a", T(label, 12, Dim), right).M(0, 0, 0, 7));
            if (sw.Count > 0)
            {
                Row(Loc.L("In Betrieb · {0}/{1}", on, sw.Count), Switch("", on == sw.Count, v => All(v, sw)));
                rows.Children.Add(Bar((double)on / sw.Count, Good, 4).M(0, 0, 0, 10));
            }
            var fuel = items.Where(b => b.Dict.Has("fuel")).ToList();
            if (fuel.Count > 0)
            {
                double avg = fuel.Average(f => f["fuel"].Num);
                Row(Loc.L("Treibstoff Ø {0}", Num(avg)), Btn("Auftanken", () => Refuel(fuel), "Btn", ""));
                rows.Children.Add(Bar(avg / maxFuel, Hex("#FF8F5C"), 4).M(0, 0, 0, 10));
            }
            if (items.Any(b => b.Dict.Has("stored"))) Row("Eingelagert", Mono(Num(items.Sum(b => b.Dict.Num("stored"))) + " " + Loc.L("Halme"), 12));
            if (items.Any(b => b.Dict.Has("tier"))) Row("Stufe", Mono(string.Join(" · ", items.GroupBy(b => (int)b.Dict.Num("tier")).OrderBy(g => g.Key).Select(g => $"T{g.Key}×{g.Count()}")), 12));
            if (items.Any(b => b.Dict.Has("trips"))) Row("Flüge", Mono(Num(items.Sum(b => b.Dict.Num("trips"))), 12));
            double paid = items.Sum(b => b.Dict.Num("paid"));
            if (paid > 0) Row("Investiert", Mono(Num(paid) + " $", 12, Hay));

            // per-unit details
            var details = new StackPanel { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 6, 0, 0) };
            bool built = false;
            var toggle = Btn("Einzeln anzeigen", null, "BtnGhost", ""); toggle.Foreground = Dim; toggle.FontSize = 11.5; toggle.Padding = new Thickness(0, 4, 8, 4); toggle.HorizontalAlignment = HorizontalAlignment.Left;
            toggle.Click += (_, _) =>
            {
                if (!built)
                {
                    built = true; int k = 0;
                    foreach (var b in items.Take(60))
                    {
                        k++; var p = b["position"]?.Floats ?? b["a"]?.Floats;
                        string where = p != null ? $"{p[0]:0.0} / {p[2]:0.0}" : "";
                        UIElement ctl = b["fuel"] is { } f ? NumBox(f.Num, v => { f.Num = v; Dirty(); }, 0, 0, 1e9, 74)
                            : b["stored"] is { } stv && stv.IsNumber ? NumBox(stv.Num, v => { stv.Num = v; Dirty(); }, 0, 0, 1e9, 74) : null;
                        UIElement swc = b["off"] is { } o ? Switch("", !o.Bool, v => { o.Bool = !v; Dirty(); }) : null;
                        details.Children.Add(Cols("26,*,a,a", Mono(k.ToString(), 11, Faint), Mono(where, 11.5, Dim), ctl, swc == null ? null : ((FrameworkElement)swc).M(10, 0, 0, 0)).M(0, 0, 0, 5));
                    }
                    if (items.Count > 60) details.Children.Add(T(Loc.L("… und {0} weitere (siehe Rohdaten)", items.Count - 60), 11, Faint));
                }
                details.Visibility = details.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            };
            var card = Box(V(head, rows, toggle, details));
            card.Width = 318; card.Margin = new Thickness(0, 0, 12, 12); card.VerticalAlignment = VerticalAlignment.Top;
            wrap.Children.Add(card);
        }
        var belts = s.Root["belts"]?["paths"]?.List;
        int onBelts = belts?.Sum(p => p["records"]?.List?.Count ?? 0) ?? 0;
        var beltCard = belts == null ? null : Box(Cols("a,*,a", Icon("", 16, Steel).M(0, 0, 12, 0),
            V(T(Loc.L("{0} Band-Strecken · {1} Teile unterwegs", belts.Count, onBelts), 13, Ui.Text, true), T("Leert alle Förderbänder (z. B. bei Stau). Die Teile verschwinden.", 11.5, Dim)),
            Btn("Bänder leeren", () => { if (!Dlg.Confirm("Bänder leeren", Loc.L("{0} Teile von allen Förderbändern entfernen?", onBelts), "Leeren", true)) return; foreach (var p in belts) p["records"]?.List?.Clear(); Dirty(); Reload(); }, "Btn", "").VCenter()), pad: 14);

        return Scroll(V(overview, actions.M(0, 4, 0, 18), wrap, beltCard?.M(0, 4, 0, 0)));
    }

    // =====================================================================  MISSIONS
    public static FrameworkElement Missions(SaveGame s)
    {
        var st = s.State;
        FrameworkElement Chips(string key, Brush color)
        {
            var list = st.Get(key)?.List; var wrap = new WrapPanel();
            if (list == null) return wrap;
            foreach (var item in list.ToList())
            {
                var x = Btn("", () => { list.Remove(item); Dirty(); Reload(); }, "BtnGhost", ""); x.Padding = new Thickness(4, 2, 2, 2); x.FontSize = 9;
                wrap.Children.Add(new Border { Background = Alpha(color, 0.12), CornerRadius = new CornerRadius(12), Padding = new Thickness(11, 3, 5, 3), Margin = new Thickness(0, 0, 6, 6), Child = H(T(Ui.Pretty(item.Str), 12, color), x) }.Tip(item.Str));
            }
            var add = new TextBox { Width = 150, Tag = key }; add.Tip("id eintippen und Enter");
            add.KeyDown += (_, e) => { if (e.Key == Key.Enter && add.Text.Trim() != "") { list.Add(GdVar.NewString(add.Text.Trim())); Dirty(); Reload(); } };
            wrap.Children.Add(add);
            return wrap;
        }
        var known = (st.Get("missions_paid")?.List?.Select(m => m.Str) ?? Array.Empty<string>()).Append(st.Str("mission_id")).Where(x => x != "").Distinct().ToList();
        var mission = Card("Aktuelle Mission", V(
            Head(Ui.Pretty(st.Str("mission_id")), 20, Hay),
            Cols("130,*", T("Missions-ID", 12, Dim), TextBoxFor(st.Str("mission_id"), v => { st.Get("mission_id").Str = v; Dirty(); Reload(); }, 230).Left()).M(0, 14, 0, 8),
            Cols("130,*", T("Position in der Liste", 12, Dim), Stepper(st.Num("mission_index"), 0, 999, v => { st.SetNum("mission_index", v); Dirty(); }).Left()).M(0, 0, 0, 8),
            Cols("130,*", T("Fortschritts-Schritt", 12, Dim), TextBoxFor(st.Str("mission_earned_step"), v => { if (st.Get("mission_earned_step") is { } g) { g.Str = v; Dirty(); } }, 230).Left()).M(0, 0, 0, 8),
            Cols("130,*", T("Fortschritt ab", 12, Dim), NumBox(st.Num("mission_earned_from"), v => { st.SetNum("mission_earned_from", v); Dirty(); }, 2, width: 130).Left()),
            T("Die Missionsliste steckt im verschlüsselten Spielpaket – darum gibt es hier ID und Index statt einer Auswahlliste. Index +1 springt zur nächsten Mission.", 11.5, Faint).Wrap().M(0, 14, 0, 0)), icon: "");
        var paid = Card("Belohnung erhalten für", Chips("missions_paid", Good), icon: "");
        var given = Card("Geschenke erhalten für", Chips("gifts_given", Hay), icon: "");

        var contracts = Card("Aufträge", V(
            Cols("130,*", T("Aktueller Auftrag", 12, Dim), Stepper(st.Num("contract_index"), 0, 999, v => { st.SetNum("contract_index", v); Dirty(); }).Left()).M(0, 0, 0, 8),
            Cols("130,*", T("Schon geliefert", 12, Dim), NumBox(st.Num("contract_delivered"), v => { st.SetNum("contract_delivered", v); Dirty(); }, 0, 0, 1e9, 110).Left()).M(0, 0, 0, 10),
            StSwitch(s, "contract_pinned", "Auftrag angepinnt"),
            T("Erledigt", 12, Dim).M(0, 4, 0, 8), Chips("contracts_done", Steel)), icon: "");

        // spending analysis
        var log = st.Get("purchase_log")?.List ?? new();
        var entries = log.Where(e => e.List?.Count >= 5).Select(e => (t: e.List[0].Num, kind: e.List[1].Str, id: e.List[2].Str, cost: e.List[4].Num)).ToList();
        double totalSpend = entries.Sum(e => e.cost);
        var analysis = new StackPanel();
        if (entries.Count == 0) analysis.Children.Add(T("Noch keine Käufe im Protokoll.", 12, Dim));
        else
        {
            var kinds = entries.GroupBy(e => e.kind).OrderByDescending(g => g.Sum(e => e.cost)).ToList();
            string[] pal = { "#5CC8E8", "#F2B544", "#6FD08C", "#B48CF2", "#FF8F5C" };
            var kindColor = kinds.Select((k, i) => (k.Key, pal[i % pal.Length])).ToDictionary(x => x.Key, x => Hex(x.Item2));
            // stacked share bar
            var share = new Grid { Height = 12, Margin = new Thickness(0, 4, 0, 10) };
            int ci = 0;
            foreach (var k in kinds)
            {
                share.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(k.Sum(e => e.cost), totalSpend * 0.004), GridUnitType.Star) });
                var seg = new Border { Background = kindColor[k.Key], CornerRadius = new CornerRadius(ci == 0 ? 6 : 0, ci == kinds.Count - 1 ? 6 : 0, ci == kinds.Count - 1 ? 6 : 0, ci == 0 ? 6 : 0), Margin = new Thickness(0, 0, ci == kinds.Count - 1 ? 0 : 2, 0) };
                Grid.SetColumn(seg, ci++); share.Children.Add(seg);
            }
            string KindName(string k) => Loc.L(k switch { "build" => "Gebäude", "card" => "Technik-Karten", "gift" => "Geschenke", "tool" => "Werkzeug", _ => Ui.Pretty(k) });
            var legend = Flow(kinds.Select(k => (UIElement)H(new Border { Width = 9, Height = 9, CornerRadius = new CornerRadius(3), Background = kindColor[k.Key], Margin = new Thickness(0, 0, 7, 0) }, T($"{KindName(k.Key)}  ", 12), Mono($"{Num(k.Sum(e => e.cost))} $ · {k.Count()}×", 11.5, Dim)).M(0, 0, 22, 6)).ToArray());
            analysis.Children.Add(Cols("*,a", Head(Num(totalSpend) + " $", 24), T(Loc.L("{0} Käufe in {1}", entries.Count, Time(entries.Max(e => e.t))), 12, Dim)));
            analysis.Children.Add(share); analysis.Children.Add(legend);

            // cumulative spend sparkline
            var spark = new System.Windows.Shapes.Polyline { Stroke = Hay, StrokeThickness = 1.6, StrokeLineJoin = PenLineJoin.Round, Stretch = Stretch.Fill, Height = 56, Margin = new Thickness(0, 10, 0, 4) };
            double cum = 0, tmax = Math.Max(1, entries.Max(e => e.t));
            spark.Points.Add(new Point(0, 0));
            foreach (var e in entries.OrderBy(e => e.t)) { cum += e.cost; spark.Points.Add(new Point(e.t / tmax, -cum)); }
            analysis.Children.Add(spark);
            analysis.Children.Add(Cols("*,a", T("Start", 10.5, Faint), T("Ausgaben über die Zeit", 10.5, Faint)));

            // top items
            var top = entries.GroupBy(e => (e.kind, e.id)).Select(g => (g.Key, sum: g.Sum(e => e.cost), n: g.Count())).OrderByDescending(g => g.sum).Take(12).ToList();
            double maxSum = top.Max(g => g.sum);
            analysis.Children.Add(Caps("Wofür das Geld draufging").M(0, 16, 0, 10));
            foreach (var g in top)
                analysis.Children.Add(Cols("150,*,110", T(Ui.Pretty(g.Key.id), 12), Bar(g.sum / maxSum, kindColor[g.Key.kind], 8, Brushes.Transparent).VCenter().M(0, 0, 10, 0), Mono($"{Num(g.sum)} $ · {g.n}×", 11.5, Dim).Right()).M(0, 0, 0, 7));
        }
        var spend = Card("Ausgaben-Analyse", analysis, entries.Count == 0 ? null : Btn("Protokoll leeren", () => { if (Dlg.Confirm("Kaufprotokoll leeren", "Alle Einträge im Kaufprotokoll löschen?", "Leeren", true)) { log.Clear(); Dirty(); Reload(); } }, "BtnGhost", ""), "");

        return Scroll(Cols("*,18,1.15*", V(mission, contracts.M(0, 14, 0, 0), paid.M(0, 14, 0, 0), given.M(0, 14, 0, 0)), null, spend));
    }
}
