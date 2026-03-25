using System.Collections.Generic;

namespace RhinoCNCExporter.Core.Emitters;

/// <summary>
/// Polyline segment — either a straight line or an arc.
/// </summary>
public sealed record PolySegment(
    double EndX, double EndY,
    bool IsArc = false,
    double CenterX = 0, double CenterY = 0,
    bool Clockwise = false);

/// <summary>
/// Interface for CNC code emitters (XCS, CIX, etc.).
/// Abstracts all emitter operations to allow multiple machine formats.
/// </summary>
public interface IEmitter
{
    /// <summary>Emit file header with program name, workpiece dimensions, and setup offsets.</summary>
    string EmitHeader(string programName, double dx, double dy, double dz,
        double setupOffsetX = 2.5, double setupOffsetY = 2.5,
        double setupOffsetZ = 0, double setupOffsetRot = 0);
    
    /// <summary>Emit file footer (e.g., XPARK macro).</summary>
    string EmitFooter();
    
    /// <summary>
    /// Emit a polyline-based routing pass (CUT, POCKET ring, GROOVE channel).
    /// Supports only straight segments.
    /// </summary>
    string EmitPolylinePass(string polyName, string opName, IReadOnlyList<(double X, double Y)> pts,
        string tech, double depth, double toolDia, string plane = "Top");

    /// <summary>
    /// Emit a polyline-based routing pass with mixed line/arc segments.
    /// First point is the start point; segments describe subsequent moves.
    /// </summary>
    string EmitPolylinePassWithArcs(string polyName, string opName,
        double startX, double startY, IReadOnlyList<PolySegment> segments,
        string tech, double depth, double toolDia, string plane = "Top");

    /// <summary>Emit a single drill operation.</summary>
    string EmitDrill(string name, double x, double y, double depth, double dia,
        string plane = "Top", string side = "P");

    /// <summary>
    /// Emit a horizontal (side) drill on a previously created workplane.
    /// Xilog uses a slightly different CreateDrill signature than top drilling.
    /// </summary>
    string EmitHorizontalDrill(string name, double depth, double dia,
        string plane, string side = "P");

    /// <summary>
    /// Emit a drill pattern (grid of holes).
    /// Creates one drill + pattern repeat + reset.
    /// </summary>
    string EmitDrillPattern(string name, double x, double y, double depth, double dia,
        int xCount, int yCount, double xSpacing, double ySpacing,
        string plane = "Top", string side = "P");

    /// <summary>Emit an RNT groove macro (X-axis).</summary>
    string EmitRntX(string name, double xStart, double yCenter, double width,
        double xLen, double depth, string code);
    
    /// <summary>Emit an RNT groove macro (Y-axis).</summary>
    string EmitRntY(string name, double xCenter, double yStart, double width,
        double yLen, double depth, string code);

    /// <summary>Create a named workplane with position and rotation for side drilling.</summary>
    string EmitWorkplane(string name, double x, double y, double z, double rotX, double rotY);

    /// <summary>Select a previously created workplane by name.</summary>
    string EmitSelectWorkplane(string name);

    /// <summary>
    /// Emit a complete BladeCut operation with SectioningMillingStrategy + Segments + BladeCut.
    /// Creates geneigte Schnitte / Fasen for Legrabox-type applications.
    /// </summary>
    string EmitBladeCut(string name, double angle, IReadOnlyList<BladeCutSegment> segments,
        string tech, double depth, SectioningStrategy strategy, string plane = "Top");

    /// <summary>
    /// Emit a CreateHelicMillingStrategy for spiral machining.
    /// Typically used before Rectangle macros for large cutouts.
    /// </summary>
    string EmitHelicMillingStrategy(double radius, bool direction, double depth);
}

/// <summary>
/// Ein Liniensegment für BladeCut-Schnittführung.
/// </summary>
public sealed record BladeCutSegment(
    string Name,
    double StartX, double StartY,
    double EndX, double EndY);

/// <summary>
/// Parameter für CreateSectioningMillingStrategy.
/// </summary>
public sealed record SectioningStrategy(
    int StrategyType = 5,
    double OffsetX = 0,
    double OffsetY = 0);
