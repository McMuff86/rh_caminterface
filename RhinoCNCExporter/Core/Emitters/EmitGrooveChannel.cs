using System;
using System.Collections.Generic;
using RhinoCNCExporter.Core.LayerParser;
using RhinoCNCExporter.Core.Naming;

namespace RhinoCNCExporter.Core.Emitters;

/// <summary>
/// Emits RBNUT_CH (channel groove) operations.
/// Matches emit_rbnut_channel() from Python reference.
/// Groove rectangle points are pre-computed by GeometryUtils.
/// </summary>
public static class EmitGrooveChannel
{
    public static string Emit(IEmitter emitter, NameService names, string baseName,
        IReadOnlyList<(double X, double Y)> rectPts, GrooveChannelSpec spec, bool layerStepdown)
    {
        string tech = spec.Tech ?? Defaults.DefaultGrooveTech;
        double toolDia = Defaults.DefaultToolDiameter;
        var parts = new List<string>();

        if (layerStepdown && spec.Stepdown.HasValue)
        {
            int n = (int)Math.Ceiling(spec.Depth / spec.Stepdown.Value);
            for (int i = 1; i <= n; i++)
            {
                double zi = Math.Min(i * spec.Stepdown.Value, spec.Depth);
                string polyName = names.CreateUnique($"{baseName}_Z{zi:F1}");
                string opName = names.CreateUnique($"{baseName}_Z{zi:F1}_OP");
                parts.Add(emitter.EmitPolylinePass(polyName, opName, rectPts, tech, zi, toolDia));
            }
        }
        else
        {
            string polyName = names.CreateUnique(baseName);
            string opName = names.CreateUnique($"{baseName}_OP");
            parts.Add(emitter.EmitPolylinePass(polyName, opName, rectPts, tech, spec.Depth, toolDia));
        }

        return string.Join("\n", parts);
    }
}
