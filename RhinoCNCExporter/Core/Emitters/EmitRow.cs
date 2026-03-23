using System;
using System.Collections.Generic;
using RhinoCNCExporter.Core.LayerParser;
using RhinoCNCExporter.Core.Naming;

namespace RhinoCNCExporter.Core.Emitters;

/// <summary>
/// Emits DRILLROW operations (drill holes along a curve at regular pitch).
/// Matches emit_drill_row() from Python reference.
/// Points are pre-computed by GeometryUtils and passed in.
/// </summary>
public static class EmitRow
{
    public static string Emit(IEmitter emitter, NameService names, string baseName,
        IReadOnlyList<(double X, double Y)> points, DrillRowSpec spec)
    {
        var parts = new List<string>();
        for (int i = 0; i < points.Count; i++)
        {
            string unique = names.CreateUnique($"{baseName}_{i + 1}");
            parts.Add(emitter.EmitDrill(unique, points[i].X, points[i].Y,
                spec.Depth, spec.Diameter, "Top", "P"));
        }
        return string.Join("\n", parts);
    }
}
