using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shell;
using System.Windows.Threading;
using static FtnEditor.Ui;

namespace FtnEditor;

public static class Chrome
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    /// <summary>Borderless dark window: no system frame, no light edge, rounded on Windows 11.</summary>
    public static void Apply(Window w, double caption)
    {
        w.WindowStyle = WindowStyle.None;
        w.Background = Bg0;
        w.UseLayoutRounding = true;
        TextOptions.SetTextFormattingMode(w, TextFormattingMode.Display);
        WindowChrome.SetWindowChrome(w, new WindowChrome { CaptionHeight = caption, ResizeBorderThickness = new Thickness(w.ResizeMode == ResizeMode.NoResize ? 0 : 6), GlassFrameThickness = new Thickness(0), CornerRadius = new CornerRadius(0), UseAeroCaptionButtons = false });
        w.SourceInitialized += (_, _) =>
        {
            IntPtr h = new WindowInteropHelper(w).Handle;
            int dark = 1, round = 2, border = 0x00110E0D, caption2 = 0x00110E0D; // COLORREF 0x00BBGGRR = #0D0E11
            DwmSetWindowAttribute(h, 20, ref dark, 4);     // DWMWA_USE_IMMERSIVE_DARK_MODE
            DwmSetWindowAttribute(h, 33, ref round, 4);    // DWMWA_WINDOW_CORNER_PREFERENCE
            DwmSetWindowAttribute(h, 34, ref border, 4);   // DWMWA_BORDER_COLOR
            DwmSetWindowAttribute(h, 35, ref caption2, 4); // DWMWA_CAPTION_COLOR
        };
    }
}

public static class Dlg
{
    static bool Show(string title, string text, string ok, string cancel, bool danger = false)
    {
        var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(x => x.IsActive) ?? Application.Current.MainWindow;
        var w = new Window { Width = 440, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, Title = title };
        if (owner != null && owner.IsVisible) { w.Owner = owner; w.WindowStartupLocation = WindowStartupLocation.CenterOwner; } else w.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Chrome.Apply(w, 36);
        bool result = false;
        var okBtn = Btn(ok, () => { result = true; w.Close(); }, "BtnAccent"); okBtn.MinWidth = 96; okBtn.IsDefault = true;
        var buttons = H(cancel == null ? null : Btn(cancel, () => w.Close()).M(0, 0, 8, 0), okBtn).Right();
        if (cancel != null) ((Button)buttons.Children[0]).IsCancel = true; else okBtn.IsCancel = true;
        w.Content = new Border
        {
            BorderBrush = Bg4, BorderThickness = new Thickness(1), Padding = new Thickness(24, 20, 24, 20),
            Child = V(H(Icon(danger ? "" : "", 16, danger ? Bad : Hay).M(0, 0, 10, 0), Head(title, 16)), T(text, 12.5, Dim).Wrap().M(0, 12, 0, 22), buttons)
        };
        w.ShowDialog();
        return result;
    }
    public static bool Confirm(string title, string text, string ok = "OK", bool danger = false) => Show(title, text, ok, "Abbrechen", danger);
    public static void Info(string title, string text) => Show(title, text, "OK", null);
}

public sealed class MainWindow : Window
{
    public static MainWindow I;
    public SaveGame Save;

    sealed record Page(string Key, string Title, string Sub, string Icon, Func<SaveGame, FrameworkElement> Build);
    readonly List<Page> pages;
    Page current;
    readonly ContentControl host = new() { Focusable = false };
    readonly TextBlock pageTitle = Head("", 22), pageSub = T("", 12, Dim);
    readonly StackPanel slotPanel = new();
    readonly TextBlock dirtyText = T("", 11.5, Dim);
    readonly Border toast = new() { Visibility = Visibility.Collapsed };
    readonly Border gameWarn;
    readonly Grid root = new();
    Button saveBtn;
    readonly DispatcherTimer watch = new() { Interval = TimeSpan.FromSeconds(3) };

    public MainWindow()
    {
        I = this;
        Title = "Find The Needle – Save Editor";
        Width = 1320; Height = 840; MinWidth = 1060; MinHeight = 640;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        App.Trace("ctor a");
        Chrome.Apply(this, 42);
        App.Trace("ctor b");
        FontFamily = Font("UiFont"); Foreground = Ui.Text;
        try { Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri("pack://application:,,,/app.ico")); } catch { }

        pages = new()
        {
            new("run", "Lauf & Geld", "Kontostand, Rekorde, Heuhaufen und Lauf-Flags", "", s => Pages.Run(s)),
            new("needles", "Nadeln", "Die im Haufen versteckten Nadeln und deine Sammlung", "", s => Pages.Needles(s)),
            new("radar", "Haystack-Radar", "Draufsicht, 3D und Röntgenblick auf den Heuhaufen", "", s => new RadarPage(s)),
            new("tech", "Technik", "Alle Karten mit Rängen – einzeln oder alles auf MAX", "", s => Pages.Tech(s)),
            new("gear", "Ausrüstung", "Werkzeuge, Bau-Leiste, Geschenke", "", s => Pages.Gear(s)),
            new("buildings", "Gebäude", "Maschinen ein/aus, Treibstoff, Lager", "", s => Pages.Buildings(s)),
            new("missions", "Missionen", "Aktuelle Mission, Aufträge und Ausgaben-Analyse", "", s => Pages.Missions(s)),
            new("career", "Karriere", "Rekorde über alle Läufe aus profile.cfg", "", s => Pages.Career(s)),
            new("backups", "Backups", "Automatische Sicherung vor jedem Speichern", "", s => Pages.Backups(s)),
            new("raw", "Rohdaten", "Der komplette Spielstand als Baum – jeder Wert editierbar", "", s => Pages.Raw(s)),
        };

        App.Trace("ctor c");
        // ----- title bar -----
        var logo = new Border { Width = 22, Height = 22, CornerRadius = new CornerRadius(6), Background = Hay, Child = new System.Windows.Shapes.Line { X1 = 5, Y1 = 17, X2 = 17, Y2 = 5, Stroke = Hex("#14100A"), StrokeThickness = 2, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round } };
        var title = H(logo.M(14, 0, 10, 0), T("FIND THE NEEDLE", 12, Ui.Text, true, "HeadFont"), T("Save Editor", 12, Dim).M(8, 0, 0, 0));
        title.VerticalAlignment = VerticalAlignment.Center;
        Button Cap(string glyph, Action a, string hover = "#232731")
        {
            var b = new Button { Style = (Style)FindResource("BtnCaption"), Content = glyph, Tag = Hex(hover) };
            b.Click += (_, _) => a(); WindowChrome.SetIsHitTestVisibleInChrome(b, true); return b;
        }
        Button maxBtn = null;
        maxBtn = Cap("", () => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized);
        var caps = H(Cap("", () => WindowState = WindowState.Minimized), maxBtn, Cap("", Close, "#C42B1C")).Right();
        var titleBar = new Grid { Height = 42, Background = Bg0 };
        titleBar.Children.Add(title); titleBar.Children.Add(caps);
        StateChanged += (_, _) => { root.Margin = new Thickness(WindowState == WindowState.Maximized ? 7 : 0); maxBtn.Content = WindowState == WindowState.Maximized ? "" : ""; };

        App.Trace("ctor d");
        // ----- sidebar -----
        var nav = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
        foreach (var p in pages)
        {
            var content = H(Icon(p.Icon, 14).W(22).M(0, 0, 10, 0), new TextBlock { Text = p.Title, VerticalAlignment = VerticalAlignment.Center });
            ((TextBlock)content.Children[0]).SetBinding(TextBlock.ForegroundProperty, new System.Windows.Data.Binding("Foreground") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor, typeof(RadioButton), 1) });
            var rb = new RadioButton { Style = (Style)FindResource("Nav"), Content = content, GroupName = "nav", Tag = p };
            rb.Checked += (_, _) => Navigate(p);
            nav.Children.Add(rb);
        }
        saveBtn = Btn("Speichern", DoSave, "BtnAccent", ""); saveBtn.Padding = new Thickness(12, 9, 12, 9);
        saveBtn.Tip("Strg+S · legt vorher automatisch ein Backup an");
        var reload = Btn("", () => { if (ConfirmDiscard()) LoadSlot(Save?.Path); }, "Btn", "").Tip("Spielstand neu von der Platte laden"); reload.Padding = new Thickness(10, 9, 10, 9);
        gameWarn = new Border
        {
            Background = Alpha(Bad, 0.1), BorderBrush = Alpha(Bad, 0.35), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 8, 10, 8), Margin = new Thickness(12, 0, 12, 10), Visibility = Visibility.Collapsed,
            Child = Cols("a,*", Ui.Icon("", 13, Bad).M(0, 0, 8, 0), T("Spiel läuft. Erst zum Hauptmenü oder beenden – sonst überschreibt es deine Änderungen.", 11, Ui.Text).Wrap())
        };
        var bottom = V(gameWarn, dirtyText.M(14, 0, 14, 8), Cols("*,a", saveBtn, reload.M(8, 0, 0, 0)).M(12, 0, 12, 14));
        var side = new DockPanel { Background = Bg1, Width = 232 };
        side.Children.Add(bottom.DockTo(System.Windows.Controls.Dock.Bottom));
        side.Children.Add(V(Caps("Spielstand").M(18, 14, 0, 8), slotPanel).DockTo(System.Windows.Controls.Dock.Top));
        side.Children.Add(new ScrollViewer { Content = V(Caps("Editor").M(18, 14, 0, 2), nav), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false });

        App.Trace("ctor e");
        // ----- content -----
        var header = V(pageTitle, pageSub.M(0, 3, 0, 0)).M(26, 14, 26, 14);
        var main = new DockPanel { Background = Bg0 };
        main.Children.Add(header.DockTo(System.Windows.Controls.Dock.Top));
        main.Children.Add(host);

        toast.Background = Hex("#20242D"); toast.BorderBrush = Bg4; toast.BorderThickness = new Thickness(1); toast.CornerRadius = new CornerRadius(9);
        toast.Padding = new Thickness(16, 10, 16, 10); toast.HorizontalAlignment = HorizontalAlignment.Center; toast.VerticalAlignment = VerticalAlignment.Bottom; toast.Margin = new Thickness(0, 0, 0, 26);
        toast.IsHitTestVisible = false; toast.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 24, ShadowDepth = 4, Opacity = 0.5, Color = Colors.Black };

        var body = Cols("a,*", side, main);
        var layout = new DockPanel();
        layout.Children.Add(titleBar.DockTo(System.Windows.Controls.Dock.Top));
        layout.Children.Add(new Border { Height = 1, Background = Hex("#1B1E25") }.DockTo(System.Windows.Controls.Dock.Top));
        layout.Children.Add(body);
        root.Children.Add(layout);
        var toastLayer = new Grid { IsHitTestVisible = false }; Grid.SetColumn(toastLayer, 0); toastLayer.Children.Add(toast);
        root.Children.Add(toastLayer);
        Content = root;

        InputBindings.Add(new KeyBinding(new Relay(DoSave), Key.S, ModifierKeys.Control));
        Closing += (_, e) => { if (!ConfirmDiscard()) e.Cancel = true; };
        App.Trace("ctor f");
        watch.Tick += (_, _) => CheckGame(); watch.Start(); CheckGame();
        App.Trace("ctor g");

        Loaded += (_, _) =>
        {
            string first = null;
            try { first = Directory.Exists(SaveGame.SaveDir) ? Directory.GetFiles(SaveGame.SaveDir, "slot_*.dat").OrderByDescending(File.GetLastWriteTime).FirstOrDefault() : null; } catch { }
            LoadSlot(first);
            ((RadioButton)nav.Children[0]).IsChecked = true;
        };
    }

    sealed class Relay(Action a) : ICommand { public event EventHandler CanExecuteChanged { add { } remove { } } public bool CanExecute(object p) => true; public void Execute(object p) => a(); }

    void CheckGame()
    {
        bool running = false;
        try { running = Process.GetProcessesByName("FindTheNeedle").Length > 0; } catch { }
        gameWarn.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
    }

    bool ConfirmDiscard() => Save == null || !Save.Dirty || Dlg.Confirm("Ungespeicherte Änderungen", "Im aktuellen Spielstand gibt es Änderungen, die noch nicht gespeichert sind. Verwerfen?", "Verwerfen", true);

    // ---------- slots ----------
    void BuildSlots()
    {
        slotPanel.Children.Clear();
        var files = new List<string>();
        try { if (Directory.Exists(SaveGame.SaveDir)) files.AddRange(Directory.GetFiles(SaveGame.SaveDir, "slot_*.dat").OrderBy(f => f)); } catch { }
        if (Save != null && !files.Contains(Save.Path, StringComparer.OrdinalIgnoreCase)) files.Add(Save.Path);
        foreach (var f in files)
        {
            bool active = Save != null && string.Equals(Save.Path, f, StringComparison.OrdinalIgnoreCase);
            string name = Path.GetFileNameWithoutExtension(f);
            string label = name.StartsWith("slot_") && int.TryParse(name[5..], out int n) ? $"Slot {n + 1}" : name;
            string line2 = "nicht lesbar"; double frac = 0; string needles = "";
            try
            {
                var s = active ? Save : SaveGame.Load(f);
                var m = s.Meta;
                string custom = m?.Str("name");
                if (!string.IsNullOrWhiteSpace(custom)) label = custom;
                line2 = $"{GameData.PileName(m?.Str("pile_size"))} · {Short(s.State.Num("money"))} $";
                double init = s.State.Num("hay_initial"); frac = init > 0 ? 1 - s.State.Num("hay_total") / init : 0;
                int total = s.State.Get("needle_taken")?.Bytes?.Length ?? 0;
                needles = $"{s.State.Get("needle_taken")?.Bytes?.Count(b => b != 0) ?? 0}/{total}";
            }
            catch { }
            var top = Cols("*,a", T(label, 12.5, active ? Ui.Text : Dim, true), H(Ui.Icon("", 10, active ? Steel : Faint).M(0, 0, 4, 0), Mono(needles, 11, active ? Steel : Faint)));
            var card = new Border
            {
                Background = active ? Bg3 : Brushes.Transparent, BorderBrush = active ? Alpha(Hay, 0.55) : Hex("#1F222A"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
                Padding = new Thickness(11, 8, 11, 9), Margin = new Thickness(12, 0, 12, 6), Cursor = Cursors.Hand,
                Child = V(top, T(line2, 11, active ? Dim : Faint).M(0, 2, 0, 6), Bar(frac, active ? Hay : Faint, 3))
            };
            card.Tip($"{f}\nAbgebaut: {frac:P1}");
            if (!active)
            {
                card.MouseEnter += (_, _) => card.Background = Hex("#191C22");
                card.MouseLeave += (_, _) => card.Background = Brushes.Transparent;
                card.MouseLeftButtonUp += (_, _) => { if (ConfirmDiscard()) LoadSlot(f); };
            }
            slotPanel.Children.Add(card);
        }
        var open = Btn("Datei öffnen …", OpenFile, "BtnGhost", ""); open.Foreground = Dim; open.FontSize = 11.5; open.HorizontalAlignment = HorizontalAlignment.Left;
        slotPanel.Children.Add(open.M(10, 0, 0, 0));
        if (files.Count == 0) slotPanel.Children.Insert(0, T("Keine Spielstände gefunden.", 11.5, Dim).M(18, 0, 0, 8));
    }

    void OpenFile()
    {
        if (!ConfirmDiscard()) return;
        var d = new Microsoft.Win32.OpenFileDialog { Filter = "Spielstand (*.dat;*.bak;*.bin)|*.dat;*.bak;*.bin|Alle Dateien|*.*", InitialDirectory = Directory.Exists(SaveGame.SaveDir) ? SaveGame.SaveDir : null };
        if (d.ShowDialog(this) == true) LoadSlot(d.FileName);
    }

    public void LoadSlot(string path)
    {
        if (path != null)
        {
            try { Save = SaveGame.Load(path); }
            catch (Exception ex) { Dlg.Info("Laden fehlgeschlagen", $"{Path.GetFileName(path)}\n\n{ex.Message}"); }
        }
        BuildSlots(); UpdateDirty(); Refresh();
    }

    // ---------- navigation ----------
    void Navigate(Page p) { current = p; pageTitle.Text = p.Title; pageSub.Text = p.Sub; Refresh(); }

    public void Refresh()
    {
        if (current == null) return;
        (host.Content as IDisposable)?.Dispose();
        if (Save == null) { host.Content = Empty("Kein Spielstand geladen", "Im Ordner " + SaveGame.SaveDir + " wurde nichts gefunden. Über „Datei öffnen …“ kannst du eine Datei wählen."); return; }
        try { host.Content = current.Build(Save); }
        catch (Exception ex) { host.Content = Empty("Dieser Bereich konnte nicht aufgebaut werden", ex.Message); }
    }

    public static FrameworkElement Empty(string title, string text)
        => new Border { Child = V(Ui.Icon("", 26, Faint).M(0, 0, 0, 12), Head(title, 16), T(text, 12.5, Dim).Wrap().M(0, 6, 0, 0)), MaxWidth = 460, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };

    // ---------- state ----------
    public void MarkDirty() { if (Save == null) return; Save.Dirty = true; UpdateDirty(); }
    void UpdateDirty()
    {
        bool d = Save?.Dirty == true;
        dirtyText.Text = Save == null ? "" : d ? "●  Ungespeicherte Änderungen" : "Alles gespeichert";
        dirtyText.Foreground = d ? Hay : Faint;
        saveBtn.IsEnabled = Save != null;
    }

    void DoSave()
    {
        if (Save == null) return;
        Keyboard.ClearFocus();
        CheckGame();
        if (gameWarn.Visibility == Visibility.Visible && !Dlg.Confirm("Spiel läuft noch", "Wenn dieser Spielstand gerade im Spiel geladen ist, überschreibt das Spiel deine Änderungen beim nächsten Autosave. Trotzdem speichern?", "Trotzdem speichern", true)) return;
        try
        {
            string bak = Save.Save();
            BuildSlots(); UpdateDirty();
            Toast("Gespeichert" + (bak != null ? " · Backup angelegt" : "") + " · Lauf zählt nicht für Bestenlisten");
            if (current?.Key == "backups") Refresh();
        }
        catch (Exception ex) { Dlg.Info("Speichern fehlgeschlagen", ex.Message); }
    }

    /// <summary>Developer aid: renders every page to PNG (FindTheNeedleEditor.exe --shots &lt;dir&gt;).</summary>
    public async Task Shots(string dir)
    {
        Directory.CreateDirectory(dir);
        async Task Snap(string name)
        {
            await Task.Delay(350);
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            var dpi = VisualTreeHelper.GetDpi(this);
            var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap((int)(root.ActualWidth * dpi.DpiScaleX), (int)(root.ActualHeight * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
            rtb.Render(root);
            var enc = new System.Windows.Media.Imaging.PngBitmapEncoder(); enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
            using var fs = File.Create(Path.Combine(dir, name + ".png")); enc.Save(fs);
        }
        foreach (var p in pages)
        {
            if (p.Key == "radar")
                foreach (var m in new[] { "top", "3d", "xray" }) { RadarPage.ForceMode(m); Navigate(p); await Snap("radar_" + m); }
            else { Navigate(p); await Snap(p.Key); }
        }
        Save.Dirty = false;
        Close();
    }

    public void Toast(string text, bool bad = false)
    {
        toast.Child = H(Icon(bad ? "" : "", 13, bad ? Bad : Good).M(0, 0, 9, 0), T(text, 12.5));
        toast.Visibility = Visibility.Visible;
        var a = new DoubleAnimationUsingKeyFrames();
        a.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        a.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(140))));
        a.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(2300))));
        a.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(2700))));
        toast.BeginAnimation(OpacityProperty, a);
    }
}
