using RhinoCNCExporter.Core.Emitters;

namespace RhinoCNCExporter.Core.Models;

/// <summary>
/// A single machining operation in plate-local coordinates.
/// All positions are relative to the plate's (0,0) = bottom-left corner.
/// Z = depth from top surface (positive = into material).
/// </summary>
public abstract record Machining
{
    /// <summary>Display name for the operation (used in CNC program).</summary>
    public required string Name { get; init; }

    /// <summary>Machining side (Top, Bottom, Left, Right, Front, Back).</summary>
    public MachiningSide Side { get; init; } = MachiningSide.Top;

    /// <summary>Technology code (e.g., "E010", "E013").</summary>
    public string? TechCode { get; init; }

    /// <summary>Source of this machining (legacy layer, block, or manual).</summary>
    public MachiningSource Source { get; init; } = MachiningSource.LegacyLayer;
}

// --- Concrete machining types ---

public sealed record DrillMachining : Machining
{
    public required double X { get; init; }
    public required double Y { get; init; }
    public required double Depth { get; init; }
    public required double Diameter { get; init; }
}

public sealed record DrillPatternMachining : Machining
{
    public required double X { get; init; }
    public required double Y { get; init; }
    public required double Depth { get; init; }
    public required double Diameter { get; init; }
    public required int CountX { get; init; }
    public required int CountY { get; init; }
    public required double SpacingX { get; init; }
    public required double SpacingY { get; init; }
}

public sealed record RoutingMachining : Machining
{
    /// <summary>Polyline points in plate-local coordinates (closed = contour, open = groove).</summary>
    public required IReadOnlyList<(double X, double Y)> Points { get; init; }
    public required double Depth { get; init; }
    public required double ToolDiameter { get; init; }
    public double? StepDown { get; init; }
    public bool IsClosed { get; init; }
}

public sealed record RoutingWithArcsMachining : Machining
{
    public required double StartX { get; init; }
    public required double StartY { get; init; }
    public required IReadOnlyList<PolySegment> Segments { get; init; }
    public required double Depth { get; init; }
    public required double ToolDiameter { get; init; }
    public double? StepDown { get; init; }
    public bool IsClosed { get; init; }
}

public sealed record PocketMachining : Machining
{
    /// <summary>Offset loops from outside to inside.</summary>
    public required IReadOnlyList<IReadOnlyList<(double X, double Y)>> Loops { get; init; }
    public required double Depth { get; init; }
    public required double ToolDiameter { get; init; }
    public double? StepDown { get; init; }
}

public sealed record GrooveRntMachining : Machining
{
    public required LayerParser.Axis Axis { get; init; }
    public required double XStart { get; init; }
    public required double YStart { get; init; }
    public required double Length { get; init; }
    public required double Width { get; init; }
    public required double Depth { get; init; }
    public required string RntCode { get; init; }
}

public sealed record MacroMachining : Machining
{
    /// <summary>Macro name (e.g., "SawCut_Lamello", "RNT", "Rectangle").</summary>
    public required string MacroName { get; init; }
    /// <summary>Ordered list of macro parameters (strings, nulls, numbers).</summary>
    public required IReadOnlyList<string?> Parameters { get; init; }
}

public sealed record HorizontalDrillMachining : Machining
{
    public required double X { get; init; }
    public required double Y { get; init; }
    public required double Depth { get; init; }
    public required double Diameter { get; init; }
    /// <summary>Which plate edge: L=Links(-X), R=Rechts(+X), V=Vorne(-Y), H=Hinten(+Y).</summary>
    public required char DrillSide { get; init; }
}

/// <summary>
/// Geneigte Schnitte / Fasen mit BladeCut-Befehlen.
/// Erzeugt CreateSectioningMillingStrategy + CreateSegment + CreateBladeCut als Einheit.
/// </summary>
public sealed record BladeCutMachining : Machining
{
    /// <summary>Schnittwinkel in Grad (z.B. 45.0 für Fasen).</summary>
    public required double Angle { get; init; }
    /// <summary>Segmente für die Schnittführung (Start/End-Punkte).</summary>
    public required IReadOnlyList<BladeCutSegment> Segments { get; init; }
    /// <summary>Schnitttiefe in mm.</summary>
    public required double Depth { get; init; }
    /// <summary>Optionale Strategie-Parameter (default: 5, 0, 0).</summary>
    public SectioningStrategy Strategy { get; init; } = new(5, 0, 0);
}

// --- Supporting types for BladeCut ---

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
