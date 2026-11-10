using System.IO;
using System.Windows;

namespace FtnEditor;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Trace("startup");
        int dump = Array.IndexOf(e.Args, "--dump");
        if (dump >= 0 && dump + 2 < e.Args.Length) { File.WriteAllText(e.Args[dump + 2], Dump(SaveGame.Load(e.Args[dump + 1]).Root, "", 0)); Shutdown(); return; }
        Loc.Load();
        int lang = Array.IndexOf(e.Args, "--lang");
        if (lang >= 0 && lang + 1 < e.Args.Length) Loc.Set(e.Args[lang + 1] == "de", persist: false);
        int shots = Array.IndexOf(e.Args, "--shots");
        string shotDir = shots >= 0 && shots + 1 < e.Args.Length ? e.Args[shots + 1] : null;
        DispatcherUnhandledException += (_, a) =>
        {
            a.Handled = true;
            if (shotDir != null) { Directory.CreateDirectory(shotDir); File.AppendAllText(Path.Combine(shotDir, "error.txt"), a.Exception + Environment.NewLine); Shutdown(); return; }
            try { Dlg.Info("Fehler", a.Exception.Message + "\n\n" + a.Exception.GetType().Name); } catch { }
        };
        Trace("before window");
        var w = new MainWindow();
        Trace("window constructed");
        if (shotDir != null) w.Loaded += async (_, _) => await w.Shots(shotDir);
        w.Show();
        Trace("shown");
    }
    /// <summary>Developer aid: the save as an indented text tree (FindTheNeedleEditor.exe --dump &lt;save&gt; &lt;out.txt&gt;).</summary>
    static string Dump(GdVar v, string ind, int depth)
    {
        var sb = new System.Text.StringBuilder();
        if (v.Dict is { } d) foreach (var kv in d.Items) { sb.Append(ind).Append(kv.Key.Type == GdVar.STRING ? "" : "<" + kv.Key.TypeName + ">").Append(kv.Key).Append(" : ").Append(kv.Value.TypeName); if (kv.Value.Dict != null || kv.Value.List != null) { sb.Append(kv.Value.Dict != null ? $" [{kv.Value.Dict.Items.Count}]" : $" [{kv.Value.List.Count}]").AppendLine(); if (depth < 8) sb.Append(Dump(kv.Value, ind + "  ", depth + 1)); } else sb.Append(" = ").AppendLine(Short(kv.Value)); }
        else if (v.List is { } l) for (int i = 0; i < l.Count && (i < 6 || Environment.GetEnvironmentVariable("FTN_DUMP_ALL") != null); i++) { sb.Append(ind).Append('#').Append(i).Append(" : ").Append(l[i].TypeName); if (l[i].Dict != null || l[i].List != null) { sb.AppendLine(); if (depth < 8) sb.Append(Dump(l[i], ind + "  ", depth + 1)); } else sb.Append(" = ").AppendLine(Short(l[i])); }
        return sb.ToString();
        static string Short(GdVar x) { var s = x.ToString(); return s.Length > 160 ? s[..160] + "…" : s; }
    }
    public static void Trace(string m) { var f = Environment.GetEnvironmentVariable("FTN_TRACE"); if (f != null) File.AppendAllText(f, DateTime.Now.ToString("HH:mm:ss.fff ") + m + Environment.NewLine); }
}
