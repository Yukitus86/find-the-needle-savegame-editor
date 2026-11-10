using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using static FtnEditor.Ui;

namespace FtnEditor;

public static partial class Pages
{
    static int techCat = -1;          // selected branch (-1 = all), remembered while the app runs
    static string techShow = "all";   // all | open | done

    static Button Small(string glyphOrText, Action click, bool isGlyph = true)
    {
        var b = isGlyph ? Btn("", click, "Btn", glyphOrText) : Btn(glyphOrText, click);
        b.Padding = isGlyph ? new Thickness(6, 4, 6, 4) : new Thickness(8, 4, 8, 4); b.FontSize = 11; b.MinWidth = 0;
        return b;
    }

    // =====================================================================  TECH
    public static FrameworkElement Tech(SaveGame s)
    {
        var ranks = s.Ranks;
        if (ranks == null) return MainWindow.Empty("Keine Technik-Daten", "Dieser Spielstand enthält keinen Technik-Block.");
        var sample = ranks.Items.FirstOrDefault().Value;
        int Get(string id) => (int)ranks.Num(id);
        void Set(string id, int v) { if (v <= 0) ranks.Remove(id); else if (ranks.Get(id) is { } g) g.Int = v; else ranks.Set(id, NewLike(sample, v)); Dirty(); }

        var cats = GameData.Tech.ToList();
        var extra = ranks.Keys.Where(k => !GameData.MaxRank.ContainsKey(k)).ToArray();
        if (extra.Length > 0) cats.Add(new("Weitere", "", "#9AA0AE", extra.Select(k => (k, Math.Max(Get(k), 1))).ToArray()));
        if (techCat >= cats.Count) techCat = -1;

        (int have, int max) Progress(IEnumerable<GameData.TechCat> which) { int h = 0, m = 0; foreach (var c in which) foreach (var (id, mx) in c.Cards) { h += Math.Min(Get(id), mx); m += mx; } return (h, m); }
        var updaters = new List<Action>();
        void RefreshAll() { foreach (var u in updaters) u(); }
        void SetAll(Func<string, int, int, int> f, IEnumerable<GameData.TechCat> which) { foreach (var c in which.ToList()) foreach (var (id, m) in c.Cards) Set(id, f(id, Get(id), m)); RefreshAll(); }

        string query = "";
        var sections = new List<(int cat, FrameworkElement section, List<(string id, int max, FrameworkElement row)> rows)>();
        var wraps = new List<WrapPanel>();
        var body = new StackPanel();
        var emptyNote = T("Keine Karte passt zu Suche und Filter.", 12.5, Dim).M(4, 10, 0, 0);
        var scroll = Scroll(V(body, emptyNote));
        var inner = (Border)scroll.Content; inner.Padding = new Thickness(14, 2, 26, 26);

        void ApplyFilter()
        {
            foreach (var (ci, section, rows) in sections)
            {
                int any = 0;
                foreach (var (id, max, row) in rows)
                {
                    int r = Get(id);
                    bool match = (query == "" || id.Contains(query, StringComparison.OrdinalIgnoreCase) || Ui.Pretty(id).Contains(query, StringComparison.OrdinalIgnoreCase))
                        && (techShow == "all" || (techShow == "open" ? r < max : r >= max));
                    row.Visibility = match ? Visibility.Visible : Visibility.Collapsed; if (match) any++;
                }
                section.Visibility = (techCat == -1 || techCat == ci) && any > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            emptyNote.Visibility = sections.Any(x => x.section.Visibility == Visibility.Visible) ? Visibility.Collapsed : Visibility.Visible;
        }

        FrameworkElement Row(string id, int max, Brush color)
        {
            var badgeText = Mono("", 11.5); badgeText.HorizontalAlignment = HorizontalAlignment.Center;
            var badgeIcon = Icon("", 12);
            var badgeGrid = new Grid(); badgeGrid.Children.Add(badgeText); badgeGrid.Children.Add(badgeIcon);
            var badge = new Border { Width = 30, Height = 30, CornerRadius = new CornerRadius(8), Child = badgeGrid };
            var name = T(Ui.Pretty(id), 13, Ui.Text, true);
            var row = Box(null, pad: 0); row.Padding = new Thickness(10, 8, 10, 8); row.Margin = new Thickness(0, 0, 8, 8);
            FrameworkElement center, right;
            Action specific;
            if (max == 1)
            {
                var status = T("", 11, Dim);
                var sw = Switch("", Get(id) > 0, v => { Set(id, v ? 1 : 0); RefreshAll(); });
                center = V(name, status.M(0, 2, 0, 0)); right = sw;
                specific = () => { bool on = Get(id) > 0; sw.IsChecked = on; status.Text = Loc.L(on ? "freigeschaltet" : "gesperrt"); status.Foreground = on ? Good : Faint; };
            }
            else
            {
                var rankText = Mono("", 11.5);
                var pips = new Grid { Height = 5, Margin = new Thickness(0, 7, 0, 1) };
                for (int i = 0; i < max; i++) { pips.ColumnDefinitions.Add(new ColumnDefinition()); var p = new Border { CornerRadius = new CornerRadius(2.5), Margin = new Thickness(0, 0, i == max - 1 ? 0 : 3, 0) }; Grid.SetColumn(p, i); pips.Children.Add(p); }
                var minus = Small("", () => { Set(id, Get(id) - 1); RefreshAll(); }).Tip("Rang −1");
                var plus = Small("", () => { Set(id, Math.Min(max, Get(id) + 1)); RefreshAll(); }).Tip("Rang +1");
                var maxB = Small("MAX", () => { Set(id, max); RefreshAll(); }, false);
                center = V(Cols("*,a", name, rankText.M(8, 0, 0, 0)), pips);
                right = H(minus.M(0, 0, 4, 0), plus.M(0, 0, 4, 0), maxB).M(12, 0, 0, 0);
                specific = () =>
                {
                    int r = Get(id); bool full = r >= max;
                    for (int i = 0; i < max; i++) ((Border)pips.Children[i]).Background = i < r ? (full ? Hay : color) : Bg4;
                    rankText.Text = $"{Math.Min(r, max)} / {max}"; rankText.Foreground = r > 0 ? Ui.Text : Faint;
                    minus.IsEnabled = r > 0; plus.IsEnabled = r < max; maxB.IsEnabled = r < max;
                };
            }
            row.Child = Cols("a,*,a", badge.M(0, 0, 10, 0), center.VCenter(), right.VCenter());
            void Update()
            {
                int r = Get(id); bool owned = r > 0, full = r >= max;
                specific();
                name.Foreground = owned ? Ui.Text : Dim;
                badge.Background = full ? Alpha(Hay, 0.16) : owned ? Alpha(color, 0.16) : Bg3;
                badgeIcon.Text = full ? "" : owned ? "" : "";
                badgeIcon.Foreground = full ? Hay : Faint;
                badgeText.Text = owned && !full ? r.ToString() : ""; badgeText.Foreground = color;
                row.BorderBrush = full ? Alpha(Hay, 0.35) : Line;
                row.Background = owned ? Bg2 : Hex("#15171C");
            }
            row.MouseRightButtonUp += (_, _) => { Set(id, Get(id) >= max ? 0 : max); RefreshAll(); };
            row.Tip(id + "\n" + Loc.L("Rechtsklick: MAX / zurücksetzen"));
            updaters.Add(Update);
            return row;
        }

        // ---- sections (right side)
        for (int ci = 0; ci < cats.Count; ci++)
        {
            var cat = cats[ci]; var color = Hex(cat.Color); var one = new[] { cat };
            var wrap = new WrapPanel { ItemWidth = 400 }; wraps.Add(wrap);
            var rows = new List<(string, int, FrameworkElement)>();
            foreach (var (id, max) in cat.Cards) { var r = Row(id, max, color); wrap.Children.Add(r); rows.Add((id, max, r)); }
            var catText = Mono("", 11.5, Dim); var catBar = new ContentControl { Focusable = false, Width = 120 };
            updaters.Add(() => { var (h, m) = Progress(one); catText.Text = $"{h} / {m}"; catBar.Content = Bar(m > 0 ? (double)h / m : 0, h >= m ? Hay : color, 4); });
            var badge = new Border { Width = 30, Height = 30, CornerRadius = new CornerRadius(8), Background = Alpha(color, 0.14), Child = Icon(cat.Icon, 13, color) };
            var head = Cols("a,a,a,a,*,a", badge.M(0, 0, 10, 0), Head(Loc.L(cat.Name), 15), catText.M(12, 0, 12, 0), catBar.VCenter(), null,
                H(Small("+1", () => SetAll((_, r, m) => Math.Min(m, r + 1), one), false).M(0, 0, 4, 0), Small("−1", () => SetAll((_, r, m) => Math.Max(0, r - 1), one), false).M(0, 0, 4, 0), Small("MAX", () => SetAll((_, _, m) => m, one), false)).M(0, 0, 8, 0));
            var section = V(head.M(0, 6, 0, 10), wrap);
            section.Margin = new Thickness(0, 0, 0, 14);
            sections.Add((ci, section, rows));
            body.Children.Add(section);
        }
        inner.SizeChanged += (_, e) =>
        {
            double w = e.NewSize.Width - inner.Padding.Left - inner.Padding.Right;
            int cols = w >= 1180 ? 3 : w >= 640 ? 2 : 1;
            double iw = Math.Max(260, Math.Floor(w / cols));
            foreach (var wp in wraps) wp.ItemWidth = iw;
        };

        // ---- branch list (left side)
        var nav = new StackPanel();
        var navItems = new List<(int ci, Border item, Brush color)>();
        Border NavItem(int ci, string label, string icon, Brush color, Func<(int, int)> prog)
        {
            var count = Mono("", 11, Dim); var barHost = new ContentControl { Focusable = false };
            var b = new Border { Padding = new Thickness(10, 8, 10, 8), CornerRadius = new CornerRadius(8), Margin = new Thickness(0, 0, 0, 4), Cursor = Cursors.Hand, BorderThickness = new Thickness(1) };
            b.Child = Cols("a,*,a", new Border { Width = 26, Height = 26, CornerRadius = new CornerRadius(7), Background = Alpha(color, 0.14), Child = Icon(icon, 12, color) }.M(0, 0, 10, 0),
                V(T(label, 12.5, Ui.Text, true), barHost.M(0, 5, 0, 0)), count.M(10, 0, 0, 0));
            updaters.Add(() => { var (h, m) = prog(); count.Text = $"{h}/{m}"; barHost.Content = Bar(m > 0 ? (double)h / m : 0, h >= m ? Hay : color, 3); });
            b.MouseLeftButtonUp += (_, _) => { techCat = ci; StyleNav(); ApplyFilter(); scroll.ScrollToTop(); };
            navItems.Add((ci, b, color));
            return b;
        }
        void StyleNav()
        {
            foreach (var (ci, item, color) in navItems)
            {
                bool on = ci == techCat;
                item.Background = on ? Bg3 : Brushes.Transparent;
                item.BorderBrush = on ? Alpha(color, 0.55) : Brushes.Transparent;
            }
        }
        nav.Children.Add(NavItem(-1, Loc.L("Alle Zweige"), "", Hay, () => Progress(cats)));
        nav.Children.Add(new Border { Height = 1, Background = Line, Margin = new Thickness(4, 4, 4, 8) });
        for (int ci = 0; ci < cats.Count; ci++) { var c = cats[ci]; nav.Children.Add(NavItem(ci, Loc.L(c.Name), c.Icon, Hex(c.Color), () => Progress(new[] { c }))); }
        StyleNav();

        // ---- toolbar
        var search = new TextBox { Width = 200 };
        var placeholder = T("Karte suchen …", 12.5, Faint).M(30, 0, 0, 0); placeholder.IsHitTestVisible = false;
        search.Padding = new Thickness(28, 6, 8, 6);
        search.TextChanged += (_, _) => { placeholder.Visibility = search.Text == "" ? Visibility.Visible : Visibility.Hidden; query = search.Text.Trim(); ApplyFilter(); };
        var searchHost = new Grid(); searchHost.Children.Add(search); searchHost.Children.Add(Icon("", 12, Dim).Left().M(10, 0, 0, 0)); searchHost.Children.Add(placeholder);
        var show = SegHost(
            Seg("Alle", "techshow", techShow == "all", () => { techShow = "all"; ApplyFilter(); }),
            Seg("Offen", "techshow", techShow == "open", () => { techShow = "open"; ApplyFilter(); }),
            Seg("Fertig", "techshow", techShow == "done", () => { techShow = "done"; ApplyFilter(); }));
        var totalText = Mono("", 12, Dim); var totalBar = new ContentControl { Focusable = false, Width = 110 };
        updaters.Add(() => { var (h, m) = Progress(cats); totalText.Text = Loc.L("{0} / {1} Ränge", h, m); totalBar.Content = Bar(m > 0 ? (double)h / m : 0, Hay, 6); });
        var bar = Box(Cols("a,a,*,a,a,a", searchHost, show.M(12, 0, 0, 0).VCenter(), null, totalBar.VCenter().M(0, 0, 10, 0), totalText.M(0, 0, 16, 0),
            H(Btn("Alle +1", () => SetAll((_, r, m) => Math.Min(m, r + 1), cats)).M(0, 0, 6, 0),
              Btn("Zurücksetzen", () => { if (Dlg.Confirm("Technik zurücksetzen", "Alle Karten auf Rang 0 setzen? (Die Start-Karte „Yard“ bleibt.)", "Zurücksetzen", true)) SetAll((id, _, _) => id == "yard" ? 1 : 0, cats); }).M(0, 0, 6, 0),
              Btn("Alles auf MAX", () => SetAll((_, _, m) => m, cats), "BtnAccent", ""))), pad: 12);

        RefreshAll(); ApplyFilter();
        var navScroll = new ScrollViewer { Content = nav, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false, Margin = new Thickness(26, 2, 0, 26) };
        var dock = new DockPanel();
        dock.Children.Add(new Border { Child = bar, Padding = new Thickness(26, 0, 26, 14) }.DockTo(System.Windows.Controls.Dock.Top));
        dock.Children.Add(Cols("236,*", navScroll, scroll));
        return dock;
    }

    // =====================================================================  DEPOT
    static string depotShow = "all";   // all | stocked
    static double depotFill = 500;

    public static FrameworkElement Depot(SaveGame s)
    {
        var st = s.State;
        var stock = st.Get("depot_stock")?.Dict;
        if (stock == null) return MainWindow.Empty("Kein Depot-Bestand", "Dieser Spielstand enthält noch keinen Depot-Block (älterer Spielstand).");
        var sampleKey = stock.Items.Count > 0 ? stock.Items[0].Key : null;
        var sampleVal = stock.Items.Count > 0 ? stock.Items[0].Value : null;
        int Get(string id) => (int)stock.Num(id);
        void Set(string id, int v)
        {
            v = Math.Clamp(v, 0, 1_000_000_000);
            if (v == 0) stock.Remove(id);
            else if (stock.Get(id) is { } g) g.Int = v;
            else stock.Items.Add(new(sampleKey != null && sampleKey.Type != GdVar.STRING ? new GdVar { Header = sampleKey.Header, Value = id } : GdVar.NewString(id), NewLike(sampleVal, v)));
            Dirty();
        }

        var ids = GameData.Goods.Select(g => g.Id).Concat(stock.Keys).Distinct().ToList();
        var depots = s.Buildings.Where(b => b["type"]?.Str == "depot").ToList();
        var updaters = new List<Action>();
        void RefreshAll() { foreach (var u in updaters) u(); }
        var cards = new List<(string id, FrameworkElement card)>();
        var groupSections = new List<(FrameworkElement section, List<string> ids)>();
        void ApplyFilter()
        {
            foreach (var (id, card) in cards) card.Visibility = depotShow == "all" || Get(id) > 0 ? Visibility.Visible : Visibility.Collapsed;
            foreach (var (section, gids) in groupSections) section.Visibility = gids.Any(i => depotShow == "all" || Get(i) > 0) ? Visibility.Visible : Visibility.Collapsed;
        }

        // ---- summary
        FrameworkElement Stat(string label, string icon, Brush color, Func<string> value, Func<string> hint)
        {
            var v = Head("", 22); var h = T("", 10.5, Faint, false, "MonoFont");
            updaters.Add(() => { v.Text = value(); h.Text = hint(); });
            var box = Box(V(new DockPanel { LastChildFill = false, Margin = new Thickness(0, 0, 0, 8), Children = { new Border { Width = 26, Height = 26, CornerRadius = new CornerRadius(7), Background = Alpha(color, 0.13), Child = Icon(icon, 13, color), Margin = new Thickness(0, 0, 9, 0) }, T(label, 12, Dim) } }, v, h.M(0, 3, 0, 0)), pad: 14);
            box.Width = 200; box.Margin = new Thickness(0, 0, 10, 10);
            return box;
        }
        var amber = Hex("#E8A15C");
        int ranksDepot = (int)(s.Ranks?.Num("depot") ?? 0), ranksDrone = (int)(s.Ranks?.Num("depot_drone") ?? 0), ranksShelf = (int)(s.Ranks?.Num("shelf_limits") ?? 0);
        var summary = Flow(
            Stat("Waren auf Lager", "", amber, () => ids.Count(i => Get(i) > 0).ToString(), () => Loc.L("von {0} Sorten", ids.Count)),
            Stat("Teile gesamt", "", Hay, () => Num(ids.Sum(i => (double)Get(i))), () => Loc.L("größter Posten: {0}", ids.Count == 0 ? "–" : GameData.GoodName(ids.OrderByDescending(Get).First()))),
            Stat("Depots gebaut", "", Steel, () => depots.Count.ToString(), () => Loc.L("{0} eingeschaltet", depots.Count(d => d["off"]?.Bool != true))),
            Box(V(T("Technik", 12, Dim).M(0, 0, 0, 10),
                Flow(Pill(Loc.L("Depot") + (ranksDepot > 0 ? " ✓" : " ✗"), ranksDepot > 0 ? Good : Faint).M(0, 0, 6, 6), Pill(Loc.L("Depot-Drohne") + (ranksDrone > 0 ? " ✓" : " ✗"), ranksDrone > 0 ? Good : Faint).M(0, 0, 6, 6), Pill(Loc.L("Regal-Limits") + (ranksShelf > 0 ? " ✓" : " ✗"), ranksShelf > 0 ? Good : Faint).M(0, 0, 6, 6)),
                T("Freischalten im Technik-Tab", 10.5, Faint).M(0, 2, 0, 0)), pad: 14).W(260).M(0, 0, 10, 10));

        // ---- toolbar
        var fillBox = NumBox(depotFill, v => depotFill = v, 0, 1, 1e9, 90);
        void FillAll() { foreach (var id in ids) if (Get(id) < depotFill) Set(id, (int)depotFill); RefreshAll(); ApplyFilter(); MainWindow.I.Toast(Loc.L("Alle Waren auf {0} aufgefüllt", Num(depotFill))); }
        var tools = Box(Cols("a,a,a,a,*,a,a",
            T("Auffüllen auf", 12, Dim).M(0, 0, 8, 0), fillBox.M(0, 0, 8, 0),
            Btn("Alle auffüllen", () => { Keyboard.ClearFocus(); FillAll(); }, "BtnAccent", "").M(0, 0, 6, 0),
            Btn("+100 je Ware auf Lager", () => { foreach (var id in ids) if (Get(id) > 0) Set(id, Get(id) + 100); RefreshAll(); }).M(0, 0, 6, 0), null,
            SegHost(Seg("Alle Waren", "depshow", depotShow == "all", () => { depotShow = "all"; ApplyFilter(); }), Seg("Nur auf Lager", "depshow", depotShow == "stocked", () => { depotShow = "stocked"; ApplyFilter(); })).VCenter().M(0, 0, 10, 0),
            Btn("Depot leeren", () => { if (!Dlg.Confirm("Depot leeren", "Alle Waren aus dem Depot entfernen?", "Leeren", true)) return; foreach (var id in ids) Set(id, 0); RefreshAll(); ApplyFilter(); }, "Btn", "")), pad: 12);

        // ---- goods, grouped
        var body = new StackPanel();
        foreach (var grp in GameData.GoodGroups)
        {
            var gids = ids.Where(i => GameData.GoodOf(i).Group == grp.De).ToList();
            if (gids.Count == 0) continue;
            var color = Hex(grp.Color);
            var wrap = new WrapPanel();
            foreach (var id in gids)
            {
                var name = T(GameData.GoodName(id), 13, Ui.Text, true);
                var badge = new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(9), Child = Icon(grp.Icon, 15, color) };
                var empty = Pill("leer", Faint);
                var box = NumBox(Get(id), v => { Set(id, (int)v); RefreshAll(); }, 0, 0, 1e9, 104);
                box.FontSize = 15; box.HorizontalContentAlignment = HorizontalAlignment.Left;
                var barHost = new ContentControl { Focusable = false };
                var buttons = H(Small("−10", () => { Set(id, Get(id) - 10); RefreshAll(); }, false).M(0, 0, 4, 0), Small("+10", () => { Set(id, Get(id) + 10); RefreshAll(); }, false).M(0, 0, 4, 0), Small("+100", () => { Set(id, Get(id) + 100); RefreshAll(); }, false));
                var card = Box(V(Cols("a,*,a", badge.M(0, 0, 10, 0), V(name, T(id, 10.5, Faint, false, "MonoFont")), empty.VCenter()),
                    Cols("*,a", box.Left(), buttons.VCenter()).M(0, 12, 0, 10), barHost), pad: 12);
                card.Width = 246; card.Margin = new Thickness(0, 0, 10, 10);
                updaters.Add(() =>
                {
                    int n = Get(id); double top = Math.Max(1, ids.Max(i => Get(i)));
                    if (!box.IsKeyboardFocusWithin) box.Text = Num(n);
                    name.Foreground = n > 0 ? Ui.Text : Dim;
                    badge.Background = Alpha(color, n > 0 ? 0.16 : 0.05);
                    empty.Visibility = n > 0 ? Visibility.Collapsed : Visibility.Visible;
                    barHost.Content = Bar(n / top, color, 4);
                    card.BorderBrush = n > 0 ? Alpha(color, 0.3) : Line;
                });
                cards.Add((id, card)); wrap.Children.Add(card);
            }
            var sum = Mono("", 11.5, Dim);
            updaters.Add(() => sum.Text = Loc.L("{0} Teile", Num(gids.Sum(i => (double)Get(i)))));
            var head = Cols("a,a,a,*", new Border { Width = 26, Height = 26, CornerRadius = new CornerRadius(7), Background = Alpha(color, 0.14), Child = Icon(grp.Icon, 12, color) }.M(0, 0, 10, 0), Head(Loc.L(grp.De), 15), sum.M(12, 0, 0, 0));
            var section = V(head.M(2, 8, 0, 10), wrap);
            groupSections.Add((section, gids)); body.Children.Add(section);
        }

        // ---- depots on the yard + acceptance rules
        var depotList = new StackPanel();
        if (depots.Count == 0) depotList.Children.Add(T("Noch kein Depot gebaut.", 12, Dim));
        for (int k = 0; k < depots.Count; k++)
        {
            var d = depots[k]; var p = d["position"]?.Floats;
            var off = d["off"];
            depotList.Children.Add(Cols("*,a,a", T(Loc.L("Depot {0}", k + 1), 12.5), Mono(p != null ? $"{p[0]:0.0} / {p[2]:0.0}" : "", 11.5, Dim).M(0, 0, 12, 0),
                off == null ? null : Switch("", !off.Bool, v => { off.Bool = !v; Dirty(); RefreshAll(); })).M(0, 0, 0, 6));
        }
        var depotCard = Card(Loc.L("Depots auf dem Hof · {0}", depots.Count), depotList, icon: "");

        var limits = st.Get("depot_limits")?.Dict; var refused = st.Get("depot_refused")?.List;
        int nLimits = (limits?.Items.Count ?? 0) + depots.Sum(d => d["limits"]?.Dict?.Items.Count ?? 0);
        int nRefused = (refused?.Count ?? 0) + depots.Sum(d => d["refused"]?.List?.Count ?? 0);
        var rules = V(T("Limits und abgelehnte Waren, die im Spiel an den Depots eingestellt sind. Aufheben lässt das Depot wieder alles annehmen.", 11.5, Dim).Wrap().M(0, 0, 0, 10),
            Cols("*,a", T("Gesetzte Limits", 12, Dim), Mono(nLimits.ToString(), 12)).M(0, 0, 0, 6),
            Cols("*,a", T("Abgelehnte Waren", 12, Dim), Mono(nRefused.ToString(), 12)).M(0, 0, 0, 6),
            nLimits + nRefused == 0 ? null : Flow((limits?.Items.Select(kv => (UIElement)Pill($"{GameData.GoodName(kv.Key.ToString())} ≤ {kv.Value}", amber).M(0, 0, 6, 6)) ?? Array.Empty<UIElement>())
                .Concat(refused?.Select(r => (UIElement)Pill(GameData.GoodName(r.ToString()) + " ✗", Bad).M(0, 0, 6, 6)) ?? Array.Empty<UIElement>()).ToArray()).M(0, 4, 0, 4),
            Btn("Alle Limits & Sperren aufheben", () =>
            {
                limits?.Items.Clear(); refused?.Clear();
                foreach (var d in depots) { d["limits"]?.Dict?.Items.Clear(); d["refused"]?.List?.Clear(); }
                Dirty(); Reload(); MainWindow.I.Toast(Loc.L("Depot nimmt wieder alles an"));
            }, "Btn", "").Left().M(0, 8, 0, 0));
        rules.Children.OfType<Button>().First().IsEnabled = nLimits + nRefused > 0;
        var rulesCard = Card("Annahme-Regeln", rules, icon: "");

        RefreshAll(); ApplyFilter();
        return Scroll(V(summary, tools.M(0, 4, 0, 14), body, Cols("*,18,*", depotCard, null, rulesCard).M(0, 10, 0, 0)));
    }
}
