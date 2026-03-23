namespace RhinoCNCExporter.Core.Models;

/// <summary>
/// Lightweight plate info for UI display (tree view).
/// Decoupled from RhinoCommon — pure data for presentation.
/// </summary>
public sealed class PlateInfo
{
    /// <summary>Plate name (layer name).</summary>
    public required string Name { get; init; }

    /// <summary>Parent layer group name (null if root-level).</summary>
    public string? GroupName { get; init; }

    /// <summary>Full layer path (e.g., "Korpus_1::Seite_links").</summary>
    public string? LayerPath { get; init; }

    /// <summary>Plate length X in mm.</summary>
    public double LengthX { get; init; }

    /// <summary>Plate width Y in mm.</summary>
    public double WidthY { get; init; }

    /// <summary>Plate thickness in mm.</summary>
    public double Thickness { get; init; }

    /// <summary>Number of machining operations assigned.</summary>
    public int MachiningCount { get; init; }

    /// <summary>Whether this plate is selected for export.</summary>
    public bool IsSelected { get; set; } = true;

    /// <summary>Display string: "Name (LPX × LPY × DZ mm) — N Bearbeitungen"</summary>
    public string DisplayText =>
        $"{Name} ({LengthX:F0} × {WidthY:F0} × {Thickness:F0}mm) — {MachiningCount} Bearbeitung(en)";
}
