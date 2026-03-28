using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using RhinoCNCExporter.Core.Blocks;
using RhinoCNCExporter.Core.Models;

namespace RhinoCNCExporter.Services;

/// <summary>
/// Reads face-tagged CNC features from RhinoObjects and converts them to Machining instances.
/// This provides an additional source of machining operations alongside legacy layers and block detection.
/// </summary>
public static class FeatureReader
{
    /// <summary>
    /// Read all face-tagged CNC features from a RhinoObject and convert to Machining instances.
    /// </summary>
    /// <param name="obj">Rhino object to read features from</param>
    /// <returns>List of Machining objects created from face tags</returns>
    public static List<Machining> ReadTaggedFeatures(RhinoObject obj)
    {
        var machinings = new List<Machining>();

        if (obj?.Geometry is not Brep brep)
            return machinings;

        try
        {
            var taggedFaces = FaceTagger.GetTaggedFaceIndices(obj);

            foreach (int faceIndex in taggedFaces)
            {
                if (faceIndex < 0 || faceIndex >= brep.Faces.Count)
                {
                    RhinoApp.WriteLine($"[FeatureReader] Invalid face index {faceIndex} for object {obj.Id}");
                    continue;
                }

                var tags = FaceTagger.ReadTags(obj, faceIndex);
                var machining = ConvertTagsToMachining(obj, faceIndex, tags, brep.Faces[faceIndex]);

                if (machining != null)
                {
                    machinings.Add(machining);
                }
            }

            RhinoApp.WriteLine($"[FeatureReader] Read {machinings.Count} face-tagged features from object {obj.Id}");
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"[FeatureReader] Error reading tagged features: {ex.Message}");
        }

        return machinings;
    }

    /// <summary>
    /// Convert a set of CNC_* tags from a face to a Machining object.
    /// </summary>
    /// <param name="obj">Source Rhino object</param>
    /// <param name="faceIndex">Face index</param>
    /// <param name="tags">CNC_* tags dictionary</param>
    /// <param name="face">The actual BrepFace for geometric analysis</param>
    /// <returns>Machining object or null if conversion fails</returns>
    private static Machining? ConvertTagsToMachining(RhinoObject obj, int faceIndex, Dictionary<string, string> tags, BrepFace face)
    {
        if (!tags.TryGetValue("Type", out var typeStr)) // Note: key is without CNC_ prefix after FaceTagger.ReadTags
        {
            RhinoApp.WriteLine($"[FeatureReader] Face {faceIndex} missing CNC_Type");
            return null;
        }

        try
        {
            // Get common properties
            var name = GetStringTag(tags, "Description", $"FaceFeature_{faceIndex}") ?? $"FaceFeature_{faceIndex}";
            var side = GetMachiningSide(tags);
            var techCode = GetStringTag(tags, "TechCode", null);

            return typeStr.ToUpper() switch
            {
                "DRILL" => CreateDrillMachining(obj, faceIndex, tags, face, name, side, techCode),
                "DRILLPATTERN" => CreateDrillPatternMachining(obj, faceIndex, tags, face, name, side, techCode),
                "POCKET" => CreatePocketMachining(obj, faceIndex, tags, face, name, side, techCode),
                "GROOVE" => CreateGrooveMachining(obj, faceIndex, tags, face, name, side, techCode),
                "MACRO" => CreateMacroMachining(obj, faceIndex, tags, face, name, side, techCode),
                _ => null
            };
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"[FeatureReader] Error converting face {faceIndex} tags: {ex.Message}");
            return null;
        }
    }

    private static DrillMachining? CreateDrillMachining(RhinoObject obj, int faceIndex, Dictionary<string, string> tags,
        BrepFace face, string name, MachiningSide side, string? techCode)
    {
        if (!GetDoubleTag(tags, "Diameter", out var diameter) || diameter <= 0)
        {
            RhinoApp.WriteLine($"[FeatureReader] DRILL face {faceIndex}: invalid diameter");
            return null;
        }

        if (!GetDoubleTag(tags, "Depth", out var depth) || depth <= 0)
        {
            RhinoApp.WriteLine($"[FeatureReader] DRILL face {faceIndex}: invalid depth");
            return null;
        }

        // For cylindrical faces (drill holes), find the center point
        var (x, y) = GetFaceCenterPoint(face);

        return new DrillMachining
        {
            Name = name,
            Side = side,
            TechCode = techCode,
            Source = MachiningSource.FaceTag,
            X = x,
            Y = y,
            Depth = depth,
            Diameter = diameter
        };
    }

    private static DrillPatternMachining? CreateDrillPatternMachining(RhinoObject obj, int faceIndex, Dictionary<string, string> tags,
        BrepFace face, string name, MachiningSide side, string? techCode)
    {
        if (!GetDoubleTag(tags, "Diameter", out var diameter) || diameter <= 0) return null;
        if (!GetDoubleTag(tags, "Depth", out var depth) || depth <= 0) return null;
        if (!GetIntTag(tags, "PatternX", out var countX) || countX < 1) return null;
        if (!GetIntTag(tags, "PatternY", out var countY) || countY < 1) return null;
        if (!GetDoubleTag(tags, "SpacingX", out var spacingX) || spacingX < 0) return null;
        if (!GetDoubleTag(tags, "SpacingY", out var spacingY) || spacingY < 0) return null;

        var (x, y) = GetFaceCenterPoint(face);

        return new DrillPatternMachining
        {
            Name = name,
            Side = side,
            TechCode = techCode,
            Source = MachiningSource.FaceTag,
            X = x,
            Y = y,
            Depth = depth,
            Diameter = diameter,
            CountX = countX,
            CountY = countY,
            SpacingX = spacingX,
            SpacingY = spacingY
        };
    }

    private static PocketMachining? CreatePocketMachining(RhinoObject obj, int faceIndex, Dictionary<string, string> tags,
        BrepFace face, string name, MachiningSide side, string? techCode)
    {
        if (!GetDoubleTag(tags, "Depth", out var depth) || depth <= 0) return null;
        if (!GetDoubleTag(tags, "ToolDia", out var toolDia) || toolDia <= 0) return null;

        // For rectangular pockets, extract boundary from face geometry
        var boundary = ExtractFaceBoundary(face);
        if (boundary.Count == 0)
        {
            RhinoApp.WriteLine($"[FeatureReader] POCKET face {faceIndex}: could not extract boundary");
            return null;
        }

        double? stepDown = GetDoubleTag(tags, "StepDown", out var sd) ? sd : null;

        return new PocketMachining
        {
            Name = name,
            Side = side,
            TechCode = techCode,
            Source = MachiningSource.FaceTag,
            Loops = new[] { boundary.AsReadOnly() }.ToList().AsReadOnly(),
            Depth = depth,
            ToolDiameter = toolDia,
            StepDown = stepDown
        };
    }

    private static RoutingMachining? CreateGrooveMachining(RhinoObject obj, int faceIndex, Dictionary<string, string> tags,
        BrepFace face, string name, MachiningSide side, string? techCode)
    {
        if (!GetDoubleTag(tags, "Depth", out var depth) || depth <= 0) return null;
        if (!GetDoubleTag(tags, "Width", out var width) || width <= 0) return null;

        // For groove faces, extract centerline path
        var path = ExtractGroovePath(face);
        if (path.Count < 2)
        {
            RhinoApp.WriteLine($"[FeatureReader] GROOVE face {faceIndex}: could not extract path");
            return null;
        }

        double? stepDown = GetDoubleTag(tags, "StepDown", out var sd) ? sd : null;

        return new RoutingMachining
        {
            Name = name,
            Side = side,
            TechCode = techCode,
            Source = MachiningSource.FaceTag,
            Points = path.AsReadOnly(),
            Depth = depth,
            ToolDiameter = width, // For grooves, the width typically matches tool diameter
            StepDown = stepDown,
            IsClosed = false
        };
    }

    private static MacroMachining? CreateMacroMachining(RhinoObject obj, int faceIndex, Dictionary<string, string> tags,
        BrepFace face, string name, MachiningSide side, string? techCode)
    {
        if (!tags.TryGetValue("MacroName", out var macroName) || string.IsNullOrWhiteSpace(macroName))
        {
            RhinoApp.WriteLine($"[FeatureReader] MACRO face {faceIndex}: missing MacroName");
            return null;
        }

        // Parse macro parameters
        var macroParams = new List<string>();
        if (tags.TryGetValue("MacroParams", out var paramStr) && !string.IsNullOrWhiteSpace(paramStr))
        {
            macroParams.AddRange(paramStr.Split(',').Select(p => p.Trim()));
        }

        // Add orientation if specified
        if (tags.TryGetValue("Orientation", out var orientStr) && !string.IsNullOrWhiteSpace(orientStr))
        {
            macroParams.Insert(0, orientStr);
        }

        return new MacroMachining
        {
            Name = name,
            Side = side,
            TechCode = techCode,
            Source = MachiningSource.FaceTag,
            MacroName = macroName,
            Parameters = macroParams.Select(p => (string?)p).ToList().AsReadOnly()
        };
    }

    // --- Helper Methods ---

    private static bool GetDoubleTag(Dictionary<string, string> tags, string key, out double value)
    {
        value = 0;
        return tags.TryGetValue(key, out var str) &&
               double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static bool GetIntTag(Dictionary<string, string> tags, string key, out int value)
    {
        value = 0;
        return tags.TryGetValue(key, out var str) && int.TryParse(str, out value);
    }

    private static string? GetStringTag(Dictionary<string, string> tags, string key, string? defaultValue)
    {
        return tags.TryGetValue(key, out var str) && !string.IsNullOrWhiteSpace(str) ? str : defaultValue;
    }

    private static MachiningSide GetMachiningSide(Dictionary<string, string> tags)
    {
        if (!tags.TryGetValue("Side", out var sideStr))
            return MachiningSide.Top;

        return sideStr.ToUpper() switch
        {
            "TOP" => MachiningSide.Top,
            "BOTTOM" => MachiningSide.Bottom,
            "LEFT" => MachiningSide.Left,
            "RIGHT" => MachiningSide.Right,
            "FRONT" => MachiningSide.Front,
            "BACK" => MachiningSide.Back,
            _ => MachiningSide.Top
        };
    }

    private static (double X, double Y) GetFaceCenterPoint(BrepFace face)
    {
        try
        {
            // Get face domain and evaluate at center
            var surface = face.UnderlyingSurface();
            var uDomain = face.Domain(0);
            var vDomain = face.Domain(1);

            double u = uDomain.Mid;
            double v = vDomain.Mid;

            var centerPoint = surface.PointAt(u, v);
            return (centerPoint.X, centerPoint.Y);
        }
        catch (Exception)
        {
            // Fallback: use centroid of face when surface parameterization fails
            var area = AreaMassProperties.Compute(face.ToBrep());
            var centroid = area?.Centroid ?? new Point3d(0, 0, 0);
            return (centroid.X, centroid.Y);
        }
    }

    private static List<(double X, double Y)> ExtractFaceBoundary(BrepFace face)
    {
        var boundary = new List<(double X, double Y)>();

        try
        {
            // Get outer loop of the face
            var loop = face.OuterLoop;
            var curve = loop.To3dCurve();

            if (curve != null)
            {
                // Sample the curve to get boundary points
                if (curve.TryGetPolyline(out var poly) && poly != null)
                {
                    boundary.AddRange(poly.Select(pt => (pt.X, pt.Y)));
                }
                else
                {
                    // Fallback: divide curve into segments
                    var pts = curve.DivideByCount(20, true);
                    if (pts != null)
                        boundary.AddRange(pts.Select(t => { var p = curve.PointAt(t); return (p.X, p.Y); }));
                }
            }
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"[FeatureReader] Error extracting face boundary: {ex.Message}");
        }

        return boundary;
    }

    private static List<(double X, double Y)> ExtractGroovePath(BrepFace face)
    {
        var path = new List<(double X, double Y)>();

        try
        {
            // For groove faces, try to find the centerline
            // This is a simplified implementation - could be enhanced based on specific groove geometry
            var boundary = ExtractFaceBoundary(face);
            if (boundary.Count >= 4)
            {
                // For rectangular grooves, create a centerline from first to last point
                var start = boundary[0];
                var end = boundary[boundary.Count / 2]; // Approximate opposite corner
                path.Add(start);
                path.Add(end);
            }
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"[FeatureReader] Error extracting groove path: {ex.Message}");
        }

        return path;
    }
}