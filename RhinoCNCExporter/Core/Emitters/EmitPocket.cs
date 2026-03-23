using System;
using System.Collections.Generic;
using RhinoCNCExporter.Core.LayerParser;
using RhinoCNCExporter.Core.Naming;

namespace RhinoCNCExporter.Core.Emitters;

/// <summary>
/// Emits POCKET operations (concentric offset rings from outer boundary inward).
/// Matches emit_pocket() from Python reference.
/// Offset loops are pre-computed by GeometryUtils and passed in as list of point-lists.
/// </summary>
public static class EmitPocket
{
    /// <param name="loops">List of polyline rings (outer + inside offsets), each as list of (X,Y) tuples.</param>
    public static string Emit(IEmitter emitter, NameService names, string baseName,
        IReadOnlyList<IReadOnlyList<(double X, double Y)>> loops, PocketSpec spec, bool layerStepdown)
    {
        var parts = new List<string>();
        double depthTotal = spec.Depth;
        string tech = spec.Tech;
        double toolDia = spec.ToolDiameter;

        if (layerStepdown && spec.Stepdown.HasValue)
        {
            int n = (int)Math.Ceiling(depthTotal / spec.Stepdown.Value);
            for (int i = 1; i <= n; i++)
            {
                double zi = Math.Min(i * spec.Stepdown.Value, depthTotal);
                EmitRingsAtDepth(emitter, names, $"{baseName}_Z{zi:F1}", zi, tech, toolDia, loops, parts);
            }
        }
        else
        {
            EmitRingsAtDepth(emitter, names, baseName, depthTotal, tech, toolDia, loops, parts);
        }

        return string.Join("\n", parts);
    }

    private static void EmitRingsAtDepth(IEmitter emitter, NameService names, string label,
        double depth, string tech, double toolDia,
        IReadOnlyList<IReadOnlyList<(double X, double Y)>> loops, List<string> parts)
    {
        for (int j = 0; j < loops.Count; j++)
        {
            if (loops[j] == null || loops[j].Count < 3)
                continue;
            string polyName = names.CreateUnique($"{label}_ring{j + 1}");
            string opName = names.CreateUnique($"{label}_ring{j + 1}_OP");
            parts.Add(emitter.EmitPolylinePass(polyName, opName, loops[j], tech, depth, toolDia));
        }
    }
}
