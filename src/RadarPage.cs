using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FtnEditor;

/// <summary>Haystack radar: top view, 3D and 3D x-ray, plus tools that act on the pile.</summary>
public sealed class RadarPage : Grid, IDisposable
{
    readonly SaveGame s;
    readonly TopView top = new();
    readonly Pile3D view3d = new();
    readonly ContentControl side = new() { Focusable = false };
    readonly TextBlock hover = Ui.Mono("", 11.5, Ui.Dim);
    readonly TextBlock hint = Ui.T("", 11, Ui.Faint);
    static string mode = "top";          // remembered while the app runs
    static bool showBuildings = true, showBelts = true, showProps = true;
    int selected = -1;
    public static void ForceMode(string m) => mode = m;

    public RadarPage(SaveGame save)
    {
        s = save;
        if (s.Heights == null || s.Grid < 2) { Children.Add(MainWindow.Empty("Kein Heuhaufen", "Dieser Spielstand enthält keine Höhenkarte.")); return; }
        Margin = new Thickness(26, 0, 26, 22);
        ColumnDefinitions.Add(new ColumnDefinition()); ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(318) });

        top.S = s;
        top.NeedleClicked += Select; view3d.NeedleClicked += Select;
        top.Hover += t => hover.Text = t;

        var segs = Ui.SegHost(
            Ui.Seg("Draufsicht", "rm", mode == "top", () => SetMode("top")),
            Ui.Seg("3D", "rm", mode == "3d", () => SetMode("3d")),
            Ui.Seg("3D Röntgen", "rm", mode == "xray", () => SetMode("xray")));
        var layers = new Border
        {
            Background = Ui.Hex("#101216", 0.86), CornerRadius = new CornerRadius(8), BorderBrush = Ui.Line, BorderThickness = new Thickness(1), Padding = new Thickness(3),
            Child = Ui.H(
                Ui.Btn("", () => { if (mode is "3d" or "xray") view3d.ResetCamera(s); else top.Fit(); }, "BtnGhost", "").Tip("Ansicht zentrieren"),
                Ui.Btn("", () => { if (mode == "top") top.Fit(true); }, "BtnGhost", "").Tip("Ganzen Hof zeigen (Draufsicht)"))
        };
        var toolbar = new DockPanel { Margin = new Thickness(12), VerticalAlignment = VerticalAlignment.Top, LastChildFill = false };
        DockPanel.SetDock(layers, Dock.Right); toolbar.Children.Add(layers); toolbar.Children.Add(segs);
        var footer = new Border { Background = Ui.Hex("#101216", 0.86), CornerRadius = new CornerRadius(7), Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(12), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Child = Ui.H(hover, hint), IsHitTestVisible = false };

        var stage = new Grid();
        stage.Children.Add(top); stage.Children.Add(view3d); stage.Children.Add(toolbar); stage.Children.Add(footer);
        var frame = new Border { CornerRadius = new CornerRadius(12), BorderBrush = Ui.Line, BorderThickness = new Thickness(1), Background = Ui.Hex("#0A0B0E"), Child = stage, ClipToBounds = true };
        void Clip() => stage.Clip = new RectangleGeometry(new Rect(0, 0, stage.ActualWidth, stage.ActualHeight), 11, 11);
        stage.SizeChanged += (_, _) => Clip();
        Children.Add(frame);

        var scroller = new ScrollViewer { Content = side, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false, Margin = new Thickness(16, 0, 0, 0) };
        SetColumn(scroller, 1); Children.Add(scroller);

        SetMode(mode);
    }

    public void Dispose() => top.Stop();

    void SetMode(string m)
    {
        mode = m; bool is3d = m is "3d" or "xray";
        top.Visibility = is3d ? Visibility.Collapsed : Visibility.Visible;
        view3d.Visibility = is3d ? Visibility.Visible : Visibility.Collapsed;
        top.Radar = false;
        hover.Visibility = is3d ? Visibility.Collapsed : Visibility.Visible;
        hint.Text = is3d ? "Ziehen: drehen · Rechts ziehen: verschieben · Rad: Zoom · Klick: Nadel wählen" : "   Ziehen: verschieben · Rad: Zoom";
        Redraw();
    }

    void Redraw()
    {
        top.Selected = selected; top.ShowBuildings = showBuildings; top.ShowBelts = showBelts; top.ShowProps = showProps;
        if (mode is "3d" or "xray") view3d.Build(s, mode == "xray", showBuildings, showBelts, showProps, selected);
        else top.Rebuild();
        BuildSide();
    }

    void Select(int i) { selected = i; Redraw(); }
    void Changed(string toast) { MainWindow.I.MarkDirty(); Redraw(); if (toast != null) MainWindow.I.Toast(toast); }

    void BuildSide()
    {
        int n = NeedleOps.Count(s); var pl = PileRender.Player(s);
        var list = new StackPanel();
        for (int i = 0; i < n; i++)
        {
            int idx = i; bool found = NeedleOps.IsFound(s, i); var (x, y, z) = NeedleOps.Pos(s, i); double depth = NeedleOps.Depth(s, i);
            var col = new SolidColorBrush(GameData.NeedleColor(NeedleOps.TypeOf(s, i)));
            double distP = Math.Sqrt(Math.Pow(x - pl.x, 2) + Math.Pow(z - pl.z, 2));
            var icon = new Border { Width = 30, Height = 30, CornerRadius = new CornerRadius(8), Background = Ui.Alpha(col, found ? 0.06 : 0.16), Child = Pages.NeedleGlyph(found ? Ui.Faint : col, 16) };
            var row = new Border
            {
                Padding = new Thickness(8, 7, 10, 7), CornerRadius = new CornerRadius(8), Margin = new Thickness(0, 0, 0, 4), Cursor = Cursors.Hand,
                Background = i == selected ? Ui.Bg3 : Brushes.Transparent, BorderThickness = new Thickness(1), BorderBrush = i == selected ? Ui.Alpha(col, 0.6) : Brushes.Transparent,
                Child = Ui.Cols("a,*,a", icon.M(0, 0, 10, 0),
                    Ui.V(Ui.T($"Nadel {i + 1}", 12.5, found ? Ui.Dim : Ui.Text, true), Ui.T(found ? "gefunden" : depth <= 0.02 ? "liegt frei" : $"{depth:0.00} m tief · Höhe {y:0.0} m", 11, found ? Ui.Faint : Ui.Dim)),
                    Ui.Mono($"{distP:0.0} m", 11.5, Ui.Dim).Tip("Entfernung zum Spieler"))
            };
            row.MouseLeftButtonUp += (_, _) => Select(idx == selected ? -1 : idx);
            list.Children.Add(row);
        }
        if (n == 0) list.Children.Add(Ui.T("In diesem Haufen sind keine Nadeln eingetragen.", 12, Ui.Dim).Wrap());

        // selection actions
        UIElement sel;
        if (selected >= 0 && selected < n)
        {
            int i = selected; bool found = NeedleOps.IsFound(s, i);
            Button Wide(string text, string glyph, Action a, string style = "Btn") { var b = Ui.Btn(text, a, style, glyph); b.HorizontalContentAlignment = HorizontalAlignment.Left; b.Margin = new Thickness(0, 0, 0, 6); b.Padding = new Thickness(12, 8, 12, 8); return b; }
            sel = Ui.Card($"Nadel {i + 1} · {GameData.NeedleName(NeedleOps.TypeOf(s, i))}", Ui.V(
                Wide("Freilegen", "", () => { NeedleOps.Excavate(s, i); Changed("Trichter bis zur Nadel gegraben"); }, "BtnAccent").Tip("Gräbt einen Trichter ins Heu, bis die Nadel offen liegt"),
                Wide("Spieler hierher stellen", "", () => { var p = NeedleOps.Pos(s, i); NeedleOps.TeleportPlayer(s, p.x, p.z); Changed("Spieler steht jetzt über der Nadel"); }),
                Wide(found ? "Zurück ins Heu legen" : "Als gefunden markieren", found ? "" : "", () => { NeedleOps.SetFound(s, i, !found); Changed(null); })), icon: "");
        }
        else sel = Ui.Box(Ui.T("Nadel in der Liste oder in der Ansicht anklicken, um sie freizulegen, den Spieler hinzustellen oder sie als gefunden zu markieren.", 11.5, Ui.Dim).Wrap(), pad: 14);

        // pile tools
        var h = s.Heights; double sum = s.HeightSum(), vol = sum * s.Cell * s.Cell;
        double pct = 75;
        var pctText = Ui.Mono("75 %", 12, Ui.Dim);
        var slider = new Slider { Minimum = 0, Maximum = 100, Value = 75, TickFrequency = 5, IsSnapToTickEnabled = true };
        slider.ValueChanged += (_, e) => { pct = e.NewValue; pctText.Text = $"{pct:0} %"; };
        var stats = Ui.V(
            Ui.Cols("*,a", Ui.T("Höchster Punkt", 12, Ui.Dim), Ui.Mono($"{(h.Length > 0 ? h.Max() : 0):0.00} m", 12)).M(0, 0, 0, 5),
            Ui.Cols("*,a", Ui.T("Volumen", 12, Ui.Dim), Ui.Mono($"{Ui.Num(vol)} m³", 12)).M(0, 0, 0, 5),
            Ui.Cols("*,a", Ui.T("Halme", 12, Ui.Dim), Ui.Mono(Ui.Short(s.State.Num("hay_total")), 12)).M(0, 0, 0, 12),
            Ui.Cols("*,a", Ui.T("Haufenhöhe setzen auf", 12, Ui.Dim), pctText).M(0, 0, 0, 4), slider,
            Ui.Btn("Höhe anwenden", () => { double before = s.HeightSum(); float f = (float)(pct / 100); for (int i = 0; i < h.Length; i++) h[i] *= f; s.ApplyVolumeChange(before); Changed($"Haufen auf {pct:0} % skaliert"); }, "Btn", "").M(0, 8, 0, 6),
            s.Dome != null && s.Dome.Length == h.Length ? Ui.Btn("Ursprungsform wiederherstellen", () =>
            {
                Array.Copy(s.Dome, h, h.Length); s.State.SetNum("hay_total", s.State.Num("hay_initial")); Changed("Haufen ist wieder voll");
            }, "Btn", "").Tip("Setzt die Höhenkarte auf die Form vom Spielstart zurück").M(0, 0, 0, 6) : null,
            Ui.Btn("Alles Heu entfernen", () =>
            {
                if (!Dlg.Confirm("Alles Heu entfernen", "Der komplette Haufen wird abgetragen – alle Nadeln liegen danach offen auf dem Boden.", "Entfernen", true)) return;
                Array.Clear(h); s.State.SetNum("hay_total", 0); Changed("Haufen abgetragen");
            }, "Btn", ""));
        foreach (var b in stats.Children.OfType<Button>()) b.HorizontalAlignment = HorizontalAlignment.Stretch;

        var layerBox = Ui.Box(Ui.Cols("a,*,a,*,a",
            Ui.Switch("Gebäude", showBuildings, v => { showBuildings = v; Redraw(); }), null,
            Ui.Switch("Bänder", showBelts, v => { showBelts = v; Redraw(); }), null,
            Ui.Switch("Objekte", showProps, v => { showProps = v; Redraw(); })), pad: 10);
        side.Content = Ui.V(layerBox.M(0, 0, 0, 12), Ui.Card($"Nadeln · {Enumerable.Range(0, n).Count(i => !NeedleOps.IsFound(s, i))} noch im Heu", list, icon: ""), ((FrameworkElement)sel).M(0, 12, 0, 12), Ui.Card("Haufen-Werkzeuge", stats, icon: ""));
    }
}
