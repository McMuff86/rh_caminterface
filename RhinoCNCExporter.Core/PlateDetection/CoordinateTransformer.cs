using RhinoCNCExporter.Core.Models;

namespace RhinoCNCExporter.Core.PlateDetection;

/// <summary>
/// Transforms world coordinates to plate-local coordinates.
///
/// Plate-local system:
///   Origin = bottom-left corner of plate's main face
///   X = along plate length (LPX direction)
///   Y = along plate width (LPY direction)
///   Z = normal to plate face (into the plate = positive depth)
///
/// Phase 1/2: Identity transform (plates are flat at Z=0).
/// Phase 3: Full 3D transform (plates can be anywhere in space, e.g., side panels standing upright).
///
/// The math is pure linear algebra (dot products for projection) and works
/// without RhinoCommon. The PlateDetector (which needs Rhino) computes the
/// PlateOrigin; this class just applies the transform.
/// </summary>
public static class CoordinateTransformer
{
    /// <summary>
    /// Transform a world-space point into plate-local coordinates.
    /// </summary>
    /// <param name="origin">Plate coordinate system origin and axes.</param>
    /// <param name="worldX">World X coordinate.</param>
    /// <param name="worldY">World Y coordinate.</param>
    /// <param name="worldZ">World Z coordinate.</param>
    /// <returns>
    /// (X, Y, Z) in plate-local space where:
    ///   X = position along plate length
    ///   Y = position along plate width
    ///   Z = distance from plate face along normal (positive = into plate)
    /// </returns>
    public static (double X, double Y, double Z) WorldToPlateLocal(
        PlateOrigin origin,
        double worldX, double worldY, double worldZ)
    {
        // Vector from plate origin to point
        double dx = worldX - origin.OriginX;
        double dy = worldY - origin.OriginY;
        double dz = worldZ - origin.OriginZ;

        // Project onto plate axes (dot products)
        double localX = dx * origin.XAxis.X + dy * origin.XAxis.Y + dz * origin.XAxis.Z;
        double localY = dx * origin.YAxis.X + dy * origin.YAxis.Y + dz * origin.YAxis.Z;
        double localZ = dx * origin.Normal.X + dy * origin.Normal.Y + dz * origin.Normal.Z;

        return (localX, localY, localZ);
    }

    /// <summary>
    /// For Phase 1/2: Identity transform (no coordinate change needed).
    /// Plates lie flat at Z=0, origin at world (0,0,0).
    /// </summary>
    public static (double X, double Y, double Z) Identity(double x, double y, double z)
        => (x, y, z);

    /// <summary>
    /// Determine which side of the plate a point is on, based on its local Z coordinate
    /// and the plate thickness.
    /// </summary>
    /// <param name="localZ">Z coordinate in plate-local space.</param>
    /// <param name="plateThickness">Plate thickness in mm.</param>
    /// <param name="tolerance">Tolerance for side detection (default 1mm).</param>
    /// <returns>MachiningSide.Top if near top face, Bottom if near bottom, etc.</returns>
    public static MachiningSide DetermineSide(
        double localZ, double plateThickness, double tolerance = 1.0)
    {
        if (Math.Abs(localZ) <= tolerance)
            return MachiningSide.Top;
        if (Math.Abs(localZ - plateThickness) <= tolerance)
            return MachiningSide.Bottom;
        // Inside the plate — default to Top
        return MachiningSide.Top;
    }

    /// <summary>
    /// Determine which edge side a point is on for horizontal drilling,
    /// based on local X, Y position relative to plate dimensions.
    /// </summary>
    /// <param name="localX">X position in plate-local coords.</param>
    /// <param name="localY">Y position in plate-local coords.</param>
    /// <param name="plateLengthX">Plate length (X dimension).</param>
    /// <param name="plateWidthY">Plate width (Y dimension).</param>
    /// <param name="tolerance">Edge tolerance in mm.</param>
    /// <returns>The edge side, or Top if not near any edge.</returns>
    public static MachiningSide DetermineEdgeSide(
        double localX, double localY,
        double plateLengthX, double plateWidthY,
        double tolerance = 1.0)
    {
        if (Math.Abs(localX) <= tolerance)
            return MachiningSide.Left;
        if (Math.Abs(localX - plateLengthX) <= tolerance)
            return MachiningSide.Right;
        if (Math.Abs(localY) <= tolerance)
            return MachiningSide.Front;
        if (Math.Abs(localY - plateWidthY) <= tolerance)
            return MachiningSide.Back;
        return MachiningSide.Top;
    }

    /// <summary>
    /// Create a PlateOrigin for a plate lying flat at a given position.
    /// The plate's main face is on the XY plane at the given Z height.
    /// </summary>
    /// <param name="originX">World X of plate origin (bottom-left corner).</param>
    /// <param name="originY">World Y of plate origin.</param>
    /// <param name="originZ">World Z of plate origin (bottom of plate).</param>
    /// <returns>PlateOrigin with standard XY axes and Z-up normal.</returns>
    public static PlateOrigin CreateFlatOrigin(double originX, double originY, double originZ)
    {
        return new PlateOrigin
        {
            OriginX = originX,
            OriginY = originY,
            OriginZ = originZ,
            XAxis = (1, 0, 0),
            YAxis = (0, 1, 0),
            Normal = (0, 0, 1)
        };
    }

    /// <summary>
    /// Create a PlateOrigin for a plate standing upright in the XZ plane
    /// (e.g., a side panel). The plate's main face has its normal pointing in the Y direction.
    /// </summary>
    /// <param name="originX">World X of plate origin.</param>
    /// <param name="originY">World Y of plate origin.</param>
    /// <param name="originZ">World Z of plate origin (bottom edge).</param>
    /// <returns>PlateOrigin with X along world-X, Y along world-Z, normal along world-Y.</returns>
    public static PlateOrigin CreateUprightXZOrigin(double originX, double originY, double originZ)
    {
        return new PlateOrigin
        {
            OriginX = originX,
            OriginY = originY,
            OriginZ = originZ,
            XAxis = (1, 0, 0),    // Plate X = World X (length)
            YAxis = (0, 0, 1),    // Plate Y = World Z (height)
            Normal = (0, 1, 0)    // Plate normal = World Y (thickness direction)
        };
    }

    /// <summary>
    /// Create a PlateOrigin for a plate standing upright in the YZ plane
    /// (e.g., a back panel). The plate's main face has its normal pointing in the X direction.
    /// </summary>
    public static PlateOrigin CreateUprightYZOrigin(double originX, double originY, double originZ)
    {
        return new PlateOrigin
        {
            OriginX = originX,
            OriginY = originY,
            OriginZ = originZ,
            XAxis = (0, 1, 0),    // Plate X = World Y
            YAxis = (0, 0, 1),    // Plate Y = World Z (height)
            Normal = (1, 0, 0)    // Plate normal = World X
        };
    }
}
