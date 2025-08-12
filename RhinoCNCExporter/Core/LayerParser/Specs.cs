namespace RhinoCNCExporter.Core.LayerParser;

public sealed record CutSpec(string Tech, double Depth, double? Stepdown, double ToolDiameter);
public sealed record PocketSpec(string Tech, double Depth, double? Stepdown, double ToolDiameter, double? OffsetStep);
public sealed record DrillSpec(double Diameter, double Depth, char Side);
public sealed record DrillRowSpec(double Diameter, double Depth, double Pitch, int? Count);

public enum Axis { X, Y }
public enum Place { Center, Positive }

public sealed record GrooveChannelSpec(Axis Axis, double Width, double Depth, double? Stepdown, string? Tech, Place Place);
public sealed record GrooveRntSpec(Axis Axis, double Width, double Depth, string Code, Place Place);

public static class Defaults
{
    public const double DefaultDz = 19.0;
    public const double DefaultToolDiameter = 9.5;
}
