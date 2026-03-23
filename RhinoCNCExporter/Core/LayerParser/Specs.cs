namespace RhinoCNCExporter.Core.LayerParser;

public sealed record CutSpec(string Tech, double Depth, double? Stepdown, double ToolDiameter);
public sealed record PocketSpec(string Tech, double Depth, double? Stepdown, double ToolDiameter, double? OffsetStep);
public sealed record DrillSpec(double Diameter, double Depth, char Side);
public sealed record DrillRowSpec(double Diameter, double Depth, double Pitch, int? Count);

/// <summary>
/// Drill pattern (grid array of holes).
/// Layer pattern: DRILLPAT_D{dia}_Z{depth}_X{xCount}_Y{yCount}_SX{xSpacing}_SY{ySpacing}
/// </summary>
public sealed record DrillPatternSpec(
    double Diameter, double Depth, char Side,
    int XCount, int YCount,
    double XSpacing, double YSpacing);

/// <summary>
/// Horizontal (side) drill specification.
/// Layer pattern: HDRILL_D{dia}_Z{depth}_S{side} where side = L/R/V/H
/// Side: L=Links(-X), R=Rechts(+X), V=Vorne(-Y), H=Hinten(+Y)
/// </summary>
public sealed record HorizontalDrillSpec(
    double Diameter, double Depth, char DrillSide);

public enum Axis { X, Y }
public enum Place { Center, Positive }

public sealed record GrooveChannelSpec(Axis Axis, double Width, double Depth, double? Stepdown, string? Tech, Place Place);
public sealed record GrooveRntSpec(Axis Axis, double Width, double Depth, string Code, Place Place);

public static class Defaults
{
    public const double DefaultDz = 19.0;
    public const double DefaultToolDiameter = 9.5;
    public const double DefaultPocketStepover = 0.7; // * ToolØ
    public const double GrooveOvertravel = 5.0;      // mm
    public const double PolyTolerance = 0.05;        // mm
    public const double DefaultSetupOffsetX = 2.5;
    public const double DefaultSetupOffsetY = 2.5;
    public const double DefaultSetupOffsetZ = 0.0;
    public const double DefaultSetupOffsetRot = 0.0;
    public const bool UseCornerRounding = false;
    public const bool UseRntMacro = true;
    public const string DefaultGrooveTech = "E010";
}
