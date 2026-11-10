using System.IO;
using System.Windows;

namespace FtnEditor;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Trace("startup");
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
    public static void Trace(string m) { var f = Environment.GetEnvironmentVariable("FTN_TRACE"); if (f != null) File.AppendAllText(f, DateTime.Now.ToString("HH:mm:ss.fff ") + m + Environment.NewLine); }
}
