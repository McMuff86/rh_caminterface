using System;
using System.Collections.Generic;
using RhinoCNCExporter.Core.LayerParser;
using RhinoCNCExporter.Core.Naming;

namespace RhinoCNCExporter.Core.Emitters;

/// <summary>
/// Emits horizontal (side) drill operations.
/// Creates a custom workplane rotated to the appropriate face, then drills into it.
/// Production pattern: CreateWorkplane → SelectWorkplane → CreateDrill at (0, 0, depth).
/// </summary>
public static class EmitHorizontalDrill
{
    /// <summary>
    /// Get workplane rotation angles for a given side.
    /// L=Links(-X, rot -90,90), R=Rechts(+X, rot 90,90), V=Vorne(-Y), H=Hinten(+Y)
    /// </summary>
    private static (double RotX, double RotY) GetSideRotation(char side) => side switch
    {
        'L' => (-90.0, 90.0),   // Links: bore into -X face
        'R' => (90.0, 90.0),    // Rechts: verified against production XCS references
        'V' => (-90.0, 0.0),    // Vorne: bore into -Y face
        'H' => (-90.0, 180.0),  // Hinten: bore into +Y face
        _ => throw new ArgumentException($"Unknown drill side: {side}. Expected L/R/V/H.")
    };

    /// <summary>
    /// Get workplane origin position for horizontal drilling.
    /// The workplane is placed at the drill point on the edge, with Z at mid-plate.
    /// </summary>
    /// <param name="x">X position of the drill on the face.</param>
    /// <param name="y">Y position of the drill on the face.</param>
    /// <param name="dz">Workpiece thickness.</param>
    /// <param name="dx">Workpiece width (X dimension).</param>
    /// <param name="dy">Workpiece depth (Y dimension).</param>
    /// <param name="side">Which face: L/R/V/H.</param>
    private static (double X, double Y, double Z) GetWorkplaneOrigin(
        double x, double y, double dz, double dx, double dy, char side) => side switch
    {
        'L' => (0, y, -(dz / 2.0) + dz),      // Left face: X=0
        'R' => (dx, y, -(dz / 2.0) + dz),      // Right face: X=dx
        'V' => (x, 0, -(dz / 2.0) + dz),       // Front face: Y=0
        'H' => (x, dy, -(dz / 2.0) + dz),      // Back face: Y=dy
        _ => throw new ArgumentException($"Unknown drill side: {side}")
    };

    /// <summary>
    /// Emit a horizontal drill with its own workplane.
    /// </summary>
    /// <param name="emitter">CNC format emitter.</param>
    /// <param name="names">Name service for unique names.</param>
    /// <param name="baseName">Base operation name.</param>
    /// <param name="x">X position of the drill center.</param>
    /// <param name="y">Y position of the drill center.</param>
    /// <param name="dz">Workpiece thickness (for Z offset calc).</param>
    /// <param name="dx">Workpiece X dimension.</param>
    /// <param name="dy">Workpiece Y dimension.</param>
    /// <param name="spec">Horizontal drill specification.</param>
    public static string Emit(IEmitter emitter, NameService names, string baseName,
        double x, double y, double dz, double dx, double dy, HorizontalDrillSpec spec)
    {
        var wpName = names.CreateUnique($"Freie Ebene_{baseName}");
        var drillName = names.CreateUnique($"H_Bohrung_{baseName}");

        var (rotX, rotY) = GetSideRotation(spec.DrillSide);
        var (wpX, wpY, wpZ) = GetWorkplaneOrigin(x, y, dz, dx, dy, spec.DrillSide);

        var parts = new List<string>
        {
            emitter.EmitWorkplane(wpName, wpX, wpY, wpZ, rotX, rotY),
            // Drill at local origin (0,0) — the workplane is positioned at the hole location.
            emitter.EmitHorizontalDrill(drillName, spec.Depth, spec.Diameter, wpName, "P")
        };

        return string.Join("", parts);
    }
}
