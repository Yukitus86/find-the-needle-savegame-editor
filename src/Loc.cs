using System.IO;

namespace FtnEditor;

/// <summary>
/// UI language. German texts in the code are the keys; English comes from <see cref="LocEn.Map"/>.
/// The UI helpers (T, Caps, Btn, Switch, Seg, Pill, Tip, Combo) translate on their own, so only
/// texts with numbers in them call L(format, args) explicitly.
/// </summary>
public static class Loc
{
    public static bool De { get; private set; }
    static string SettingsFile => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FindTheNeedleEditor", "settings.ini");

    public static void Load()
    {
        De = false; // English by default
        try { if (File.Exists(SettingsFile)) De = File.ReadAllLines(SettingsFile).Any(l => l.Trim() == "lang=de"); } catch { }
        ApplyCulture();
    }
    static void ApplyCulture()
    {
        var c = System.Globalization.CultureInfo.GetCultureInfo(De ? "de-DE" : "en-US");
        System.Globalization.CultureInfo.CurrentCulture = c; System.Globalization.CultureInfo.DefaultThreadCurrentCulture = c;
    }
    public static void Set(bool de, bool persist = true)
    {
        De = de; ApplyCulture();
        if (!persist) return;
        try { Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)); File.WriteAllText(SettingsFile, "lang=" + (de ? "de" : "en") + Environment.NewLine); } catch { }
    }

    static readonly HashSet<string> missing = new();
    static readonly string missLog = Environment.GetEnvironmentVariable("FTN_I18N_MISSING");

    public static string L(string de)
    {
        if (De || string.IsNullOrEmpty(de)) return de;
        if (LocEn.Map.TryGetValue(de, out var en)) return en;
        if (missLog != null && de.Any(char.IsLetter) && missing.Add(de)) try { File.AppendAllText(missLog, de + Environment.NewLine); } catch { }
        return de;
    }
    public static string L(string de, params object[] args) => string.Format(L(de), args);

    public static string Date(DateTime d) => d.ToString(De ? "dd.MM.yyyy HH:mm" : "yyyy-MM-dd HH:mm");
}
