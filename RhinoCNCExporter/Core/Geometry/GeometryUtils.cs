using System;
using System.Collections.Generic;
using System.Linq;
using Rhino.Geometry;
using RhinoCNCExporter.Core.Emitters;
using RhinoCNCExporter.Core.LayerParser;

namespace RhinoCNCExporter.Core.Geometry;

/// <summary>
/// Geometry helpers: polyline sampling, offset computation, groove construction.
/// Direct port of the Python reference (RH_caminterface_v007.py).
/// </summary>
public static class GeometryUtils
{
    /// <summary>
    /// Convert a Rhino curve to a list of (X,Y) points via polyline approximation.
    /// Matches to_poly_points() from Python. Ensures closure for closed curves.
    /// </summary>
    public static List<(double X, double Y)>? ToPolyPoints(Curve crv, double tol = 0.05)
    {
        var plc = crv.ToPolyline(tol, tol, 0, 0);
        if (plc == null)
        {
            plc = crv.ToPolyline(tol * 0.5, tol * 0.5, 0, 0);
            if (plc == null) return null;
        }

        var polyline = plc.ToPolyline();
        var pts = new List<(double X, double Y)>();
        foreach (var pt in polyline)
            pts.Add((pt.X, pt.Y));

        // Ensure closure
        if (pts.Count > 0)
        {
            var first = pts[0];
            var last = pts[^1];
            if (Math.Abs(first.X - last.X) > 1e-6 || Math.Abs(first.Y - last.Y) > 1e-6)
                pts.Add(first);
        }

        return pts.Count >= 4 ? pts : null;
    }

    /// <summary>
    /// Get bounding box dimensions in XY plane.
    /// Matches bbox_xy() from Python.
    /// </summary>
    public static (double DX, double DY) BBoxXY(Curve crv)
    {
        var bb = crv.GetBoundingBox(true);
        return (bb.Max.X - bb.Min.X, bb.Max.Y - bb.Min.Y);
    }

    /// <summary>
    /// Determine curve orientation sign: -1 for CCW, +1 for CW.
    /// Matches orientation_sign() from Python.
    /// </summary>
    public static int OrientationSign(Curve crv)
    {
        var ori = crv.ClosedCurveOrientation(Plane.WorldXY);
        return ori == CurveOrientation.CounterClockwise ? -1 : 1;
    }

    /// <summary>
    /// Compute area of a closed curve.
    /// Matches curve_area() from Python.
    /// </summary>
    public static double CurveArea(Curve crv)
    {
        var amp = AreaMassProperties.Compute(crv);
        return amp?.Area ?? 0.0;
    }

    /// <summary>
    /// Generate inside offset curves for pocket operations.
    /// Matches inside_offsets() from Python.
    /// Returns list of offset curves (inner rings).
    /// </summary>
    public static List<Curve> InsideOffsets(Curve crv, double stepDist, double tolerance = 0.01)
    {
        var result = new List<Curve>();
        var current = crv;
        int sign = OrientationSign(current);
        double dist = -sign * stepDist; // inward offset

        while (true)
        {
            var arr = current.Offset(Plane.WorldXY, dist, tolerance, CurveOffsetCornerStyle.Sharp);
            if (arr == null || arr.Length == 0) break;

            var best = arr.OrderByDescending(CurveArea).First();
            if (CurveArea(best) <= 1e-6) break;

            result.Add(best);
            current = best;
        }

        return result;
    }

    /// <summary>
    /// Build groove rectangle from a line curve.
    /// Matches build_groove_rect_from_line() from Python.
    /// Returns 5 points (closed rectangle).
    /// </summary>
    public static List<(double X, double Y)> BuildGrooveRectFromLine(
        Curve lineCrv, Axis axis, double width, Place place, double overtravel)
    {
        var p0 = lineCrv.PointAtStart;
        var p1 = lineCrv.PointAtEnd;

        if (axis == Axis.X)
        {
            double y = 0.5 * (p0.Y + p1.Y);
            double xStart = Math.Min(p0.X, p1.X) - overtravel;
            double xEnd = Math.Max(p0.X, p1.X) + overtravel;
            double yLo, yHi;
            if (place == Place.Center)
            {
                yLo = y - 0.5 * width;
                yHi = y + 0.5 * width;
            }
            else // Positive → Y+
            {
                yLo = y;
                yHi = y + width;
            }
            return new List<(double, double)>
            {
                (xStart, yLo), (xEnd, yLo), (xEnd, yHi), (xStart, yHi), (xStart, yLo)
            };
        }
        else // axis == Y
        {
            double x = 0.5 * (p0.X + p1.X);
            double yStart = Math.Min(p0.Y, p1.Y) - overtravel;
            double yEnd = Math.Max(p0.Y, p1.Y) + overtravel;
            double xLo, xHi;
            if (place == Place.Center)
            {
                xLo = x - 0.5 * width;
                xHi = x + 0.5 * width;
            }
            else // Positive → X+
            {
                xLo = x;
                xHi = x + width;
            }
            return new List<(double, double)>
            {
                (xLo, yStart), (xHi, yStart), (xHi, yEnd), (xLo, yEnd), (xLo, yStart)
            };
        }
    }

    /// <summary>
    /// Compute groove endpoints from a line curve for RNT macro.
    /// Matches groove_endpoints_from_line() from Python.
    /// </summary>
    public static EmitGrooveRnt.GrooveEndpoints GrooveEndpointsFromLine(
        Curve lineCrv, Axis axis, Place place, double width, double overtravel)
    {
        var p0 = lineCrv.PointAtStart;
        var p1 = lineCrv.PointAtEnd;

        if (axis == Axis.X)
        {
            double yCenter = 0.5 * (p0.Y + p1.Y);
            double xStart = Math.Min(p0.X, p1.X) - overtravel;
            double xEnd = Math.Max(p0.X, p1.X) + overtravel;

            if (place == Place.Center)
            {
                return new EmitGrooveRnt.GrooveEndpoints
                {
                    XStart = xStart, XEnd = xEnd,
                    YCenter = yCenter,
                    YStart = yCenter - width * 0.5,
                    YEnd = yCenter + width * 0.5
                };
            }
            else // Positive → Y+
            {
                return new EmitGrooveRnt.GrooveEndpoints
                {
                    XStart = xStart, XEnd = xEnd,
                    YCenter = yCenter,
                    YStart = yCenter,
                    YEnd = yCenter + width
                };
            }
        }
        else // Y
        {
            double xCenter = 0.5 * (p0.X + p1.X);
            double yStart = Math.Min(p0.Y, p1.Y) - overtravel;
            double yEnd = Math.Max(p0.Y, p1.Y) + overtravel;

            if (place == Place.Center)
            {
                return new EmitGrooveRnt.GrooveEndpoints
                {
                    YStart = yStart, YEnd = yEnd,
                    XCenter = xCenter,
                    XStart = xCenter - width * 0.5,
                    XEnd = xCenter + width * 0.5
                };
            }
            else // Positive → X+
            {
                return new EmitGrooveRnt.GrooveEndpoints
                {
                    YStart = yStart, YEnd = yEnd,
                    XCenter = xCenter,
                    XStart = xCenter,
                    XEnd = xCenter + width
                };
            }
        }
    }

    /// <summary>
    /// Sample drill row points along a curve at regular pitch.
    /// Matches the drill row logic in emit_drill_row() from Python.
    /// </summary>
    public static List<(double X, double Y)> SampleDrillRowPoints(Curve crv, double pitch, int? count)
    {
        double length = crv.GetLength();
        int n = count ?? ((int)Math.Floor(length / pitch) + 1);

        var points = new List<(double X, double Y)>();
        for (int i = 0; i < n; i++)
        {
            double s = Math.Min(i * pitch, length);
            if (!crv.LengthParameter(s, out double t))
                continue;
            var pt = crv.PointAt(t);
            points.Add((pt.X, pt.Y));
        }
        return points;
    }

    /// <summary>
    /// Convert pocket curve + its inside offsets to lists of polyline points.
    /// Used by EmitPocket.
    /// </summary>
    public static List<IReadOnlyList<(double X, double Y)>> PocketLoops(
        Curve outerCurve, double offsetStep, double polyTol = 0.05, double tolerance = 0.01)
    {
        var result = new List<IReadOnlyList<(double X, double Y)>>();

        // Outer ring
        var outerPts = ToPolyPoints(outerCurve, polyTol);
        if (outerPts != null)
            result.Add(outerPts);

        // Inner offset rings
        var offsets = InsideOffsets(outerCurve, offsetStep, tolerance);
        foreach (var offsetCrv in offsets)
        {
            var pts = ToPolyPoints(offsetCrv, polyTol);
            if (pts != null)
                result.Add(pts);
        }

        return result;
    }
}
