namespace RhinoCNCExporter.Core.Models;

/// <summary>
/// Represents a single plate (Platte) detected from the 3D model.
/// Contains all information needed to generate a CNC program for this plate.
/// </summary>
public sealed record Plate
{
    /// <summary>Unique identifier — typically the layer name or user-assigned name.</summary>
    public required string Name { get; init; }

    /// <summary>Plate length in mm (X-dimension in plate-local coordinates).</summary>
    public required double LengthX { get; init; }

    /// <summary>Plate width in mm (Y-dimension in plate-local coordinates).</summary>
    public required double WidthY { get; init; }

    /// <summary>Plate thickness in mm (Z-dimension).</summary>
    public required double Thickness { get; init; }

    /// <summary>Material identifier (optional, for future use: stückliste, nesting).</summary>
    public string? Material { get; init; }

    /// <summary>Layer path in Rhino (e.g., "Korpus_1::Seite_links").</summary>
    public string? LayerPath { get; init; }

    /// <summary>
    /// Origin of the plate-local coordinate system in world coordinates.
    /// For 2D (Phase 1): always Identity.
    /// For 3D (Phase 3): bottom-left corner of the plate's main face.
    /// </summary>
    public PlateOrigin Origin { get; init; } = PlateOrigin.Identity;

    /// <summary>All machining operations assigned to this plate.</summary>
    public IReadOnlyList<Machining> Machinings { get; init; } = Array.Empty<Machining>();

    /// <summary>
    /// When true, <see cref="RhinoCNCExporter.Core.Pipeline.EmitterRouter"/> emits machinings in <see cref="Machinings"/> list order.
    /// Use for production-reference tests or when CAD+T order must be preserved (e.g. drills, then patterns, then more drills).
    /// Default false keeps type-based ordering (contours, drills, patterns, …).
    /// </summary>
    public bool PreserveMachiningOrder { get; init; }

    /// <summary>Source: how this plate was detected.</summary>
    public PlateSource Source { get; init; } = PlateSource.LegacyLayer;
}
