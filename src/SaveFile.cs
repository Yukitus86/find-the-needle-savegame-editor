using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace FtnEditor;

/// <summary>Godot "GDEC" encrypted file (FileAccessEncrypted, AES-256-CFB, password mode).</summary>
public static class GdCrypt
{
    const string Pass = "b88b32d1fb83a23ac1facd11a612222774cca9aed41a87ed";
    // open_encrypted_with_pass: key = ASCII of the lowercase md5 hex of the password
    static readonly byte[] Key = Encoding.ASCII.GetBytes(Convert.ToHexString(MD5.HashData(Encoding.ASCII.GetBytes(Pass))).ToLowerInvariant());

    public static bool IsEncrypted(byte[] file) => file.Length > 44 && file[0] == 'G' && file[1] == 'D' && file[2] == 'E' && file[3] == 'C';

    public static byte[] Decrypt(byte[] file)
    {
        if (!IsEncrypted(file)) throw new InvalidDataException("Not a GDEC file.");
        long len = BitConverter.ToInt64(file, 20);
        int padded = (int)((len + 15) / 16 * 16);
        if (len < 0 || 44 + padded > file.Length) throw new InvalidDataException("Save file is truncated.");
        byte[] ecbIn = new byte[padded];
        Buffer.BlockCopy(file, 28, ecbIn, 0, 16);                       // IV
        if (padded > 16) Buffer.BlockCopy(file, 44, ecbIn, 16, padded - 16); // previous cipher blocks
        using var aes = Aes.Create(); aes.Key = Key;
        byte[] o = aes.EncryptEcb(ecbIn, PaddingMode.None);
        for (int i = 0; i < padded; i++) o[i] ^= file[44 + i];
        byte[] plain = o.AsSpan(0, (int)len).ToArray();
        if (!MD5.HashData(plain).AsSpan().SequenceEqual(file.AsSpan(4, 16))) throw new InvalidDataException("Checksum mismatch - wrong key or damaged save.");
        return plain;
    }

    public static byte[] Encrypt(byte[] plain)
    {
        int padded = (plain.Length + 15) / 16 * 16;
        byte[] data = new byte[padded];
        Buffer.BlockCopy(plain, 0, data, 0, plain.Length);
        byte[] iv = RandomNumberGenerator.GetBytes(16);
        byte[] outp = new byte[44 + padded];
        outp[0] = (byte)'G'; outp[1] = (byte)'D'; outp[2] = (byte)'E'; outp[3] = (byte)'C';
        MD5.HashData(plain).CopyTo(outp, 4);
        BitConverter.GetBytes((long)plain.Length).CopyTo(outp, 20);
        iv.CopyTo(outp, 28);
        using var aes = Aes.Create(); aes.Key = Key;
        byte[] prev = iv, ks = new byte[16];
        for (int off = 0; off < padded; off += 16)
        {
            aes.EncryptEcb(prev, ks, PaddingMode.None);
            for (int i = 0; i < 16; i++) outp[44 + off + i] = (byte)(data[off + i] ^ ks[i]);
            prev = outp.AsSpan(44 + off, 16).ToArray();
        }
        return outp;
    }
}

public sealed class SaveGame
{
    public string Path;
    public GdVar Root;
    public bool Dirty;

    public GdDict State => Root["state"].Dict;
    public GdDict Meta => Root["meta"]?.Dict;
    public GdDict Ranks => Root["tech"]?["ranks"]?.Dict;
    public List<GdVar> Buildings => Root["buildings"]?.List ?? new();
    public List<GdVar> Props => Root["props"]?.List ?? new();
    public float[] Heights => Root["heights"]?.F32;
    public float[] Dome => Root["dome"]?.F32;
    public double Cell => Root.Dict.Num("cell", 0.25);
    public double Extent => Root.Dict.Num("extent", 15);
    public int Grid => Heights == null ? 0 : (int)Math.Round(Math.Sqrt(Heights.Length));

    // Early Access writes to "FindTheNeedle", the demo used "Haystack Incremental". Prefer the first folder that holds slot files.
    static readonly string[] GameDirs = { "FindTheNeedle", "Find The Needle", "Haystack Incremental" };
    static string saveDir;
    public static string SaveDir => saveDir ??= FindSaveDir();
    static string FindSaveDir()
    {
        string root = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Godot", "app_userdata");
        var all = GameDirs.Select(g => System.IO.Path.Combine(root, g, "saves")).ToList();
        bool HasSlots(string d) { try { return Directory.Exists(d) && Directory.EnumerateFiles(d, "slot_*.dat").Any(); } catch { return false; } }
        return all.FirstOrDefault(HasSlots) ?? all.FirstOrDefault(Directory.Exists) ?? all[0];
    }
    public static string UserDir => System.IO.Path.GetDirectoryName(SaveDir);
    public static string BackupDir => System.IO.Path.Combine(UserDir, "NeedleEditorBackups");

    public static SaveGame Load(string path)
    {
        byte[] raw = File.ReadAllBytes(path);
        byte[] plain = GdCrypt.IsEncrypted(raw) ? GdCrypt.Decrypt(raw) : raw;
        return new SaveGame { Path = path, Root = GdVar.FromFile(plain) };
    }

    /// <summary>Keeps the slot-preview block ("meta") in line with the real state.</summary>
    public void SyncMeta()
    {
        var m = Meta; if (m == null) return;
        foreach (var k in new[] { "hay_dug", "hay_initial", "hay_total", "needles_found", "money", "money_earned" })
            if (m.Has(k) && State.Has(k)) m.SetNum(k, State.Num(k));
    }

    public string Save(bool backup = true)
    {
        SyncMeta();
        // Fair play: an edited run is marked the way the game marks debug use, so it stays off the leaderboards.
        if (State.Get("debug_used") is { } dbg) dbg.Bool = true; else State.Set("debug_used", GdVar.NewBool(true));
        string bak = null;
        if (backup && File.Exists(Path)) bak = Backup(Path, "auto");
        byte[] enc = GdCrypt.Encrypt(Root.ToFile());
        string tmp = Path + ".tmp";
        File.WriteAllBytes(tmp, enc);
        File.Move(tmp, Path, true);
        Dirty = false;
        return bak;
    }

    public static string Backup(string path, string tag)
    {
        Directory.CreateDirectory(BackupDir);
        string name = $"{System.IO.Path.GetFileNameWithoutExtension(path)}__{DateTime.Now:yyyy-MM-dd_HH-mm-ss}__{tag}{System.IO.Path.GetExtension(path)}";
        string dst = System.IO.Path.Combine(BackupDir, name);
        File.Copy(path, dst, true);
        return dst;
    }

    // ---------- pile helpers ----------
    public double WorldOf(int i) => -Extent + i * Cell;
    public double HeightAt(double x, double z)
    {
        var h = Heights; int n = Grid; if (h == null || n < 2) return 0;
        double fx = Math.Clamp((x + Extent) / Cell, 0, n - 1.001), fz = Math.Clamp((z + Extent) / Cell, 0, n - 1.001);
        int ix = (int)fx, iz = (int)fz; double tx = fx - ix, tz = fz - iz;
        double a = h[iz * n + ix], b = h[iz * n + ix + 1], c = h[(iz + 1) * n + ix], d = h[(iz + 1) * n + ix + 1];
        return (a * (1 - tx) + b * tx) * (1 - tz) + (c * (1 - tx) + d * tx) * tz;
    }
    public double HeightSum() { double s = 0; var h = Heights; if (h != null) foreach (var v in h) s += v; return s; }

    /// <summary>Call around any height edit: rescales the hay counter by the volume change.</summary>
    public void ApplyVolumeChange(double sumBefore)
    {
        double after = HeightSum();
        double total = State.Num("hay_total");
        double perUnit = sumBefore > 1e-6 ? total / sumBefore : 542.5;
        if (perUnit < 1 || perUnit > 1e6) perUnit = 542.5;
        State.SetNum("hay_total", Math.Max(0, total + (after - sumBefore) * perUnit));
        Dirty = true;
    }
}
