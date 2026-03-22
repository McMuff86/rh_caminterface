using System;
using System.Collections.Generic;
using RhinoCNCExporter.Core.LayerParser;
using RhinoCNCExporter.Core.Naming;

namespace RhinoCNCExporter.Core.Emitters;

/// <summary>
/// Emits RBNUT_RNT (RNT macro groove) operations.
/// Matches emit_rbnut_rnt() from Python reference.
/// Groove endpoints are pre-computed by GeometryUtils.
/// </summary>
public static class EmitGrooveRnt
{
    /// <summary>
    /// Groove endpoint data computed from the line geometry.
    /// Matches groove_endpoints_from_line() output from Python.
    /// </summary>
    public sealed class GrooveEndpoints
    {
        public double XStart { get; init; }
        public double XEnd { get; init; }
        public double YStart { get; init; }
        public double YEnd { get; init; }
        public double XCenter { get; init; }
        public double YCenter { get; init; }
    }

    public static string Emit(XilogEmitter emitter, NameService names, string baseName,
        GrooveEndpoints ends, GrooveRntSpec spec)
    {
        // Python uses descriptive names like "Nut in X-Richtung"
        string niceName = spec.Axis == Axis.X ? "Nut_in_X_Richtung" : "Nut_in_Y_Richtung";
        string macroName = names.CreateUnique(niceName);

        if (spec.Axis == Axis.X)
        {
            double xLen = Math.Abs(ends.XEnd - ends.XStart);
            return emitter.EmitRntX(macroName, ends.XStart, ends.YCenter,
                spec.Width, xLen, spec.Depth, spec.Code);
        }
        else
        {
            double yLen = Math.Abs(ends.YEnd - ends.YStart);
            return emitter.EmitRntY(macroName, ends.XCenter, ends.YStart,
                spec.Width, yLen, spec.Depth, spec.Code);
        }
    }
}
