using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using static FtnEditor.Ui;

namespace FtnEditor;

/// <summary>Minimal reader/writer for the scalar lines of Godot's profile.cfg (everything else is kept verbatim).</summary>
public sealed class ProfileCfg
{
    public string Path; public List<string> Lines = new();
    public static string DefaultPath => System.IO.Path.Combine(SaveGame.UserDir, "profile.cfg");
    public static ProfileCfg Load() { var p = new ProfileCfg { Path = DefaultPath }; if (File.Exists(p.Path)) p.Lines = File.ReadAllLines(p.Path).ToList(); return p; }

    (int start, int end) Section(string name)
    {
        int s = Lines.FindIndex(l => l.Trim() == $"[{name}]"); if (s < 0) return (-1, -1);
        int e = Lines.FindIndex(s + 1, l => l.StartsWith('[') && l.TrimEnd().EndsWith(']')); return (s + 1, e < 0 ? Lines.Count : e);
    }
    /// <summary>key → (line index, raw value, is multi-line block)</summary>
    public List<(string key, int line, string value)> Entries(string section)
    {
        var res = new List<(string, int, string)>(); var (s, e) = Section(section); if (s < 0) return res;
        for (int i = s; i < e; i++)
        {
            int eq = Lines[i].IndexOf('='); if (eq <= 0 || Lines[i].StartsWith(' ') || Lines[i].StartsWith('"') || Lines[i].StartsWith('}')) continue;
            string key = Lines[i][..eq], val = Lines[i][(eq + 1)..];
            if (val.TrimStart().StartsWith('{') && !val.TrimEnd().EndsWith('}'))
            {
                var sb = new StringBuilder(val); int depth = val.Count(c => c == '{') - val.Count(c => c == '}'); int j = i;
                while (depth > 0 && ++j < e) { sb.Append('\n').Append(Lines[j]); depth += Lines[j].Count(c => c == '{') - Lines[j].Count(c => c == '}'); }
                res.Add((key, -1, sb.ToString())); i = j;
            }
            else res.Add((key, i, val));
        }
        return res;
    }
    public void Set(int line, string key, string value) => Lines[line] = key + "=" + value;
    public void Save() { if (File.Exists(Path)) SaveGame.Backup(Path, "auto"); File.WriteAllLines(Path, Lines); }
}

public static partial class Pages
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // =====================================================================  CAREER
    public static FrameworkElement Career(SaveGame s)
    {
        var cfg = ProfileCfg.Load();
        var entries = cfg.Entries("career");
        if (entries.Count == 0) return MainWindow.Empty("Kein Karriere-Profil", Loc.L("profile.cfg wurde nicht gefunden oder enthält keinen [career]-Abschnitt.") + "\n" + cfg.Path);
        bool changed = false;
        var saveBtn = Btn("Profil speichern", null, "BtnAccent", ""); saveBtn.IsEnabled = false;
        saveBtn.Click += (_, _) => { try { cfg.Save(); changed = false; saveBtn.IsEnabled = false; MainWindow.I.Toast("profile.cfg gespeichert · Backup angelegt"); } catch (Exception ex) { Dlg.Info("Fehler", ex.Message); } };
        void Touch() { changed = true; saveBtn.IsEnabled = true; }

        var labels = new Dictionary<string, (string, string, string, bool)>
        {
            ["play_secs"] = ("Gesamte Spielzeit", "", "#B48CF2", true), ["hay_dug"] = ("Heu abgebaut", "", "#5CC8E8", false), ["needles_found"] = ("Nadeln gefunden", "", "#5CC8E8", false),
            ["money_earned"] = ("Geld verdient", "", "#F2B544", false), ["money_peak"] = ("Höchster Kontostand", "", "#F2B544", false), ["structures_built"] = ("Gebäude gebaut", "", "#6FD08C", false),
            ["best_income"] = ("Bestes Einkommen", "", "#6FD08C", false), ["first_needle_secs"] = ("Schnellste erste Nadel", "", "#B48CF2", true), ["belt_peak"] = ("Meiste Bänder", "", "#5CC8E8", false),
            ["best_dug"] = ("Bester Abbau", "", "#FF8F5C", false), ["epoch"] = ("Epoche", "", "#9AA0AE", false),
        };
        var tiles = new WrapPanel(); var flags = new WrapPanel();
        foreach (var (key, line, value) in entries)
        {
            if (line < 0) continue;
            if (value is "true" or "false")
            {
                string k = key;
                flags.Children.Add(Switch(Ui.Pretty(key), value == "true", v => { cfg.Set(line, k, v ? "true" : "false"); Touch(); }).M(0, 0, 24, 8)
                    .Tip(key == "debug_tainted" ? "Profil ist als „Debug benutzt“ markiert. Ausschalten macht es wieder sauber." : key));
            }
            else if (double.TryParse(value, NumberStyles.Float, Inv, out double num))
            {
                string k = key; bool isInt = !value.Contains('.');
                var (label, icon, color, isTime) = labels.GetValueOrDefault(key, (Ui.Pretty(key), "", "#9AA0AE", false));
                tiles.Children.Add(Tile(label, num, v => { cfg.Set(line, k, isInt ? ((long)v).ToString(Inv) : v.ToString("R", Inv)); Touch(); }, icon, Hex(color), isInt ? 0 : 2, isTime ? (Func<double, string>)Time : null, hint: isTime ? "Sekunden" : null));
            }
        }

        // records per pile size
        FrameworkElement sizeTable = null;
        var sb = entries.FirstOrDefault(e => e.key == "size_bests");
        if (sb.value != null)
            try
            {
                using var doc = JsonDocument.Parse(sb.value);
                var sizes = GameData.PileSizes; var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
                foreach (var _ in sizes) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                int row = 0; grid.RowDefinitions.Add(new RowDefinition());
                for (int c = 0; c < sizes.Length; c++) grid.Children.Add(Caps(GameData.PileName(sizes[c])).Col(c + 1).Right());
                foreach (var metric in doc.RootElement.EnumerateObject())
                {
                    row++; grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
                    bool isTime = metric.Name.EndsWith("_secs");
                    grid.Children.Add(T(labels.GetValueOrDefault(metric.Name, (Ui.Pretty(metric.Name), "", "", false)).Item1, 12.5, Dim).Row(row));
                    double rowMax = metric.Value.EnumerateObject().Select(p => p.Value.GetDouble()).DefaultIfEmpty(0).Max();
                    for (int c = 0; c < sizes.Length; c++)
                        if (metric.Value.TryGetProperty(sizes[c], out var v))
                            grid.Children.Add(Mono(isTime ? Time(v.GetDouble()) : Short(v.GetDouble()), 12.5, v.GetDouble() == rowMax && !isTime ? Hay : Ui.Text).Row(row).Col(c + 1).Right());
                        else grid.Children.Add(Mono("–", 12.5, Faint).Row(row).Col(c + 1).Right());
                }
                sizeTable = Card("Bestwerte je Haufengröße", grid, icon: "");
            }
            catch { }

        // daily history chart
        FrameworkElement daysCard = null;
        var days = new List<(string date, double play, double dug, double earned)>();
        foreach (var (key, _, value) in cfg.Entries("days"))
            try { using var d = JsonDocument.Parse(value); double G(string n) => d.RootElement.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0; days.Add((key, G("play_secs"), G("hay_dug"), G("money_earned"))); } catch { }
        if (days.Count > 0)
        {
            string metric = "play";
            var chartHost = new ContentControl { Focusable = false };
            void Draw()
            {
                Func<(string date, double play, double dug, double earned), double> sel = metric == "play" ? d => d.play : metric == "dug" ? d => d.dug : d => d.earned;
                var color = metric == "play" ? Hex("#B48CF2") : metric == "dug" ? Steel : Hay;
                double max = Math.Max(1e-9, days.Max(sel));
                var g = new Grid { Height = 170 };
                foreach (var d in days.TakeLast(31))
                {
                    g.ColumnDefinitions.Add(new ColumnDefinition());
                    double v = sel(d);
                    var bar = new Border { Background = color, CornerRadius = new CornerRadius(4, 4, 0, 0), Height = Math.Max(2, 120 * v / max), VerticalAlignment = VerticalAlignment.Bottom, MaxWidth = 46, Margin = new Thickness(4, 0, 4, 0) };
                    var col = Rows("*,a,a", bar, Mono(metric == "play" ? Time(v) : Short(v), 10.5, Ui.Text).M(0, 5, 0, 0), T(DateTime.TryParse(d.date, Inv, DateTimeStyles.None, out var dt) ? dt.ToString(Loc.De ? "dd.MM." : "MM-dd") : d.date, 10.5, Faint));
                    foreach (var t in col.Children.OfType<TextBlock>()) t.HorizontalAlignment = HorizontalAlignment.Center;
                    Grid.SetColumn(col, g.ColumnDefinitions.Count - 1); g.Children.Add(col);
                }
                chartHost.Content = g;
            }
            Draw();
            var seg = SegHost(Seg("Spielzeit", "cm", true, () => { metric = "play"; Draw(); }), Seg("Heu abgebaut", "cm", false, () => { metric = "dug"; Draw(); }), Seg("Geld verdient", "cm", false, () => { metric = "earned"; Draw(); }));
            daysCard = Card(Loc.L("Tagebuch · {0} Spieltage", days.Count), chartHost, seg, "");
        }

        var head = Box(Cols("a,*,a", Icon("", 16, Hay).M(0, 0, 12, 0), V(T("Karriere gilt für alle Slots und liegt in einer eigenen Datei.", 13, Ui.Text, true), T(cfg.Path, 11, Faint, false, "MonoFont")), Pill("nur lesen", Dim).VCenter()), pad: 14);
        // Career records feed the online leaderboards: shown, never written.
        tiles.IsHitTestVisible = false; flags.IsHitTestVisible = false; _ = changed;
        return Scroll(V(head, Caps("Rekorde · speisen die Bestenlisten, daher gesperrt").M(2, 20, 0, 10), tiles, flags.Children.Count > 0 ? Card("Flags", flags, icon: "").M(0, 4, 0, 0) : null, sizeTable?.M(0, 14, 0, 0), daysCard?.M(0, 14, 0, 0)));
    }

    // =====================================================================  BACKUPS
    public static FrameworkElement Backups(SaveGame s)
    {
        var files = new List<FileInfo>();
        try { if (Directory.Exists(SaveGame.BackupDir)) files.AddRange(new DirectoryInfo(SaveGame.BackupDir).GetFiles().OrderByDescending(f => f.LastWriteTime)); } catch { }
        try { files.AddRange(new DirectoryInfo(SaveGame.SaveDir).GetFiles("*.bak")); } catch { }
        string curName = Path.GetFileNameWithoutExtension(s.Path);

        var head = Box(Cols("a,*,a,a", Icon("", 16, Hay).M(0, 0, 12, 0),
            V(T("Vor jedem Speichern wird automatisch eine Kopie abgelegt.", 13, Ui.Text, true), T(SaveGame.BackupDir, 11, Faint, false, "MonoFont")),
            Btn("Ordner öffnen", () => { Directory.CreateDirectory(SaveGame.BackupDir); Process.Start(new ProcessStartInfo("explorer.exe", $"\"{SaveGame.BackupDir}\"") { UseShellExecute = true }); }, "Btn", "").VCenter().M(0, 0, 8, 0),
            Btn("Backup jetzt", () => { SaveGame.Backup(s.Path, "manuell"); MainWindow.I.Toast("Backup angelegt"); Reload(); }, "BtnAccent", "").VCenter()), pad: 14);

        var list = new StackPanel { Margin = new Thickness(0, 16, 0, 0) };
        if (files.Count == 0) list.Children.Add(T("Noch keine Backups vorhanden.", 12.5, Dim).M(2, 8, 0, 0));
        foreach (var f in files.Take(200))
        {
            bool profile = f.Name.StartsWith("profile"); bool gameBak = f.Extension == ".bak";
            string slot = f.Name.Split(new[] { "__", ".dat" }, StringSplitOptions.None)[0];
            bool mine = slot == curName;
            string tag = gameBak ? "Spiel-Backup" : f.Name.Contains("__manuell") ? "manuell" : f.Name.Contains("__vor-restore") ? "vor Wiederherstellung" : "automatisch";
            string info = "";
            if (!profile) try { var b = SaveGame.Load(f.FullName); info = Loc.L("{0} $ · {1} · {2} Karten", Short(b.State.Num("money")), Time(b.State.Num("run_secs")), b.Ranks?.Items.Count ?? 0); } catch { info = "nicht lesbar"; }
            var color = profile ? Hex("#B48CF2") : mine ? Hay : Dim;
            var restore = Btn("Wiederherstellen", () =>
            {
                string target = profile ? ProfileCfg.DefaultPath : Path.Combine(SaveGame.SaveDir, slot + ".dat");
                if (!Dlg.Confirm("Backup wiederherstellen", Loc.L("{0} wird durch dieses Backup vom {1} ersetzt. Der jetzige Stand wird vorher gesichert.", Path.GetFileName(target), Loc.Date(f.LastWriteTime)), "Wiederherstellen")) return;
                try
                {
                    if (File.Exists(target)) SaveGame.Backup(target, "vor-restore");
                    File.Copy(f.FullName, target, true);
                    MainWindow.I.Toast("Wiederhergestellt");
                    if (!profile && string.Equals(target, s.Path, StringComparison.OrdinalIgnoreCase)) MainWindow.I.LoadSlot(target); else MainWindow.I.LoadSlot(null);
                }
                catch (Exception ex) { Dlg.Info("Fehler", ex.Message); }
            }, "Btn", "");
            var del = Btn("", () => { if (Dlg.Confirm("Backup löschen", f.Name + " endgültig löschen?", "Löschen", true)) { try { f.Delete(); } catch { } Reload(); } }, "BtnGhost", "");
            var row = Box(Cols("a,150,*,a,a,a",
                new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(9), Background = Alpha(color, 0.13), Child = Icon(profile ? "" : "", 14, color) }.M(0, 0, 12, 0),
                V(T(profile ? "Karriere-Profil" : slot.StartsWith("slot_") && int.TryParse(slot[5..], out int n) ? $"Slot {n + 1}" : slot, 13, Ui.Text, true), T(f.LastWriteTime.ToString(Loc.De ? "dd.MM.yyyy · HH:mm:ss" : "yyyy-MM-dd · HH:mm:ss"), 11.5, Dim)),
                T(info, 12, Dim), Pill(tag, gameBak ? Steel : tag == "manuell" ? Hay : Dim).M(0, 0, 14, 0), restore.VCenter().M(0, 0, 4, 0), gameBak ? null : del.VCenter()), pad: 10);
            row.Margin = new Thickness(0, 0, 0, 6); row.BorderBrush = mine ? Alpha(Hay, 0.25) : Line;
            list.Children.Add(row);
        }
        return Scroll(V(head, list));
    }

    // =====================================================================  RAW DATA
    public static FrameworkElement Raw(SaveGame s)
    {
        var tree = new StackPanel();
        AddChildren(tree, s.Root, 0);
        var export = H(
            Btn("Als JSON exportieren", () => { var d = new Microsoft.Win32.SaveFileDialog { Filter = "JSON|*.json", FileName = Path.GetFileNameWithoutExtension(s.Path) + ".json" }; if (d.ShowDialog() == true) { File.WriteAllText(d.FileName, ToJson(s.Root)); MainWindow.I.Toast("JSON exportiert"); } }, "Btn", "").M(0, 0, 8, 0),
            Btn("Entschlüsselt exportieren (.bin)", () => { var d = new Microsoft.Win32.SaveFileDialog { Filter = "Godot-Variant|*.bin", FileName = Path.GetFileNameWithoutExtension(s.Path) + ".bin" }; if (d.ShowDialog() == true) { File.WriteAllBytes(d.FileName, s.Root.ToFile()); MainWindow.I.Toast(".bin exportiert"); } }, "Btn", "").M(0, 0, 8, 0),
            Btn(".bin importieren", () =>
            {
                var d = new Microsoft.Win32.OpenFileDialog { Filter = "Godot-Variant|*.bin|" + Loc.L("Alle Dateien") + "|*.*" };
                if (d.ShowDialog() != true) return;
                try { s.Root = GdVar.FromFile(File.ReadAllBytes(d.FileName)); _ = s.State; Dirty(); Reload(); MainWindow.I.Toast("Importiert – noch nicht gespeichert"); } catch (Exception ex) { Dlg.Info("Import fehlgeschlagen", ex.Message); }
            }, "Btn", ""));
        var head = Box(Cols("a,*,a", Icon("", 16, Hay).M(0, 0, 12, 0), V(T("Alles, was im Spielstand steht.", 13, Ui.Text, true), T("Zahlen, Texte und Schalter lassen sich direkt ändern. Für Mutige.", 11.5, Dim)), export.VCenter()), pad: 14);
        var dock = new DockPanel();
        dock.Children.Add(new Border { Child = head, Padding = new Thickness(26, 0, 26, 12) }.DockTo(System.Windows.Controls.Dock.Top));
        dock.Children.Add(Scroll(Box(tree, pad: 10)));
        return dock;
    }

    static void AddChildren(Panel parent, GdVar node, int depth, int from = 0)
    {
        const int Page = 200;
        var kids = new List<(string key, GdVar val, Action<string> setInline)>();
        if (node.Type == GdVar.DICT) foreach (var kv in node.Dict.Items) kids.Add((kv.Key.ToString(), kv.Value, null));
        else if (node.Type == GdVar.ARRAY) for (int i = 0; i < node.List.Count; i++) kids.Add(($"[{i}]", node.List[i], null));
        else if (node.Value is Array arr)
        {
            int per = node.Type == GdVar.P_V3 ? 3 : node.Type == GdVar.P_V2 ? 2 : node.Type is GdVar.P_COLOR or GdVar.P_V4 ? 4 : 1;
            for (int i = 0; i < arr.Length; i++)
            {
                int idx = i; string label = per > 1 ? $"[{i / per}].{"xyzw"[i % per]}" : $"[{i}]";
                kids.Add((label, null, v =>
                {
                    switch (arr)
                    {
                        case byte[] b: if (byte.TryParse(v, out var bv)) b[idx] = bv; break;
                        case int[] ia: if (int.TryParse(v, out var iv)) ia[idx] = iv; break;
                        case long[] la: if (long.TryParse(v, out var lv)) la[idx] = lv; break;
                        case float[] fa: if (TryParse(v, out var fv)) fa[idx] = (float)fv; break;
                        case double[] da: if (TryParse(v, out var dv)) da[idx] = dv; break;
                        case string[] sa: sa[idx] = v.TrimEnd('\0') + (sa[idx].EndsWith('\0') ? "\0" : ""); break;
                    }
                }));
            }
        }
        int end = Math.Min(kids.Count, from + Page);
        for (int i = from; i < end; i++)
        {
            var (key, val, setInline) = kids[i];
            if (val == null)
            {
                var arr = (Array)node.Value; object o = arr.GetValue(i);
                string text = o is float f ? f.ToString("R", Inv) : o is double d ? d.ToString("R", Inv) : o is string str ? str.TrimEnd('\0') : Convert.ToString(o, Inv);
                parent.Children.Add(RawRow(depth, key, null, null, text, v => { setInline(v); Dirty(); }));
            }
            else parent.Children.Add(RawNode(key, val, depth));
        }
        if (end < kids.Count)
        {
            Button more = null;
            more = Btn(Loc.L("weitere {0} von {1} anzeigen", Math.Min(Page, kids.Count - end), kids.Count - end), () => { parent.Children.Remove(more); AddChildren(parent, node, depth, end); }, "BtnGhost", "");
            more.Foreground = Steel; more.HorizontalAlignment = HorizontalAlignment.Left; more.Margin = new Thickness(depth * 18 + 22, 2, 0, 2);
            parent.Children.Add(more);
        }
    }

    static FrameworkElement RawNode(string key, GdVar v, int depth)
    {
        bool container = v.Type is GdVar.DICT or GdVar.ARRAY || (v.Value is Array && v.Value is not double[] { Length: <= 4 } && v.Value is not long[] { Length: <= 4 });
        if (!container)
        {
            if (key != null && (key.StartsWith("board_") || key is "debug_used" or "run_timed")) return RawRow(depth, key, v, null, v.ToString() + "   · " + Loc.L("gesperrt (Bestenliste)"), null);
            if (v.Type == GdVar.BOOL) return RawRow(depth, key, v, null, null, null, Switch("", v.Bool, b => { v.Bool = b; Dirty(); }));
            if (v.Value is double[] da)
            {
                var boxes = new StackPanel { Orientation = Orientation.Horizontal };
                for (int i = 0; i < da.Length; i++) { int idx = i; var tb = RawBox(da[i].ToString("0.####", Inv), t => { if (TryParse(t, out var d)) { da[idx] = d; Dirty(); } }, 74); tb.Margin = new Thickness(0, 0, 4, 0); boxes.Children.Add(tb); }
                return RawRow(depth, key, v, null, null, null, boxes);
            }
            if (v.Type == GdVar.NIL || v.Value is long[]) return RawRow(depth, key, v, null, v.ToString(), null);
            return RawRow(depth, key, v, null, v.ToString(), t =>
            {
                if (v.Type is GdVar.STRING or GdVar.STRING_NAME) v.Str = t;
                else if (TryParse(t, out var d)) v.Num = d;
                Dirty();
            });
        }
        var kidsHost = new StackPanel { Visibility = Visibility.Collapsed };
        bool loaded = false;
        var chevron = Icon("", 9, Dim); chevron.Width = 16;
        int count = v.Type == GdVar.DICT ? v.Dict.Items.Count : v.Type == GdVar.ARRAY ? v.List.Count : ((Array)v.Value).Length;
        var row = RawRow(depth, key, v, chevron, null, null, T(count == 0 ? "leer" : Loc.L("{0} Einträge", count), 11.5, Faint));
        row.Cursor = Cursors.Hand;
        row.MouseLeftButtonUp += (_, e) =>
        {
            if (e.OriginalSource is TextBox) return;
            if (!loaded) { loaded = true; AddChildren(kidsHost, v, depth + 1); }
            bool open = kidsHost.Visibility != Visibility.Visible;
            kidsHost.Visibility = open ? Visibility.Visible : Visibility.Collapsed; chevron.Text = open ? "" : "";
        };
        return V(row, kidsHost);
    }

    static TextBox RawBox(string text, Action<string> commit, double width)
    {
        var tb = new TextBox { Text = text, Width = width, FontFamily = Font("MonoFont"), FontSize = 12, Padding = new Thickness(6, 2, 6, 2) };
        string last = text;
        tb.LostKeyboardFocus += (_, _) => { if (tb.Text != last) { last = tb.Text; commit(tb.Text); } };
        tb.KeyDown += (_, e) => { if (e.Key == Key.Enter) Keyboard.ClearFocus(); };
        return tb;
    }

    static Border RawRow(int depth, string key, GdVar v, UIElement chevron, string text, Action<string> commit, UIElement custom = null)
    {
        Brush typeColor = v == null ? Faint : v.Type switch { GdVar.INT or GdVar.FLOAT => Steel, GdVar.STRING or GdVar.STRING_NAME => Good, GdVar.BOOL => Hay, GdVar.DICT or GdVar.ARRAY => Hex("#B48CF2"), _ => Dim };
        UIElement right = custom ?? (commit != null ? RawBox(text, commit, Math.Clamp((text?.Length ?? 4) * 7.4 + 26, 90, 420)) : Mono(text ?? "", 12, Dim));
        var g = Cols("a,a,a,*", chevron ?? new Border { Width = 16 }, Mono(key, 12.5, key.StartsWith('[') ? Dim : Ui.Text).M(2, 0, 10, 0), v == null ? null : T(v.TypeName, 10.5, typeColor).M(0, 0, 12, 0), ((FrameworkElement)right).Left());
        var b = new Border { Child = g, Padding = new Thickness(depth * 18 + 6, 3, 6, 3), CornerRadius = new CornerRadius(5), Background = Brushes.Transparent, MinHeight = 28 };
        b.MouseEnter += (_, _) => b.Background = Hex("#20232B"); b.MouseLeave += (_, _) => b.Background = Brushes.Transparent;
        return b;
    }

    public static string ToJson(GdVar root)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
            WriteJson(w, root);
        return Encoding.UTF8.GetString(ms.ToArray());
    }
    static void WriteNum(Utf8JsonWriter w, double d) { if (double.IsFinite(d)) w.WriteNumberValue(d); else w.WriteStringValue(d.ToString(Inv)); }
    static void WriteJson(Utf8JsonWriter w, GdVar v)
    {
        switch (v.Value)
        {
            case null: w.WriteNullValue(); break;
            case bool b: w.WriteBooleanValue(b); break;
            case long l: w.WriteNumberValue(l); break;
            case double d: WriteNum(w, d); break;
            case string s: w.WriteStringValue(s); break;
            case GdDict dict: w.WriteStartObject(); foreach (var kv in dict.Items) { w.WritePropertyName(kv.Key.ToString()); WriteJson(w, kv.Value); } w.WriteEndObject(); break;
            case List<GdVar> list: w.WriteStartArray(); foreach (var x in list) WriteJson(w, x); w.WriteEndArray(); break;
            case byte[] a: w.WriteStartArray(); foreach (var x in a) w.WriteNumberValue(x); w.WriteEndArray(); break;
            case int[] a: w.WriteStartArray(); foreach (var x in a) w.WriteNumberValue(x); w.WriteEndArray(); break;
            case long[] a: w.WriteStartArray(); foreach (var x in a) w.WriteNumberValue(x); w.WriteEndArray(); break;
            case float[] a: w.WriteStartArray(); foreach (var x in a) WriteNum(w, x); w.WriteEndArray(); break;
            case double[] a: w.WriteStartArray(); foreach (var x in a) WriteNum(w, x); w.WriteEndArray(); break;
            case string[] a: w.WriteStartArray(); foreach (var x in a) w.WriteStringValue(x.TrimEnd('\0')); w.WriteEndArray(); break;
            default: w.WriteStringValue(v.ToString()); break;
        }
    }
}
