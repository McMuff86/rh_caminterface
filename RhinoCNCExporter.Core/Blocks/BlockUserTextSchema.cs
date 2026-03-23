namespace RhinoCNCExporter.Core.Blocks;

/// <summary>
/// Schema for CNC_* UserText keys on block instances.
/// Defines constants, valid values, and validation logic.
/// </summary>
public static class BlockUserTextSchema
{
    // --- Required Keys ---
    public const string CNC_TYPE = "CNC_Type";

    // --- Common Optional Keys ---
    public const string CNC_DIAMETER = "CNC_Diameter";
    public const string CNC_DEPTH = "CNC_Depth";
    public const string CNC_SIDE = "CNC_Side";
    public const string CNC_TECHCODE = "CNC_TechCode";
    public const string CNC_ORIENTATION = "CNC_Orientation";
    public const string CNC_MACRO_NAME = "CNC_MacroName";
    public const string CNC_MACRO_PARAMS = "CNC_MacroParams";
    public const string CNC_PATTERN_X = "CNC_PatternX";
    public const string CNC_PATTERN_Y = "CNC_PatternY";
    public const string CNC_SPACING_X = "CNC_SpacingX";
    public const string CNC_SPACING_Y = "CNC_SpacingY";
    public const string CNC_STEPDOWN = "CNC_StepDown";
    public const string CNC_TOOL_DIA = "CNC_ToolDia";
    public const string CNC_THROUGH = "CNC_Through";
    public const string CNC_DESCRIPTION = "CNC_Description";

    // --- Valid CNC_Type values ---
    public static readonly IReadOnlySet<string> ValidTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "DRILL", "DRILLPATTERN", "MACRO", "CUT", "POCKET", "GROOVE", "HDRILL"
    };

    // --- Valid CNC_Side values ---
    public static readonly IReadOnlySet<string> ValidSides = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "TOP", "BOTTOM", "LEFT", "RIGHT", "FRONT", "BACK"
    };

    // --- Valid CNC_Orientation values ---
    public static readonly IReadOnlySet<int> ValidOrientations = new HashSet<int> { 0, 90, 180, 270 };

    /// <summary>
    /// Validate that a dictionary of CNC_* attributes is well-formed.
    /// Returns (true, null) on success, (false, error message) on failure.
    /// </summary>
    public static (bool IsValid, string? Error) Validate(IReadOnlyDictionary<string, string> attrs)
    {
        // CNC_Type is required
        if (!attrs.TryGetValue(CNC_TYPE, out var cncType) || string.IsNullOrWhiteSpace(cncType))
            return (false, $"Missing required key: {CNC_TYPE}");

        if (!ValidTypes.Contains(cncType))
            return (false, $"Unknown CNC_Type: '{cncType}'. Valid: {string.Join(", ", ValidTypes)}");

        // MACRO requires CNC_MacroName
        if (cncType.Equals("MACRO", StringComparison.OrdinalIgnoreCase)
            && !attrs.ContainsKey(CNC_MACRO_NAME))
            return (false, $"CNC_Type=MACRO requires {CNC_MACRO_NAME}");

        // Validate CNC_Side if present
        if (attrs.TryGetValue(CNC_SIDE, out var side) && !ValidSides.Contains(side))
            return (false, $"Invalid CNC_Side: '{side}'. Valid: {string.Join(", ", ValidSides)}");

        // Validate CNC_Orientation if present
        if (attrs.TryGetValue(CNC_ORIENTATION, out var orientStr))
        {
            if (!int.TryParse(orientStr, out var orient) || !ValidOrientations.Contains(orient))
                return (false, $"Invalid CNC_Orientation: '{orientStr}'. Valid: 0, 90, 180, 270");
        }

        // Validate positive doubles
        if (!ValidatePositiveDouble(attrs, CNC_DIAMETER, out var err)) return (false, err);
        if (!ValidatePositiveDoubleOrPlaceholder(attrs, CNC_DEPTH, out err)) return (false, err);

        // Validate positive integers for pattern counts
        if (!ValidatePositiveInt(attrs, CNC_PATTERN_X, out err)) return (false, err);
        if (!ValidatePositiveInt(attrs, CNC_PATTERN_Y, out err)) return (false, err);

        // Validate non-negative doubles for spacings
        if (!ValidateNonNegativeDouble(attrs, CNC_SPACING_X, out err)) return (false, err);
        if (!ValidateNonNegativeDouble(attrs, CNC_SPACING_Y, out err)) return (false, err);

        return (true, null);
    }

    private static bool ValidatePositiveDouble(IReadOnlyDictionary<string, string> attrs, string key, out string? error)
    {
        error = null;
        if (!attrs.TryGetValue(key, out var v)) return true;
        if (!double.TryParse(v, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var d) || d <= 0)
        {
            error = $"{key} must be a positive number, got: '{v}'";
            return false;
        }
        return true;
    }

    private static bool ValidatePositiveDoubleOrPlaceholder(IReadOnlyDictionary<string, string> attrs, string key, out string? error)
    {
        error = null;
        if (!attrs.TryGetValue(key, out var v)) return true;
        // Allow placeholders like {DZ}, {DZ}-2, etc.
        if (v.Contains('{')) return true;
        if (!double.TryParse(v, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var d) || d <= 0)
        {
            error = $"{key} must be a positive number or placeholder, got: '{v}'";
            return false;
        }
        return true;
    }

    private static bool ValidatePositiveInt(IReadOnlyDictionary<string, string> attrs, string key, out string? error)
    {
        error = null;
        if (!attrs.TryGetValue(key, out var v)) return true;
        if (!int.TryParse(v, out var i) || i < 1)
        {
            error = $"{key} must be a positive integer (>= 1), got: '{v}'";
            return false;
        }
        return true;
    }

    private static bool ValidateNonNegativeDouble(IReadOnlyDictionary<string, string> attrs, string key, out string? error)
    {
        error = null;
        if (!attrs.TryGetValue(key, out var v)) return true;
        if (!double.TryParse(v, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var d) || d < 0)
        {
            error = $"{key} must be a non-negative number, got: '{v}'";
            return false;
        }
        return true;
    }
}
