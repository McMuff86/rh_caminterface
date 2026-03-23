namespace RhinoCNCExporter.Core.Models;

/// <summary>
/// Plate-local coordinate system origin + orientation in world space.
/// Identity = plate lies flat at origin (Phase 1 / 2D mode).
/// </summary>
public sealed record PlateOrigin
{
    public double OriginX { get; init; }
    public double OriginY { get; init; }
    public double OriginZ { get; init; }

    /// <summary>X-axis direction of the plate in world coordinates (unit vector).</summary>
    public (double X, double Y, double Z) XAxis { get; init; } = (1, 0, 0);

    /// <summary>Y-axis direction of the plate in world coordinates (unit vector).</summary>
    public (double X, double Y, double Z) YAxis { get; init; } = (0, 1, 0);

    /// <summary>Normal (Z-axis) of the plate in world coordinates (unit vector).</summary>
    public (double X, double Y, double Z) Normal { get; init; } = (0, 0, 1);

    /// <summary>Identity origin: plate at world origin, flat on XY plane.</summary>
    public static PlateOrigin Identity => new();
}
