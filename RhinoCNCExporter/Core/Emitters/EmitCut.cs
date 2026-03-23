using System;
using System.Collections.Generic;
using RhinoCNCExporter.Core.LayerParser;
using RhinoCNCExporter.Core.Naming;

namespace RhinoCNCExporter.Core.Emitters;

/// <summary>
/// Emits CUT operations (contour routing).
/// Matches emit_cut_operation() from Python reference.
/// Supports both single-pass and layer-stepdown modes.
/// </summary>
public static class EmitCut
{
    public static string Emit(IEmitter emitter, NameService names, string baseName,
        IReadOnlyList<(double X, double Y)> pts, CutSpec spec, bool layerStepdown)
    {
        var parts = new List<string>();

        if (layerStepdown && spec.Stepdown.HasValue)
        {
            int n = (int)Math.Ceiling(spec.Depth / spec.Stepdown.Value);
            for (int i = 1; i <= n; i++)
            {
                double zi = Math.Min(i * spec.Stepdown.Value, spec.Depth);
                string polyName = names.CreateUnique($"{baseName}_Z{zi:F1}");
                string opName = names.CreateUnique($"{baseName}_Z{zi:F1}_OP");
                parts.Add(emitter.EmitPolylinePass(polyName, opName, pts, spec.Tech, zi, spec.ToolDiameter));
            }
        }
        else
        {
            string polyName = names.CreateUnique(baseName);
            string opName = names.CreateUnique($"{baseName}_OP");
            parts.Add(emitter.EmitPolylinePass(polyName, opName, pts, spec.Tech, spec.Depth, spec.ToolDiameter));
        }

        return string.Join("\n", parts);
    }
}
