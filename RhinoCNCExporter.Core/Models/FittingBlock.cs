using System.Globalization;

namespace RhinoCNCExporter.Core.Models;

/// <summary>
/// Represents a parsed Block-Insert with CNC_* UserText attributes.
/// Pure data — no RhinoCommon dependency.
/// </summary>
public sealed record FittingBlock
{
    /// <summary>Block definition name (e.g., "Topfband_35", "CLAMEX_P14").</summary>
    public required string BlockName { get; init; }

    /// <summary>CNC operation type from CNC_Type UserText.</summary>
    public required string CncType { get; init; }

    /// <summary>Insertion point in world coordinates.</summary>
    public required (double X, double Y, double Z) InsertionPoint { get; init; }

    /// <summary>Rotation angle in degrees (0, 90, 180, 270).</summary>
    public double Rotation { get; init; }

    /// <summary>All CNC_* UserText key-value pairs from the block instance.</summary>
    public required IReadOnlyDictionary<string, string> CncAttributes { get; init; }

    /// <summary>The Rhino layer this block insert lives on.</summary>
    public string? LayerName { get; init; }

    // --- Convenience accessors for common CNC_* keys ---

    public double? Diameter => TryGetDouble("CNC_Diameter");
    public double? Depth => TryGetDouble("CNC_Depth");
    public string? MacroName => CncAttributes.GetValueOrDefault("CNC_MacroName");
    public string? MacroParams => CncAttributes.GetValueOrDefault("CNC_MacroParams");
    public string? Orientation => CncAttributes.GetValueOrDefault("CNC_Orientation");
    public bool Through => CncAttributes.TryGetValue("CNC_Through", out var v)
        && v.Equals("true", StringComparison.OrdinalIgnoreCase);

    public MachiningSide? CncSide => ParseSide(CncAttributes.GetValueOrDefault("CNC_Side"));

    private double? TryGetDouble(string key)
        => CncAttributes.TryGetValue(key, out var v) && double.TryParse(v,
            NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;

    private static MachiningSide? ParseSide(string? s) => s?.ToUpperInvariant() switch
    {
        "TOP" => MachiningSide.Top,
        "BOTTOM" => MachiningSide.Bottom,
        "LEFT" => MachiningSide.Left,
        "RIGHT" => MachiningSide.Right,
        "FRONT" => MachiningSide.Front,
        "BACK" => MachiningSide.Back,
        _ => null
    };
}
