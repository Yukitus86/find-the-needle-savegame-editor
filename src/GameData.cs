using System.Windows.Media;
using static FtnEditor.Loc;

namespace FtnEditor;

/// <summary>
/// Catalog built from what the save files themselves contain (ids, order, highest ranks seen in well progressed runs).
/// Nothing here comes from the game's packed data.
/// </summary>
public static class GameData
{
    public sealed record TechCat(string Name, string Icon, string Color, (string Id, int Max)[] Cards);

    static (string, int)[] P(string spec) => spec.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(s => { var a = s.Split(':'); return (a[0], int.Parse(a[1])); }).ToArray();

    public static readonly TechCat[] Tech =
    {
        new("Handwerkzeug", "", "#F2B544", P("yard:1 hand_carry:5 sand_shovel:1 toy_shovel_size:3 spade:1 shovel_size:2 pitchfork:1 fork_size:5 broom:1 broom_bristles:3 bucket:1 bucket_size:5 wheelbarrow:1 barrow_size:5 dump_hatch:1 smooth_pour:3 yard_vac:1 vac_bin:3 vac_suction:3 vac_hose:1 vac_hose_length:2")),
        new("Förderbänder", "", "#5CC8E8", P("belt:1 enclosed_belt:1 belt_speed:8 belt_links:1 belt_pole:1 belt_gate:1 belt_meter:1 underground_belt:1 splitter:1 overflow_gate:1 t_splitter:1 compact_splitter:1 smart_splitter:1 split_ratio:1 splitter_routes:1 joiner:1 compact_joiner:1 haystairs:1 haylift:1 launcher:1")),
        new("Automation", "", "#B48CF2", P("piston_rake:1 rake_bite:5 rake_speed:5 rake_wheels:1 hay_excavator:1 arm_small:1 arm_standard:1 arm_long:1 overflow_arm:1 pick_order:1 arm_speed:8 arm_payload:8 drone:1 drone_collect:1 drone_radius:5 drone_speed:5 drone_lift:1 drone_lift_mk3:1 skyline_crane:1 switch_box:1 light_sensor:1")),
        new("Verarbeitung", "", "#6FD08C", P("compressor:1 compressor_batch:6 compressor_speed:6 bale_quality:5 tune_compressor:7 wrapper:1 wrapper_speed:6 foil_quality:5 tune_wrapper:6 lighter:1 silo:1 silo_rate:5 silo_capacity:5 silo_bulk:6 pelletizer:1 pellet_batch:6 pellet_speed:6 brick_quality:5 pellet_swivel:1 panel_press:1 panel_quality:5 pulper:1 pulper_batch:6 pulper_speed:6 pulp_quality:5 tune_pulper:1 paper_machine:1 paper_speed:6 paper_quality:5 briquette_press:1 briquette_speed:6 briquette_quality:5 digester:1 sugar_cooker:1 bioplastic_reactor:1")),
        new("Metall & Fertigung", "", "#C9A0FF", P("smelter:1 ingot_quality:5 fabricator:1 fabricator_quality:5 tune_fabricator:1 assembler:1 assembler_quality:5 tune_assembler:2 overclocking:1")),
        new("Lager & Handel", "", "#E8A15C", P("depot:1 depot_drone:1 shelf_limits:1 hay_price:5 better_prices:3")),
        new("Strom & Wasser", "", "#FF8F5C", P("electricity:1 power_pole:1 generator_output:6 tune_power:7 pole_span:5 pole_drop:4 underground_power:1 gas_plant:1 borehole:1 pump_output:6 pipe_pump_mk2:1 water_main:1 underground_pipe:1")),
        new("Bauen & Hof", "", "#E3C9A0", P("deck:1 access:1 paint_board:1 wall:1 roof:1 work_lamp:1 yard_lights:1 yard_fan:1 yard_space:6 steel_saving:3 build_reach:5 stand_move:1 jump_pad:1 blueprint_designer:1 blueprint_designer_2:1 blueprint_designer_3:1")),
        new("Nadelsuche", "", "#7FE3D2", P("scanner_mk1:1 scanner_mk2:1 scanner_mk3:1 scan_batch:5 scan_speed:5 radar_mk1:1 cabinet:1 drawer_space:3 metal_detector:1 detector_coil:3 detector_pinpoint:1")),
        new("Körper", "", "#F27CA8", P("work_boots:5 strong_back:5 arm_load:6 second_wind:5 easy_swing:5 steady_carry:5 careful_steps:5 grab_reach:5 jump_height:5 soft_landings:5 grippy_boots:1 jetpack:1")),
    };

    public static readonly Dictionary<string, int> MaxRank = Tech.SelectMany(c => c.Cards).ToDictionary(c => c.Id, c => c.Max);
    public static int MaxOf(string id, int current) => MaxRank.TryGetValue(id, out int m) ? Math.Max(m, 1) : Math.Max(current, 1);

    public static readonly string[] Tools = { "sand_shovel", "spade", "pitchfork", "broom", "bucket", "wheelbarrow", "yard_vac", "metal_detector" };
    public static readonly Dictionary<string, string> ToolGlyph = new()
    {
        ["sand_shovel"] = "", ["spade"] = "", ["pitchfork"] = "", ["broom"] = "", ["bucket"] = "", ["wheelbarrow"] = "", ["yard_vac"] = "", ["metal_detector"] = "",
    };
    static readonly Dictionary<string, string> ToolDe = new()
    {
        ["sand_shovel"] = "Sandschaufel", ["spade"] = "Spaten", ["pitchfork"] = "Heugabel", ["broom"] = "Besen", ["bucket"] = "Eimer", ["wheelbarrow"] = "Schubkarre", ["yard_vac"] = "Hofsauger", ["metal_detector"] = "Metalldetektor",
    };
    public static string ToolName(string id) => ToolDe.TryGetValue(id, out var n) ? L(n) : Ui.Pretty(id);

    public static readonly string[] Builds = { "belt", "arm", "arm_standard", "arm_long", "deck", "stair", "scanner", "pole", "generator", "splitter", "u_splitter", "joiner", "u_joiner", "compressor", "wrapper", "silo", "drone", "rake", "cabinet", "depot" };

    public sealed record BuildingInfo(string Name, string Icon, string Color);
    static readonly Dictionary<string, BuildingInfo> Bld = new()
    {
        ["haystack_scanner"] = new("Heu-Scanner", "", "#7FE3D2"),
        ["needle_radar"] = new("Nadel-Radar", "", "#7FE3D2"),
        ["hay_compressor"] = new("Ballenpresse", "", "#6FD08C"),
        ["hay_wrapper"] = new("Folienwickler", "", "#9ADF7A"),
        ["hay_pulper"] = new("Heubrei-Maschine", "", "#6FD08C"),
        ["digester"] = new("Faulbehälter", "", "#9ADF7A"),
        ["smelter"] = new("Schmelzofen", "", "#C9A0FF"),
        ["hay_silo"] = new("Silo", "", "#E3C9A0"),
        ["depot"] = new("Depot", "", "#E8A15C"),
        ["hay_generator"] = new("Generator", "", "#FF8F5C"),
        ["power_pole"] = new("Strommast", "", "#FFC27A"),
        ["machine_clocks"] = new("Maschinen-Takt", "", "#FFC27A"),
        ["borehole_pump"] = new("Brunnenpumpe", "", "#5CA8E8"),
        ["water_main"] = new("Wasserleitung", "", "#5CA8E8"),
        ["water_splitter"] = new("Wasserverteiler", "", "#5CA8E8"),
        ["piston_rake"] = new("Kolbenrechen", "", "#B48CF2"),
        ["conveyor"] = new("Förderband", "", "#5CC8E8"),
        ["enclosed_conveyor"] = new("Geschlossenes Band", "", "#5CC8E8"),
        ["conveyor_splitter"] = new("Weiche", "", "#6FA8FF"),
        ["conveyor_u_splitter"] = new("U-Weiche", "", "#6FA8FF"),
        ["conveyor_t_splitter"] = new("T-Weiche", "", "#6FA8FF"),
        ["conveyor_joiner"] = new("Zusammenführung", "", "#8E9BFF"),
        ["conveyor_u_joiner"] = new("U-Zusammenführung", "", "#8E9BFF"),
        ["conveyor_t_joiner"] = new("T-Zusammenführung", "", "#8E9BFF"),
        ["needle_cabinet"] = new("Nadelschrank", "", "#F2B544"),
        ["hay_drone"] = new("Drohne", "", "#B48CF2"),
        ["robotic_arm"] = new("Roboterarm", "", "#C99BFF"),
    };
    public static BuildingInfo Building(string type) => Bld.TryGetValue(type ?? "", out var b) ? b with { Name = L(b.Name) } : new(Ui.Pretty(type), "", "#9AA0AE");

    // ---------- depot goods ----------
    public sealed record Good(string Id, string De, string En, string Group);
    public static readonly (string De, string Color, string Icon)[] GoodGroups =
    {
        ("Heuprodukte", "#F2B544", ""), ("Metall", "#9FB4C8", ""), ("Bauteile", "#C9A0FF", ""), ("Carbon & Verbund", "#5CC8E8", ""), ("Bio", "#6FD08C", ""), ("Sonstiges", "#9AA0AE", ""),
    };
    public static readonly Good[] Goods =
    {
        new("hay_bale", "Heuballen", "Hay bale", "Heuprodukte"),
        new("foiled_bale", "Folienballen", "Foiled bale", "Heuprodukte"),
        new("straw_panel", "Strohplatte", "Straw panel", "Heuprodukte"),
        new("eco_brick", "Öko-Brikett", "Eco brick", "Heuprodukte"),
        new("hay_pulp", "Heubrei", "Hay pulp", "Heuprodukte"),
        new("hay_rod", "Heustab", "Hay rod", "Heuprodukte"),
        new("feed_disc", "Futterscheibe", "Feed disc", "Heuprodukte"),
        new("packed_carton", "Karton", "Packed carton", "Heuprodukte"),
        new("paper_roll", "Papierrolle", "Paper roll", "Heuprodukte"),
        new("iron_ingot", "Eisenbarren", "Iron ingot", "Metall"),
        new("copper_ingot", "Kupferbarren", "Copper ingot", "Metall"),
        new("wire_spool", "Drahtspule", "Wire spool", "Metall"),
        new("gear_set", "Zahnradsatz", "Gear set", "Bauteile"),
        new("reinforced_panel", "Verstärkte Platte", "Reinforced panel", "Bauteile"),
        new("motor_core", "Motorkern", "Motor core", "Bauteile"),
        new("motor", "Motor", "Motor", "Bauteile"),
        new("carbon_fibre", "Kohlefaser", "Carbon fibre", "Carbon & Verbund"),
        new("white_bobbin", "Weiße Spule", "White bobbin", "Carbon & Verbund"),
        new("carbon_cable", "Carbonkabel", "Carbon cable", "Carbon & Verbund"),
        new("carbon_boom", "Carbonausleger", "Carbon boom", "Carbon & Verbund"),
        new("composite_plate", "Verbundplatte", "Composite plate", "Carbon & Verbund"),
        new("composite_frame", "Verbundrahmen", "Composite frame", "Carbon & Verbund"),
        new("sugar_sack", "Zuckersack", "Sugar sack", "Bio"),
        new("bioplastic_sheet", "Bioplastik-Folie", "Bioplastic sheet", "Bio"),
    };
    public static Good GoodOf(string id) => Goods.FirstOrDefault(g => g.Id == id) ?? new(id, Ui.Pretty(id), Ui.Pretty(id), "Sonstiges");
    public static string GoodName(string id) { var g = GoodOf(id); return De ? g.De : g.En; }

    public static readonly string[] PileSizes = { "small", "standard", "big", "huge", "mountain" };
    public static string PileName(string id) => id switch { "small" => L("Klein"), "standard" => L("Standard"), "big" => L("Groß"), "huge" => L("Riesig"), "mountain" => L("Berg"), _ => Ui.Pretty(id) };

    /// <summary>Stable colour per needle type so the same type looks the same everywhere.</summary>
    public static Color NeedleColor(int type)
    {
        string[] pal = { "#5CC8E8", "#F2B544", "#6FD08C", "#F27CA8", "#B48CF2", "#FF8F5C", "#7FE3D2", "#E3E36A" };
        var c = Ui.C(pal[((type % pal.Length) + pal.Length) % pal.Length]);
        double f = 1 - (type / pal.Length) * 0.22;
        return Color.FromRgb((byte)(c.R * f), (byte)(c.G * f), (byte)(c.B * f));
    }
    public static string NeedleName(int type) => L("Nadel-Typ {0}", (type + 1).ToString("00"));
}
