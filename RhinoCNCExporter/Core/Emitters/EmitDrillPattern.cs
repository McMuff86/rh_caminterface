using RhinoCNCExporter.Core.LayerParser;
using RhinoCNCExporter.Core.Naming;

namespace RhinoCNCExporter.Core.Emitters;

/// <summary>
/// Emits DRILLPAT operations (drill pattern / grid array).
/// Production XCS (e.g. Staub_*, Mittelseite) uses CreatePattern(...) then CreateDrill then ResetPattern.
/// 122 occurrences in production — most common pattern after basic drills.
/// </summary>
public static class EmitDrillPattern
{
    public static string Emit(IEmitter emitter, NameService names, string baseName,
        double x, double y, DrillPatternSpec spec)
    {
        string unique = names.CreateUnique(baseName);
        return emitter.EmitDrillPattern(unique, x, y, spec.Depth, spec.Diameter,
            spec.XCount, spec.YCount, spec.XSpacing, spec.YSpacing,
            "Top", spec.Side.ToString());
    }
}
