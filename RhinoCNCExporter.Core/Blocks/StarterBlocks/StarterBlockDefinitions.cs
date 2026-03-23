namespace RhinoCNCExporter.Core.Blocks.StarterBlocks;

/// <summary>
/// Static definitions for the 4 starter CNC blocks.
/// Each block is defined as a Dictionary&lt;string,string&gt; matching the CNC_* UserText schema.
/// Used for:
///   a) Validation testing
///   b) Reference for users creating custom blocks
///   c) Future: automatic block creation command
/// </summary>
public static class StarterBlockDefinitions
{
    /// <summary>
    /// Topfband Ø35mm — simple vertical drill for concealed hinges.
    /// CNC_Type=DRILL, Diameter=35, Depth=13, Side=TOP
    /// </summary>
    public static IReadOnlyDictionary<string, string> Topfband_35 { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [BlockUserTextSchema.CNC_TYPE] = "DRILL",
            [BlockUserTextSchema.CNC_DIAMETER] = "35",
            [BlockUserTextSchema.CNC_DEPTH] = "13",
            [BlockUserTextSchema.CNC_SIDE] = "TOP",
            [BlockUserTextSchema.CNC_TECHCODE] = "E009",
            [BlockUserTextSchema.CNC_DESCRIPTION] = "Topfband 35mm Bohrung (Blum, Hettich, Grass)"
        };

    /// <summary>
    /// System 32 Lochreihe — drill pattern for shelf pin holes.
    /// CNC_Type=DRILLPATTERN, Diameter=5, Depth=13, PatternY=10, SpacingY=32
    /// </summary>
    public static IReadOnlyDictionary<string, string> Lochreihe_32 { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [BlockUserTextSchema.CNC_TYPE] = "DRILLPATTERN",
            [BlockUserTextSchema.CNC_DIAMETER] = "5",
            [BlockUserTextSchema.CNC_DEPTH] = "13",
            [BlockUserTextSchema.CNC_PATTERN_X] = "1",
            [BlockUserTextSchema.CNC_PATTERN_Y] = "10",
            [BlockUserTextSchema.CNC_SPACING_X] = "0",
            [BlockUserTextSchema.CNC_SPACING_Y] = "32",
            [BlockUserTextSchema.CNC_SIDE] = "TOP",
            [BlockUserTextSchema.CNC_TECHCODE] = "E013",
            [BlockUserTextSchema.CNC_DESCRIPTION] = "System 32 Lochreihe (10 Löcher, Abstand 32mm)"
        };

    /// <summary>
    /// Riffeldübel Ø8×30 — face drill (top) + edge drill (side) as separate block.
    /// This definition covers the face drill (Ø8, T=10, TOP).
    /// The corresponding edge drill (HDRILL) would be a separate block on the mating plate.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Duebel_8x30 { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [BlockUserTextSchema.CNC_TYPE] = "DRILL",
            [BlockUserTextSchema.CNC_DIAMETER] = "8",
            [BlockUserTextSchema.CNC_DEPTH] = "10",
            [BlockUserTextSchema.CNC_SIDE] = "TOP",
            [BlockUserTextSchema.CNC_TECHCODE] = "E013",
            [BlockUserTextSchema.CNC_DESCRIPTION] = "Riffeldübel Ø8×30 (Flächenbohrung)"
        };

    /// <summary>
    /// Dübel edge drill counterpart — horizontal drill for the mating plate.
    /// CNC_Type=HDRILL, Diameter=8, Depth=30, Side=LEFT
    /// </summary>
    public static IReadOnlyDictionary<string, string> Duebel_8x30_Stirn { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [BlockUserTextSchema.CNC_TYPE] = "HDRILL",
            [BlockUserTextSchema.CNC_DIAMETER] = "8",
            [BlockUserTextSchema.CNC_DEPTH] = "30",
            [BlockUserTextSchema.CNC_SIDE] = "LEFT",
            [BlockUserTextSchema.CNC_TECHCODE] = "E013",
            [BlockUserTextSchema.CNC_DESCRIPTION] = "Riffeldübel Ø8×30 (Stirnbohrung)"
        };

    /// <summary>
    /// CLAMEX P14 Lamello connector — macro-based machining (SawCut_Lamello).
    /// Generates a complex CNC macro with ~48 parameters.
    /// Parameters use {DZ} and {Y} placeholders resolved at export time.
    /// </summary>
    public static IReadOnlyDictionary<string, string> CLAMEX_P14 { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [BlockUserTextSchema.CNC_TYPE] = "MACRO",
            [BlockUserTextSchema.CNC_MACRO_NAME] = "SawCut_Lamello",
            [BlockUserTextSchema.CNC_MACRO_PARAMS] =
                "{DZ}-9.5,{Y},{DZ}-9.5,{Y},"
                + "0,2,{DZ},5,null,1,0.05,null,null,null,null,"
                + "2,3,E015,null,3,E004,null,0,0,false,-1,0,null,"
                + "0,false,3,E019,null,null,null,null,null,null,null,"
                + "4,null,null,14.3,null,3,E032,270",
            [BlockUserTextSchema.CNC_SIDE] = "TOP",
            [BlockUserTextSchema.CNC_ORIENTATION] = "0",
            [BlockUserTextSchema.CNC_DEPTH] = "9.5",
            [BlockUserTextSchema.CNC_TECHCODE] = "E015",
            [BlockUserTextSchema.CNC_DESCRIPTION] = "CLAMEX P14 Verbinder (Lamello)"
        };

    /// <summary>
    /// Get all starter block definitions as name-definition pairs.
    /// </summary>
    public static IReadOnlyList<(string Name, IReadOnlyDictionary<string, string> Definition)> All { get; } =
        new (string, IReadOnlyDictionary<string, string>)[]
        {
            ("Topfband_35", Topfband_35),
            ("Lochreihe_32", Lochreihe_32),
            ("Duebel_8x30", Duebel_8x30),
            ("Duebel_8x30_Stirn", Duebel_8x30_Stirn),
            ("CLAMEX_P14", CLAMEX_P14)
        };
}
