using System.Globalization;
using System.Text;
using RhinoCNCExporter.Core.Models;

namespace RhinoCNCExporter.Core.Blocks;

/// <summary>
/// Generates SawCut_Lamello CreateMacro calls for CLAMEX P-System connectors.
/// 
/// CLAMEX has two orientations with different E-codes:
///   Vertikal (machined from face into plate edge):
///     E015, E004, E019, E032
///   Horizontal (machined from top into plate face):
///     E015, E005, E022, E021
///
/// Template-based: constants per CLAMEX type, only position/rotation/DZ vary.
/// Reference: Production XCS files in tests/references/*.xcs
/// </summary>
public static class ClamexMacroBuilder
{
    // --- Parameter positions (0-indexed within the CreateMacro parameter list) ---
    // After name and macro name, these are the ~48 positional parameters.

    /// <summary>
    /// Build a CreateMacro line for a CLAMEX Vertical connector.
    /// Vertical = machined from the plate face (side plate / Seite).
    /// E-codes: E015, E004, E019, E032
    /// 
    /// Reference pattern from production:
    /// CreateMacro("CLAMEX Vertikal_1","SawCut_Lamello",9.5,50.03,9.5,50.03,0,2,19,5,null,1,0.05,
    ///   null,null,null,null,2,"3","E015",null,"3","E004",null,0,0,false,-1,0,null,0,false,
    ///   "3","E019",null,null,null,null,null,null,null,4,null,null,14.3,null,"3","E032",270);
    /// </summary>
    public static string BuildVertical(
        string name,
        double x,
        double y,
        double plateThickness,
        double rotationDeg = 270)
    {
        // Vertical CLAMEX: X position = DZ-9.5 (fixed depth from edge)
        // In production files: x=9.5 for left side, x=DZ-9.5 (=290.5 for 300mm plate) for right side
        var p = new ClamexParams
        {
            Name = name,
            X1 = x,
            Y1 = y,
            X2 = x,
            Y2 = y,
            Orientation = 0,           // 0 = vertical
            P6 = 2,
            PlateThickness = plateThickness,
            P8 = 5,
            P9 = null,
            P10 = 1,
            P11 = 0.05,
            // P12-P15: null
            P16 = 2,
            P17 = "3",
            E1 = "E015",
            P19 = null,
            P20 = "3",
            E2 = "E004",
            P22 = null,
            P23 = 0,
            P24 = 0,
            P25 = false,
            P26 = -1,
            P27 = 0,
            P28 = null,
            P29 = 0,
            P30 = false,
            P31 = "3",
            E3 = "E019",
            P33 = null,
            P34 = null,
            P35 = null,
            P36 = null,
            P37 = null,
            P38 = null,
            P39 = null,
            P40 = 4,
            P41 = null,
            P42 = null,
            ClamexDepth = 14.3,        // CLAMEX cutter depth
            P44 = null,
            P45 = "3",
            E4 = "E032",
            Rotation = rotationDeg,
            // No trailing DZ parameter for vertical
            HasTrailingDzOffset = false
        };

        return FormatCreateMacro(p);
    }

    /// <summary>
    /// Build a CreateMacro line for a CLAMEX Horizontal connector.
    /// Horizontal = machined from top of plate (bottom/top plate / Boden/Deckel).
    /// E-codes: E015, E005, E022, E021
    /// 
    /// Reference pattern from production:
    /// CreateMacro("CLAMEX Horizontal_1","SawCut_Lamello",0,60.03,0,60.03,90,2,19,5,null,1,0.05,
    ///   null,null,null,null,-1,"3","E015",null,null,"E005",null,0,-1,false,-1,0,null,0,false,
    ///   null,"E022",null,null,null,null,null,null,null,2,null,null,14,null,"3","E021",270,DZ-9.5);
    /// </summary>
    public static string BuildHorizontal(
        string name,
        double x,
        double y,
        double plateThickness,
        double rotationDeg = 270)
    {
        var p = new ClamexParams
        {
            Name = name,
            X1 = x,
            Y1 = y,
            X2 = x,
            Y2 = y,
            Orientation = 90,          // 90 = horizontal
            P6 = 2,
            PlateThickness = plateThickness,
            P8 = 5,
            P9 = null,
            P10 = 1,
            P11 = 0.05,
            // P12-P15: null
            P16 = -1,
            P17 = "3",
            E1 = "E015",
            P19 = null,
            P20 = null,               // Different from vertical!
            E2 = "E005",
            P22 = null,
            P23 = 0,
            P24 = -1,                 // Different from vertical!
            P25 = false,
            P26 = -1,
            P27 = 0,
            P28 = null,
            P29 = 0,
            P30 = false,
            P31 = null,               // Different from vertical!
            E3 = "E022",
            P33 = null,
            P34 = null,
            P35 = null,
            P36 = null,
            P37 = null,
            P38 = null,
            P39 = null,
            P40 = 2,                  // Different from vertical (2 vs 4)
            P41 = null,
            P42 = null,
            ClamexDepth = 14,         // 14 vs 14.3 for vertical
            P44 = null,
            P45 = "3",
            E4 = "E021",
            Rotation = rotationDeg,
            // Horizontal has trailing DZ-9.5 parameter
            HasTrailingDzOffset = true
        };

        return FormatCreateMacro(p);
    }

    /// <summary>
    /// Build a CLAMEX macro from a FittingBlock, determining orientation automatically.
    /// </summary>
    /// <param name="block">The fitting block with CNC_* attributes.</param>
    /// <param name="plateLocalX">X position in plate-local coordinates.</param>
    /// <param name="plateLocalY">Y position in plate-local coordinates.</param>
    /// <param name="plateThickness">Plate thickness (DZ) in mm.</param>
    /// <param name="counter">Counter for unique naming (1, 2, 3...).</param>
    /// <returns>The CreateMacro line, or null if block is not a CLAMEX.</returns>
    public static string? BuildFromBlock(
        FittingBlock block,
        double plateLocalX,
        double plateLocalY,
        double plateThickness,
        int counter = 1)
    {
        if (block.MacroName == null ||
            !block.MacroName.Equals("SawCut_Lamello", StringComparison.OrdinalIgnoreCase))
            return null;

        // Determine orientation from CNC_Orientation attribute
        var orientStr = block.Orientation ?? "0";
        bool isHorizontal = orientStr == "90" || orientStr == "horizontal";

        // Get rotation from block rotation
        double rotation = block.Rotation;
        // Default rotation based on convention
        if (rotation == 0) rotation = 270;

        if (isHorizontal)
        {
            var name = $"CLAMEX Horizontal_{counter}";
            return BuildHorizontal(name, plateLocalX, plateLocalY, plateThickness, rotation);
        }
        else
        {
            var name = $"CLAMEX Vertikal_{counter}";
            return BuildVertical(name, plateLocalX, plateLocalY, plateThickness, rotation);
        }
    }

    /// <summary>
    /// Create a MacroMachining from a CLAMEX FittingBlock.
    /// Returns the parameters list that can be used with EmitterRouter.
    /// </summary>
    public static MacroMachining? CreateMachining(
        FittingBlock block,
        double plateLocalX,
        double plateLocalY,
        double plateThickness,
        int counter = 1)
    {
        var macroLine = BuildFromBlock(block, plateLocalX, plateLocalY, plateThickness, counter);
        if (macroLine == null) return null;

        // Parse the generated line into parameters
        var orientStr = block.Orientation ?? "0";
        bool isHorizontal = orientStr == "90" || orientStr == "horizontal";
        var typeName = isHorizontal ? "Horizontal" : "Vertikal";

        return new MacroMachining
        {
            Name = $"CLAMEX {typeName}_{counter}",
            MacroName = "SawCut_Lamello",
            Parameters = ExtractParametersFromLine(macroLine),
            Side = block.CncSide ?? MachiningSide.Top,
            TechCode = "E015",
            Source = MachiningSource.BlockDetection
        };
    }

    // --- Internal helpers ---

    private static string FormatCreateMacro(ClamexParams p)
    {
        var sb = new StringBuilder();
        sb.Append($"CreateMacro(\"{p.Name}\",\"SawCut_Lamello\",");

        // P1-P4: Position (X1, Y1, X2, Y2)
        sb.Append($"{F(p.X1)},{F(p.Y1)},{F(p.X2)},{F(p.Y2)},");

        // P5: Orientation (0=vertical, 90=horizontal)
        sb.Append($"{p.Orientation},");

        // P6: type constant
        sb.Append($"{p.P6},");

        // P7: Plate thickness (DZ)
        sb.Append($"{F(p.PlateThickness)},");

        // P8-P11
        sb.Append($"{p.P8},{Null(p.P9)},{p.P10},{F(p.P11)},");

        // P12-P15: null block
        sb.Append("null,null,null,null,");

        // P16: integer flag
        sb.Append($"{p.P16},");

        // P17-P19: Tool 1 (E015)
        sb.Append($"{Str(p.P17)},{Str(p.E1)},{Null(p.P19)},");

        // P20-P22: Tool 2 (E004 for vertical, E005 for horizontal)
        sb.Append($"{Str(p.P20)},{Str(p.E2)},{Null(p.P22)},");

        // P23-P30: Flags and offsets
        sb.Append($"{p.P23},{p.P24},{Bool(p.P25)},{p.P26},{p.P27},{Null(p.P28)},{p.P29},{Bool(p.P30)},");

        // P31-P33: Tool 3 (E019 for vertical, E022 for horizontal)
        sb.Append($"{Str(p.P31)},{Str(p.E3)},{Null(p.P33)},");

        // P34-P39: 6 null values
        sb.Append("null,null,null,null,null,null,");

        // P40: integer constant
        sb.Append($"{p.P40},");

        // P41-P42: null
        sb.Append("null,null,");

        // P43: CLAMEX cutter depth
        sb.Append($"{F(p.ClamexDepth)},");

        // P44: null
        sb.Append("null,");

        // P45-P46: Tool 4 (E032 for vertical, E021 for horizontal)
        sb.Append($"{Str(p.P45)},{Str(p.E4)},");

        // P47: Rotation
        sb.Append($"{F(p.Rotation)}");

        // Optional trailing DZ-9.5 for horizontal
        if (p.HasTrailingDzOffset)
        {
            sb.Append(",DZ-9.5");
        }

        sb.Append(");");
        return sb.ToString();
    }

    /// <summary>
    /// Extract the raw parameter strings from a CreateMacro line for MacroMachining.
    /// </summary>
    internal static IReadOnlyList<string?> ExtractParametersFromLine(string macroLine)
    {
        // Remove CreateMacro(" prefix and "); suffix
        var start = macroLine.IndexOf(",\"SawCut_Lamello\",", StringComparison.Ordinal);
        if (start < 0) return Array.Empty<string?>();

        var paramStart = start + ",\"SawCut_Lamello\",".Length;
        var paramEnd = macroLine.LastIndexOf(");", StringComparison.Ordinal);
        if (paramEnd < 0) paramEnd = macroLine.Length;

        var paramStr = macroLine[paramStart..paramEnd];
        return paramStr.Split(',')
            .Select(p =>
            {
                var trimmed = p.Trim();
                if (trimmed == "null") return (string?)null;
                // Remove quotes from string parameters
                if (trimmed.StartsWith('"') && trimmed.EndsWith('"'))
                    return trimmed[1..^1];
                return trimmed;
            })
            .ToArray();
    }

    private static string F(double v)
    {
        // Match production format: no trailing zeros, use period as decimal separator
        if (v == Math.Floor(v))
            return ((int)v).ToString(CultureInfo.InvariantCulture);
        return v.ToString("G", CultureInfo.InvariantCulture);
    }

    private static string Null(object? v)
        => v == null ? "null" : v.ToString()!;

    private static string Str(string? v)
        => v == null ? "null" : $"\"{v}\"";

    private static string Bool(bool v)
        => v ? "true" : "false";

    /// <summary>
    /// Internal parameter container for CLAMEX macro generation.
    /// </summary>
    private sealed class ClamexParams
    {
        public string Name { get; set; } = "";
        public double X1 { get; set; }
        public double Y1 { get; set; }
        public double X2 { get; set; }
        public double Y2 { get; set; }
        public int Orientation { get; set; }        // 0=vertical, 90=horizontal
        public int P6 { get; set; }
        public double PlateThickness { get; set; }
        public int P8 { get; set; }
        public object? P9 { get; set; }
        public int P10 { get; set; }
        public double P11 { get; set; }
        public int P16 { get; set; }
        public string? P17 { get; set; }
        public string E1 { get; set; } = "";        // Tool 1
        public object? P19 { get; set; }
        public string? P20 { get; set; }
        public string E2 { get; set; } = "";        // Tool 2
        public object? P22 { get; set; }
        public int P23 { get; set; }
        public int P24 { get; set; }
        public bool P25 { get; set; }
        public int P26 { get; set; }
        public int P27 { get; set; }
        public object? P28 { get; set; }
        public int P29 { get; set; }
        public bool P30 { get; set; }
        public string? P31 { get; set; }
        public string E3 { get; set; } = "";        // Tool 3
        public object? P33 { get; set; }
        public object? P34 { get; set; }
        public object? P35 { get; set; }
        public object? P36 { get; set; }
        public object? P37 { get; set; }
        public object? P38 { get; set; }
        public object? P39 { get; set; }
        public int P40 { get; set; }
        public object? P41 { get; set; }
        public object? P42 { get; set; }
        public double ClamexDepth { get; set; }
        public object? P44 { get; set; }
        public string? P45 { get; set; }
        public string E4 { get; set; } = "";        // Tool 4
        public double Rotation { get; set; }
        public bool HasTrailingDzOffset { get; set; }
    }
}
