using RhinoCNCExporter.Core.LayerParser;
using RhinoCNCExporter.Core.Naming;

namespace RhinoCNCExporter.Core.Emitters;

/// <summary>
/// Emits single DRILL operations.
/// Matches emit_drill() from Python reference.
/// </summary>
public static class EmitDrill
{
    public static string Emit(IEmitter emitter, NameService names, string baseName,
        double x, double y, DrillSpec spec)
    {
        string unique = names.CreateUnique(baseName);
        return emitter.EmitDrill(unique, x, y, spec.Depth, spec.Diameter, "Top",
            spec.Side.ToString());
    }
}
