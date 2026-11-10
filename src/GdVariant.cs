using System.IO;
using System.Text;

namespace FtnEditor;

/// <summary>Godot 4 binary Variant (as written by FileAccess.store_var). Round-trips byte-exact.</summary>
public sealed class GdVar
{
    public const int NIL = 0, BOOL = 1, INT = 2, FLOAT = 3, STRING = 4, VECTOR3 = 9, TRANSFORM3D = 18, COLOR = 20,
        STRING_NAME = 21, DICT = 27, ARRAY = 28, P_BYTE = 29, P_I32 = 30, P_I64 = 31, P_F32 = 32, P_F64 = 33,
        P_STR = 34, P_V2 = 35, P_V3 = 36, P_COLOR = 37, P_V4 = 38;

    public uint Header;
    /// <summary>bool | long | double | string | double[] | long[] (int vectors) | GdDict | List&lt;GdVar&gt; | byte[] | int[] | float[] | string[]</summary>
    public object Value;
    /// <summary>typed-container prefix (uint builtin type or string class/script) for arrays and dictionaries</summary>
    public object[] TypeInfo;
    public bool SharedFlag;

    public int Type => (int)(Header & 0xFF);
    bool Is64 => (Header & 0x10000) != 0;

    // ---------- convenience ----------
    public GdDict Dict => Value as GdDict;
    public List<GdVar> List => Value as List<GdVar>;
    public bool IsNumber => Type == INT || Type == FLOAT;
    public double Num { get => Value is long l ? l : Value is double d ? d : Value is bool b ? (b ? 1 : 0) : 0; set { if (Type == INT) Value = (long)Math.Round(value); else if (Type == FLOAT) Value = value; else if (Type == BOOL) Value = value != 0; } }
    public long Int { get => (long)Math.Round(Num); set => Num = value; }
    public bool Bool { get => Value is bool b ? b : Num != 0; set { if (Type == BOOL) Value = value; else Num = value ? 1 : 0; } }
    public string Str { get => Value as string ?? ""; set => Value = value; }
    public GdVar this[string key] => Dict?.Get(key);
    public double[] Floats => Value as double[];
    public float[] F32 => Value as float[];
    public int[] I32 => Value as int[];
    public byte[] Bytes => Value as byte[];
    public string[] Strings => Value as string[];

    public static GdVar NewInt(long v) => new() { Header = INT | 0x10000, Value = v };
    public static GdVar NewFloat(double v) => new() { Header = FLOAT | 0x10000, Value = v };
    public static GdVar NewBool(bool v) => new() { Header = BOOL, Value = v };
    public static GdVar NewString(string v) => new() { Header = STRING, Value = v };
    public static GdVar NewDict() => new() { Header = DICT, Value = new GdDict(), TypeInfo = new object[2] };
    public static GdVar NewArray() => new() { Header = ARRAY, Value = new List<GdVar>(), TypeInfo = new object[1] };

    static int FloatCount(int t) => t switch { 5 => 2, 7 => 4, 9 => 3, 11 => 6, 12 => 4, 14 => 4, 15 => 4, 16 => 6, 17 => 9, 18 => 12, 19 => 16, _ => 0 };
    static int IntCount(int t) => t switch { 6 => 2, 8 => 4, 10 => 3, 13 => 4, _ => 0 };

    public string TypeName => Type switch
    {
        NIL => "null", BOOL => "bool", INT => "int", FLOAT => "float", STRING => "String", STRING_NAME => "StringName", 5 => "Vector2", 6 => "Vector2i",
        9 => "Vector3", 10 => "Vector3i", 15 => "Quaternion", 17 => "Basis", 18 => "Transform3D", 20 => "Color", DICT => "Dictionary", ARRAY => "Array",
        P_BYTE => "PackedByteArray", P_I32 => "PackedInt32Array", P_I64 => "PackedInt64Array", P_F32 => "PackedFloat32Array", P_F64 => "PackedFloat64Array",
        P_STR => "PackedStringArray", P_V2 => "PackedVector2Array", P_V3 => "PackedVector3Array", P_COLOR => "PackedColorArray", _ => "Type" + Type
    };

    // ---------- file envelope ----------
    public static GdVar FromFile(byte[] plain)
    {
        var r = new BinaryReader(new MemoryStream(plain));
        r.ReadUInt32();
        return Read(r);
    }
    public byte[] ToFile()
    {
        var ms = new MemoryStream();
        var w = new BinaryWriter(ms);
        w.Write(0u);
        Write(w);
        w.Flush();
        ms.Position = 0; w.Write((uint)(ms.Length - 4));
        return ms.ToArray();
    }

    // ---------- reading ----------
    static string ReadStr(BinaryReader r)
    {
        int n = r.ReadInt32();
        string s = Encoding.UTF8.GetString(r.ReadBytes(n));
        int pad = (4 - n % 4) % 4; if (pad > 0) r.ReadBytes(pad);
        return s;
    }
    static object ReadTypeInfo(BinaryReader r, uint kind) => kind switch { 1 => r.ReadUInt32(), 2 or 3 => ReadStr(r), _ => null };

    public static GdVar Read(BinaryReader r)
    {
        var v = new GdVar { Header = r.ReadUInt32() };
        int t = v.Type; bool d = v.Is64;
        int fc = FloatCount(t), ic = IntCount(t);
        if (fc > 0) { var a = new double[fc]; for (int i = 0; i < fc; i++) a[i] = d ? r.ReadDouble() : r.ReadSingle(); v.Value = a; return v; }
        if (ic > 0) { var a = new long[ic]; for (int i = 0; i < ic; i++) a[i] = r.ReadInt32(); v.Value = a; return v; }
        switch (t)
        {
            case NIL: break;
            case BOOL: v.Value = r.ReadUInt32() != 0; break;
            case INT: v.Value = d ? r.ReadInt64() : r.ReadInt32(); break;
            case FLOAT: v.Value = d ? r.ReadDouble() : (double)r.ReadSingle(); break;
            case STRING: case STRING_NAME: v.Value = ReadStr(r); break;
            case COLOR: { var a = new double[4]; for (int i = 0; i < 4; i++) a[i] = r.ReadSingle(); v.Value = a; break; }
            case DICT:
                {
                    v.TypeInfo = new[] { ReadTypeInfo(r, (v.Header >> 16) & 3), ReadTypeInfo(r, (v.Header >> 18) & 3) };
                    uint n = r.ReadUInt32(); v.SharedFlag = (n & 0x80000000) != 0; n &= 0x7FFFFFFF;
                    var dict = new GdDict();
                    for (uint i = 0; i < n; i++) { var k = Read(r); var val = Read(r); dict.Items.Add(new KeyValuePair<GdVar, GdVar>(k, val)); }
                    v.Value = dict; break;
                }
            case ARRAY:
                {
                    v.TypeInfo = new[] { ReadTypeInfo(r, (v.Header >> 16) & 3) };
                    uint n = r.ReadUInt32(); v.SharedFlag = (n & 0x80000000) != 0; n &= 0x7FFFFFFF;
                    var list = new List<GdVar>((int)n);
                    for (uint i = 0; i < n; i++) list.Add(Read(r));
                    v.Value = list; break;
                }
            case P_BYTE: { int n = r.ReadInt32(); v.Value = r.ReadBytes(n); int pad = (4 - n % 4) % 4; if (pad > 0) r.ReadBytes(pad); break; }
            case P_I32: { int n = r.ReadInt32(); var a = new int[n]; for (int i = 0; i < n; i++) a[i] = r.ReadInt32(); v.Value = a; break; }
            case P_I64: { int n = r.ReadInt32(); var a = new long[n]; for (int i = 0; i < n; i++) a[i] = r.ReadInt64(); v.Value = a; break; }
            case P_F32: { int n = r.ReadInt32(); var a = new float[n]; for (int i = 0; i < n; i++) a[i] = r.ReadSingle(); v.Value = a; break; }
            case P_F64: { int n = r.ReadInt32(); var a = new double[n]; for (int i = 0; i < n; i++) a[i] = r.ReadDouble(); v.Value = a; break; }
            case P_STR: { int n = r.ReadInt32(); var a = new string[n]; for (int i = 0; i < n; i++) a[i] = ReadStr(r); v.Value = a; break; }
            case P_V2: case P_V3: case P_COLOR: case P_V4:
                {
                    int per = t == P_V2 ? 2 : t == P_V3 ? 3 : 4;
                    int n = r.ReadInt32(); var a = new float[n * per]; for (int i = 0; i < a.Length; i++) a[i] = r.ReadSingle(); v.Value = a; break;
                }
            default: throw new InvalidDataException($"Unsupported Godot variant type {t} at 0x{r.BaseStream.Position - 4:x}");
        }
        return v;
    }

    // ---------- writing ----------
    static void WriteStr(BinaryWriter w, string s)
    {
        byte[] b = Encoding.UTF8.GetBytes(s);
        w.Write(b.Length); w.Write(b);
        int pad = (4 - b.Length % 4) % 4; for (int i = 0; i < pad; i++) w.Write((byte)0);
    }
    static void WriteTypeInfo(BinaryWriter w, object o) { if (o is uint u) w.Write(u); else if (o is string s) WriteStr(w, s); }

    public void Write(BinaryWriter w)
    {
        w.Write(Header);
        int t = Type; bool d = Is64;
        if (FloatCount(t) > 0) { foreach (double x in (double[])Value) { if (d) w.Write(x); else w.Write((float)x); } return; }
        if (IntCount(t) > 0) { foreach (long x in (long[])Value) w.Write((int)x); return; }
        switch (t)
        {
            case NIL: break;
            case BOOL: w.Write((bool)Value ? 1u : 0u); break;
            case INT: if (d) w.Write((long)Value); else w.Write((int)(long)Value); break;
            case FLOAT: if (d) w.Write((double)Value); else w.Write((float)(double)Value); break;
            case STRING: case STRING_NAME: WriteStr(w, (string)Value); break;
            case COLOR: foreach (double x in (double[])Value) w.Write((float)x); break;
            case DICT:
                {
                    foreach (var ti in TypeInfo) WriteTypeInfo(w, ti);
                    var dict = (GdDict)Value;
                    w.Write((uint)dict.Items.Count | (SharedFlag ? 0x80000000 : 0));
                    foreach (var kv in dict.Items) { kv.Key.Write(w); kv.Value.Write(w); }
                    break;
                }
            case ARRAY:
                {
                    foreach (var ti in TypeInfo) WriteTypeInfo(w, ti);
                    var list = (List<GdVar>)Value;
                    w.Write((uint)list.Count | (SharedFlag ? 0x80000000 : 0));
                    foreach (var x in list) x.Write(w);
                    break;
                }
            case P_BYTE: { var a = (byte[])Value; w.Write(a.Length); w.Write(a); int pad = (4 - a.Length % 4) % 4; for (int i = 0; i < pad; i++) w.Write((byte)0); break; }
            case P_I32: { var a = (int[])Value; w.Write(a.Length); foreach (var x in a) w.Write(x); break; }
            case P_I64: { var a = (long[])Value; w.Write(a.Length); foreach (var x in a) w.Write(x); break; }
            case P_F32: { var a = (float[])Value; w.Write(a.Length); foreach (var x in a) w.Write(x); break; }
            case P_F64: { var a = (double[])Value; w.Write(a.Length); foreach (var x in a) w.Write(x); break; }
            case P_STR: { var a = (string[])Value; w.Write(a.Length); foreach (var x in a) WriteStr(w, x); break; }
            case P_V2: case P_V3: case P_COLOR: case P_V4:
                {
                    int per = t == P_V2 ? 2 : t == P_V3 ? 3 : 4;
                    var a = (float[])Value; w.Write(a.Length / per); foreach (var x in a) w.Write(x); break;
                }
            default: throw new InvalidDataException("Unsupported type " + t);
        }
    }

    public override string ToString() => Type switch
    {
        NIL => "null",
        BOOL => (bool)Value ? "true" : "false",
        INT => ((long)Value).ToString(),
        FLOAT => ((double)Value).ToString("R", System.Globalization.CultureInfo.InvariantCulture),
        STRING or STRING_NAME => (string)Value,
        DICT => $"{{ {Dict.Items.Count} }}",
        ARRAY => $"[ {List.Count} ]",
        P_BYTE => $"bytes[{Bytes.Length}]",
        P_STR => $"strings[{Strings.Length}]",
        _ => Value is double[] f ? "(" + string.Join(", ", f.Select(x => x.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture))) + ")"
           : Value is Array a ? $"{TypeName}[{a.Length}]" : TypeName
    };
}

/// <summary>Insertion-ordered dictionary keyed by Variant (usually strings).</summary>
public sealed class GdDict
{
    public readonly List<KeyValuePair<GdVar, GdVar>> Items = new();

    public GdVar Get(string key)
    {
        foreach (var kv in Items) if (kv.Key.Value is string s && s == key) return kv.Value;
        return null;
    }
    public bool Has(string key) => Get(key) != null;
    public IEnumerable<string> Keys => Items.Select(kv => kv.Key.ToString());
    public void Set(string key, GdVar value)
    {
        for (int i = 0; i < Items.Count; i++)
            if (Items[i].Key.Value is string s && s == key) { Items[i] = new(Items[i].Key, value); return; }
        Items.Add(new(GdVar.NewString(key), value));
    }
    public bool Remove(string key) => Items.RemoveAll(kv => kv.Key.Value is string s && s == key) > 0;
    public double Num(string key, double def = 0) => Get(key) is { } v && (v.IsNumber || v.Type == GdVar.BOOL) ? v.Num : def;
    public string Str(string key, string def = "") => Get(key)?.Value as string ?? def;
    public void SetNum(string key, double value)
    {
        var v = Get(key);
        if (v != null && (v.IsNumber || v.Type == GdVar.BOOL)) v.Num = value; else Set(key, GdVar.NewFloat(value));
    }
}
