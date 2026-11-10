using System.Windows.Media;

namespace FtnEditor;

/// <summary>
/// Catalog built from what the save files themselves contain (ids, order, highest ranks seen in fully upgraded runs).
/// Nothing here comes from the game's packed data.
/// </summary>
public static class GameData
{
    public sealed record TechCat(string Name, string Icon, string Color, (string Id, int Max)[] Cards);

    static (string, int)[] P(string spec) => spec.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(s => { var a = s.Split(':'); return (a[0], int.Parse(a[1])); }).ToArray();

    public static readonly TechCat[] Tech =
    {
        new("Handwerkzeug", "", "#F2B544", P("yard:1 hand_carry:5 sand_shovel:1 toy_shovel_size:3 spade:1 shovel_size:2 pitchfork:1 fork_size:5 broom:1 broom_bristles:3 bucket:1 bucket_size:5 wheelbarrow:1 barrow_size:5 dump_hatch:1 smooth_pour:3 yard_vac:1 vac_bin:3 vac_suction:3")),
        new("Förderbänder", "", "#5CC8E8", P("belt:1 enclosed_belt:1 belt_speed:8 belt_links:1 splitter:1 overflow_gate:1 t_splitter:1 compact_splitter:1 smart_splitter:1 joiner:1 haystairs:1 haylift:1 launcher:1")),
        new("Automation", "", "#B48CF2", P("piston_rake:1 rake_bite:5 rake_speed:5 rake_wheels:1 arm_small:1 arm_standard:1 arm_long:1 overflow_arm:1 pick_order:1 arm_speed:8 arm_payload:8 drone:1 drone_collect:1 drone_radius:5 drone_speed:5")),
        new("Verarbeitung", "", "#6FD08C", P("compressor:1 compressor_batch:6 compressor_speed:6 bale_quality:5 wrapper:1 wrapper_speed:6 foil_quality:5 silo:1 silo_rate:5 silo_capacity:5 silo_bulk:6 pelletizer:1 pellet_batch:6 pellet_speed:6 brick_quality:5 pellet_swivel:1 pulper:1 pulper_batch:6 pulper_speed:6 pulp_quality:5 paper_machine:1 paper_speed:6 paper_quality:5 briquette_press:1 briquette_speed:6 briquette_quality:5 hay_price:5")),
        new("Strom & Wasser", "", "#FF8F5C", P("electricity:1 power_pole:1 generator_output:6 pole_span:5 pole_drop:4 underground_power:1 borehole:1 pump_output:6 water_main:1")),
        new("Bauen & Hof", "", "#E3C9A0", P("deck:1 access:1 paint_board:1 wall:1 roof:1 work_lamp:1 yard_space:6 steel_saving:3 build_reach:5")),
        new("Nadelsuche", "", "#7FE3D2", P("scanner_mk1:1 scanner_mk2:1 scan_batch:5 scan_speed:5 cabinet:1 drawer_space:3 metal_detector:1 detector_coil:3 detector_pinpoint:1")),
        new("Körper", "", "#F27CA8", P("work_boots:5 strong_back:5 arm_load:4 second_wind:5 easy_swing:5 steady_carry:5 careful_steps:5 grab_reach:5 jump_height:5 soft_landings:5")),
    };

    public static readonly Dictionary<string, int> MaxRank = Tech.SelectMany(c => c.Cards).ToDictionary(c => c.Id, c => c.Max);
    public static int MaxOf(string id, int current) => MaxRank.TryGetValue(id, out int m) ? Math.Max(m, 1) : Math.Max(current, 1);

    public static readonly string[] Tools = { "sand_shovel", "spade", "pitchfork", "broom", "bucket", "wheelbarrow", "yard_vac", "metal_detector" };
    public static readonly Dictionary<string, string> ToolGlyph = new()
    {
        ["sand_shovel"] = "", ["spade"] = "", ["pitchfork"] = "", ["broom"] = "", ["bucket"] = "", ["wheelbarrow"] = "", ["yard_vac"] = "", ["metal_detector"] = "",
    };
    public static readonly Dictionary<string, string> ToolName = new()
    {
        ["sand_shovel"] = "Sandschaufel", ["spade"] = "Spaten", ["pitchfork"] = "Heugabel", ["broom"] = "Besen", ["bucket"] = "Eimer", ["wheelbarrow"] = "Schubkarre", ["yard_vac"] = "Hofsauger", ["metal_detector"] = "Metalldetektor",
    };

    public static readonly string[] Builds = { "belt", "arm", "arm_standard", "arm_long", "deck", "stair", "scanner", "pole", "generator", "splitter", "u_splitter", "joiner", "u_joiner", "compressor", "wrapper", "silo", "drone", "rake", "cabinet" };

    public sealed record BuildingInfo(string Name, string Icon, string Color);
    static readonly Dictionary<string, BuildingInfo> Bld = new()
    {
        ["haystack_scanner"] = new("Heu-Scanner", "", "#7FE3D2"),
        ["hay_compressor"] = new("Ballenpresse", "", "#6FD08C"),
        ["hay_wrapper"] = new("Folienwickler", "", "#9ADF7A"),
        ["hay_silo"] = new("Silo", "", "#E3C9A0"),
        ["hay_generator"] = new("Generator", "", "#FF8F5C"),
        ["power_pole"] = new("Strommast", "", "#FFC27A"),
        ["conveyor"] = new("Förderband", "", "#5CC8E8"),
        ["conveyor_splitter"] = new("Weiche", "", "#6FA8FF"),
        ["conveyor_u_splitter"] = new("U-Weiche", "", "#6FA8FF"),
        ["conveyor_joiner"] = new("Zusammenführung", "", "#8E9BFF"),
        ["conveyor_u_joiner"] = new("U-Zusammenführung", "", "#8E9BFF"),
        ["needle_cabinet"] = new("Nadelschrank", "", "#F2B544"),
        ["hay_drone"] = new("Drohne", "", "#B48CF2"),
        ["robotic_arm"] = new("Roboterarm", "", "#C99BFF"),
    };
    public static BuildingInfo Building(string type) => Bld.TryGetValue(type ?? "", out var b) ? b : new(Ui.Pretty(type), "", "#9AA0AE");

    public static readonly string[] PileSizes = { "small", "standard", "big", "huge" };
    public static string PileName(string id) => id switch { "small" => "Klein", "standard" => "Standard", "big" => "Groß", "huge" => "Riesig", _ => Ui.Pretty(id) };

    /// <summary>Stable colour per needle type so the same type looks the same everywhere.</summary>
    public static Color NeedleColor(int type)
    {
        string[] pal = { "#5CC8E8", "#F2B544", "#6FD08C", "#F27CA8", "#B48CF2", "#FF8F5C", "#7FE3D2", "#E3E36A" };
        var c = Ui.C(pal[((type % pal.Length) + pal.Length) % pal.Length]);
        double f = 1 - (type / pal.Length) * 0.22;
        return Color.FromRgb((byte)(c.R * f), (byte)(c.G * f), (byte)(c.B * f));
    }
    public static string NeedleName(int type) => $"Nadel-Typ {type + 1:00}";
}
