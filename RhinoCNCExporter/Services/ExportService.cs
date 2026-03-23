using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using RhinoCNCExporter.Core.Emitters;
using RhinoCNCExporter.Core.Geometry;
using RhinoCNCExporter.Core.LayerParser;
using RhinoCNCExporter.Core.Naming;
using RhinoCNCExporter.Core.Profiles;

namespace RhinoCNCExporter.Services;

/// <summary>
/// Orchestrates the CNC export pipeline: collect geometry → parse layers → emit CNC code.
/// Supports XCS (SCM/Maestro), CIX (Biesse), and future formats via IEmitter.
/// </summary>
public static class ExportService
{
    /// <summary>Export CNC program using specified emitter and profile.</summary>
    public static bool ExportCNC(RhinoDoc doc, bool onlySelection, string filePath,
        IEmitter emitter, NameService nameService, IMachineProfile profile,
        bool layerStepdown = false)
    {
        return ExportWithEmitter(doc, onlySelection, filePath, emitter, nameService,
            profile.SetupOffsetX, profile.SetupOffsetY, profile.SetupOffsetZ, profile.SetupOffsetRot,
            layerStepdown);
    }

    /// <summary>Export Xilog Script (.xcs) with configurable setup offsets.</summary>
    public static bool ExportXilog(RhinoDoc doc, bool onlySelection, string filePath,
        bool layerStepdown = false,
        double setupOffsetX = 2.5, double setupOffsetY = 2.5)
    {
        var nameService = new NameService(maxLength: 31);
        var emitter = new XilogEmitter(nameService);
        return ExportWithEmitter(doc, onlySelection, filePath, emitter, nameService,
            setupOffsetX, setupOffsetY, 0, 0, layerStepdown);
    }

    /// <summary>Export Biesse CIX with configurable setup offsets.</summary>
    public static bool ExportBiesse(RhinoDoc doc, bool onlySelection, string filePath,
        bool layerStepdown = false,
        double setupOffsetX = 2.5, double setupOffsetY = 2.5)
    {
        var nameService = new NameService(maxLength: 63);
        var emitter = new BiesseEmitter(nameService);
        return ExportWithEmitter(doc, onlySelection, filePath, emitter, nameService,
            setupOffsetX, setupOffsetY, 0, 0, layerStepdown);
    }

    /// <summary>Internal export logic using any IEmitter implementation.</summary>
    private static bool ExportWithEmitter(RhinoDoc doc, bool onlySelection, string filePath,
        IEmitter emitter, NameService nameService,
        double setupOffsetX, double setupOffsetY, double setupOffsetZ, double setupOffsetRot,
        bool layerStepdown)
    {
        try
        {
            var programName = Path.GetFileNameWithoutExtension(filePath);

            // Collect geometry by type
            var pieceCurves = new List<Curve>();
            var cuts = new List<(Curve Crv, string Layer)>();
            var drills = new List<(double X, double Y, string Layer)>();
            var drillPatterns = new List<(double X, double Y, string Layer)>();
            var horizontalDrills = new List<(double X, double Y, string Layer)>();
            var rows = new List<(Curve Crv, string Layer)>();
            var pockets = new List<(Curve Crv, string Layer)>();
            var grooves = new List<(Curve Crv, string Layer)>();
            var groovesRnt = new List<(Curve Crv, string Layer)>();

            IEnumerable<RhinoObject> objects;
            if (onlySelection)
            {
                objects = doc.Objects.GetSelectedObjects(includeLights: false, includeGrips: false)
                    .OfType<RhinoObject>();
            }
            else
            {
                objects = doc.Objects.Where(o => o != null);
            }

            foreach (var ro in objects)
            {
                var layerIndex = ro.Attributes.LayerIndex;
                var layer = doc.Layers.FindIndex(layerIndex);
                if (layer == null) continue;
                var layerName = layer.Name ?? string.Empty;
                if (string.IsNullOrWhiteSpace(layerName)) continue;

                var crv = ro.Geometry as Curve;

                // WK_PIECE
                if (crv != null && layerName == "WK_PIECE" && crv.IsClosed)
                {
                    pieceCurves.Add(crv);
                    continue;
                }

                // CUT
                if (crv != null && LayerRegex.TryParseCut(layerName, out _) && crv.IsClosed)
                {
                    cuts.Add((crv, layerName));
                    continue;
                }

                // POCKET
                if (crv != null && LayerRegex.TryParsePocket(layerName, out _) && crv.IsClosed)
                {
                    pockets.Add((crv, layerName));
                    continue;
                }

                // DRILLROW (open curves)
                if (crv != null && LayerRegex.TryParseRow(layerName, out _) && !crv.IsClosed)
                {
                    rows.Add((crv, layerName));
                    continue;
                }

                // DRILLPAT — drill patterns (circles/points like normal drills)
                if (LayerRegex.TryParseDrillPattern(layerName, out _))
                {
                    if (TryGetDrillCenter(ro, out double dpx, out double dpy))
                    {
                        drillPatterns.Add((dpx, dpy, layerName));
                        continue;
                    }
                }

                // HDRILL — horizontal drills (circles/points)
                if (LayerRegex.TryParseHorizontalDrill(layerName, out _))
                {
                    if (TryGetDrillCenter(ro, out double hx, out double hy))
                    {
                        horizontalDrills.Add((hx, hy, layerName));
                        continue;
                    }
                }

                // DRILL — circles, points, arc-circles
                if (LayerRegex.TryParseDrill(layerName, out _))
                {
                    if (TryGetDrillCenter(ro, out double dx, out double dy))
                    {
                        drills.Add((dx, dy, layerName));
                        continue;
                    }
                }

                // RBNUT_CH
                if (crv != null && LayerRegex.TryParseGrooveChannel(layerName, out _))
                {
                    grooves.Add((crv, layerName));
                    continue;
                }

                // RBNUT_RNT
                if (crv != null && LayerRegex.TryParseGrooveRnt(layerName, out _))
                {
                    groovesRnt.Add((crv, layerName));
                    continue;
                }
            }

            // Must have a workpiece
            if (pieceCurves.Count == 0)
                return false;

            // Find largest piece curve
            var outer = pieceCurves.OrderByDescending(GeometryUtils.CurveArea).First();
            var (wpDx, wpDy) = GeometryUtils.BBoxXY(outer);

            var parts = new List<string>();
            parts.Add(emitter.EmitHeader(programName, wpDx, wpDy, Defaults.DefaultDz,
                setupOffsetX, setupOffsetY, setupOffsetZ, setupOffsetRot));

            // Fallback: if nothing but WK_PIECE, route the outer contour
            if (cuts.Count == 0 && pockets.Count == 0 && drills.Count == 0 &&
                drillPatterns.Count == 0 && horizontalDrills.Count == 0 &&
                rows.Count == 0 && grooves.Count == 0 && groovesRnt.Count == 0)
            {
                var pts = GeometryUtils.ToPolyPoints(outer, Defaults.PolyTolerance);
                if (pts != null)
                {
                    string polyName = nameService.CreateUnique("Aussenkontur");
                    string opName = nameService.CreateUnique("Aussenkontur_OP");
                    parts.Add(emitter.EmitPolylinePass(polyName, opName, pts,
                        "E010", Defaults.DefaultDz, Defaults.DefaultToolDiameter));
                }
            }

            // CUT operations
            for (int idx = 0; idx < cuts.Count; idx++)
            {
                var (crv, layerName) = cuts[idx];
                if (!LayerRegex.TryParseCut(layerName, out var spec)) continue;
                var pts = GeometryUtils.ToPolyPoints(crv, Defaults.PolyTolerance);
                if (pts == null) continue;
                parts.Add(EmitCut.Emit(emitter, nameService, $"CUT_{idx + 1}", pts, spec!, layerStepdown));
            }

            // POCKET operations
            for (int idx = 0; idx < pockets.Count; idx++)
            {
                var (crv, layerName) = pockets[idx];
                if (!LayerRegex.TryParsePocket(layerName, out var spec)) continue;
                double offsetStep = spec!.OffsetStep ?? spec.ToolDiameter * Defaults.DefaultPocketStepover;
                var loops = GeometryUtils.PocketLoops(crv, offsetStep, Defaults.PolyTolerance);
                if (loops.Count == 0) continue;
                parts.Add(EmitPocket.Emit(emitter, nameService, $"POCKET_{idx + 1}", loops, spec, layerStepdown));
            }

            // DRILL operations
            for (int idx = 0; idx < drills.Count; idx++)
            {
                var (x, y, layerName) = drills[idx];
                if (!LayerRegex.TryParseDrill(layerName, out var spec)) continue;
                parts.Add(EmitDrill.Emit(emitter, nameService, $"DRILL_{idx + 1}", x, y, spec!));
            }

            // DRILLPAT operations (drill patterns / grid arrays)
            for (int idx = 0; idx < drillPatterns.Count; idx++)
            {
                var (x, y, layerName) = drillPatterns[idx];
                if (!LayerRegex.TryParseDrillPattern(layerName, out var spec)) continue;
                parts.Add(EmitDrillPattern.Emit(emitter, nameService, $"DRILLPAT_{idx + 1}", x, y, spec!));
            }

            // DRILLROW operations
            for (int idx = 0; idx < rows.Count; idx++)
            {
                var (crv, layerName) = rows[idx];
                if (!LayerRegex.TryParseRow(layerName, out var spec)) continue;
                var points = GeometryUtils.SampleDrillRowPoints(crv, spec!.Pitch, spec.Count);
                parts.Add(EmitRow.Emit(emitter, nameService, $"DRILLROW_{idx + 1}", points, spec));
            }

            // HDRILL operations (horizontal / side drilling)
            for (int idx = 0; idx < horizontalDrills.Count; idx++)
            {
                var (x, y, layerName) = horizontalDrills[idx];
                if (!LayerRegex.TryParseHorizontalDrill(layerName, out var spec)) continue;
                parts.Add(EmitHorizontalDrill.Emit(emitter, nameService, $"HDRILL_{idx + 1}",
                    x, y, Defaults.DefaultDz, wpDx, wpDy, spec!));
            }

            // RBNUT_RNT operations (before channel grooves, matching Python order)
            for (int idx = 0; idx < groovesRnt.Count; idx++)
            {
                var (crv, layerName) = groovesRnt[idx];
                if (!LayerRegex.TryParseGrooveRnt(layerName, out var spec)) continue;
                var ends = GeometryUtils.GrooveEndpointsFromLine(crv, spec!.Axis,
                    spec.Place, spec.Width, Defaults.GrooveOvertravel);
                parts.Add(EmitGrooveRnt.Emit(emitter, nameService, $"RBNUT_RNT_{idx + 1}", ends, spec));
            }

            // RBNUT_CH operations
            for (int idx = 0; idx < grooves.Count; idx++)
            {
                var (crv, layerName) = grooves[idx];
                if (!LayerRegex.TryParseGrooveChannel(layerName, out var spec)) continue;
                var rectPts = GeometryUtils.BuildGrooveRectFromLine(crv, spec!.Axis,
                    spec.Width, spec.Place, Defaults.GrooveOvertravel);
                parts.Add(EmitGrooveChannel.Emit(emitter, nameService, $"RBNUT_{idx + 1}", rectPts, spec, layerStepdown));
            }

            // Footer
            parts.Add(emitter.EmitFooter());

            // Write file
            var content = string.Join("\n", parts);
            File.WriteAllText(filePath, content, Encoding.UTF8);
            return true;
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"[RhinoCNCExporter] Export error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Try to extract drill center point from various geometry types.
    /// Matches Python's drill detection logic (Circle, Point, ArcCurve).
    /// </summary>
    private static bool TryGetDrillCenter(RhinoObject ro, out double x, out double y)
    {
        x = y = 0;
        var geom = ro.Geometry;

        if (geom is Point pt)
        {
            x = pt.Location.X;
            y = pt.Location.Y;
            return true;
        }

        if (geom is ArcCurve arc && arc.IsCircle())
        {
            var center = arc.Arc.Center;
            x = center.X;
            y = center.Y;
            return true;
        }

        if (geom is Curve crv && crv.TryGetCircle(out Circle circle))
        {
            x = circle.Center.X;
            y = circle.Center.Y;
            return true;
        }

        return false;
    }
}
