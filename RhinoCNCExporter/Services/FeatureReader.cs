using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using RhinoCNCExporter.Core.Blocks;
using RhinoCNCExporter.Core.Models;
using RhinoCNCExporter.Core.PlateDetection;

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
    /// <param name="plate">Optional plate context used to normalize world-space feature geometry into plate-local coordinates</param>
    /// <returns>List of Machining objects created from face tags</returns>
    public static List<Machining> ReadTaggedFeatures(RhinoObject obj, Plate? plate = null)
    {
        var machinings = new List<Machining>();

        if (obj?.Geometry is not Brep brep)
            return machinings;

        try
        {
            var taggedFaces = FaceTagger.GetTaggedFaceIndices(obj)
                .OrderBy(index => index)
                .ToList();
            var seenFeatureIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (int faceIndex in taggedFaces)
            {
                if (faceIndex < 0 || faceIndex >= brep.Faces.Count)
                {
                    RhinoApp.WriteLine($"[FeatureReader] Invalid face index {faceIndex} for object {obj.Id}");
                    continue;
                }

                var tags = FaceTagger.ReadTags(obj, faceIndex);
                if (TryGetFeatureId(tags, out var featureId) && !seenFeatureIds.Add(featureId))
                {
                    continue;
                }

                var machining = ConvertTagsToMachining(obj, faceIndex, tags, brep.Faces[faceIndex], plate);
                if (machining == null)
                {
                    continue;
                }

                if (machining is DrillMachining drill && machinings.OfType<DrillMachining>().Any(existing => AreLikelySameDrill(existing, drill)))
                {
                    continue;
                }

                machinings.Add(machining);
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
    /// <param name="plate">Optional plate context for plate-local coordinate normalization</param>
    /// <returns>Machining object or null if conversion fails</returns>
    private static Machining? ConvertTagsToMachining(RhinoObject obj, int faceIndex, Dictionary<string, string> tags, BrepFace face, Plate? plate)
    {
        if (!tags.TryGetValue("Type", out var typeStr)) // Note: key is without CNC_ prefix after FaceTagger.ReadTags
        {
            RhinoApp.WriteLine($"[FeatureReader] Face {faceIndex} missing CNC_Type");
            return null;
        }

        try
        {
            var name = GetStringTag(tags, "Description", $"FaceFeature_{faceIndex}") ?? $"FaceFeature_{faceIndex}";
            var hasExplicitSide = TryGetMachiningSide(tags, out var side);
            var techCode = GetStringTag(tags, "TechCode", null);

            return typeStr.ToUpperInvariant() switch
            {
                "DRILL" => CreateDrillMachining(obj, faceIndex, tags, face, name, side, hasExplicitSide, techCode, plate),
                "DRILLPATTERN" => CreateDrillPatternMachining(obj, faceIndex, tags, face, name, side, hasExplicitSide, techCode, plate),
                "POCKET" => CreatePocketMachining(obj, faceIndex, tags, face, name, side, hasExplicitSide, techCode, plate),
                "GROOVE" => CreateGrooveMachining(obj, faceIndex, tags, face, name, side, hasExplicitSide, techCode, plate),
                "MACRO" => CreateMacroMachining(obj, faceIndex, tags, face, name, side, hasExplicitSide, techCode, plate),
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
        BrepFace face, string name, MachiningSide side, bool hasExplicitSide, string? techCode, Plate? plate)
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

        var centerPoint = GetFeatureCenterPoint(face, tags);
        var (x, y, _) = ToPlateLocalPoint(centerPoint, plate);
        var resolvedSide = ResolveMachiningSide(side, hasExplicitSide, plate, centerPoint);

        return new DrillMachining
        {
            Name = name,
            Side = resolvedSide,
            TechCode = techCode,
            Source = MachiningSource.FaceTag,
            X = x,
            Y = y,
            Depth = depth,
            Diameter = diameter
        };
    }

    private static DrillPatternMachining? CreateDrillPatternMachining(RhinoObject obj, int faceIndex, Dictionary<string, string> tags,
        BrepFace face, string name, MachiningSide side, bool hasExplicitSide, string? techCode, Plate? plate)
    {
        if (!GetDoubleTag(tags, "Diameter", out var diameter) || diameter <= 0) return null;
        if (!GetDoubleTag(tags, "Depth", out var depth) || depth <= 0) return null;
        if (!GetIntTag(tags, "PatternX", out var countX) || countX < 1) return null;
        if (!GetIntTag(tags, "PatternY", out var countY) || countY < 1) return null;
        if (!GetDoubleTag(tags, "SpacingX", out var spacingX) || spacingX < 0) return null;
        if (!GetDoubleTag(tags, "SpacingY", out var spacingY) || spacingY < 0) return null;

        var centerPoint = GetFeatureCenterPoint(face, tags);
        var (x, y, _) = ToPlateLocalPoint(centerPoint, plate);
        var resolvedSide = ResolveMachiningSide(side, hasExplicitSide, plate, centerPoint);

        return new DrillPatternMachining
        {
            Name = name,
            Side = resolvedSide,
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
        BrepFace face, string name, MachiningSide side, bool hasExplicitSide, string? techCode, Plate? plate)
    {
        if (!GetDoubleTag(tags, "Depth", out var depth) || depth <= 0) return null;
        if (!GetDoubleTag(tags, "ToolDia", out var toolDia) || toolDia <= 0) return null;

        var centerPoint = GetFeatureCenterPoint(face, tags);
        var resolvedSide = ResolveMachiningSide(side, hasExplicitSide, plate, centerPoint);

        // For rectangular pockets, extract boundary from face geometry
        var boundary = ExtractFaceBoundary(face, plate);
        if (boundary.Count == 0)
        {
            RhinoApp.WriteLine($"[FeatureReader] POCKET face {faceIndex}: could not extract boundary");
            return null;
        }

        double? stepDown = GetDoubleTag(tags, "StepDown", out var sd) ? sd : null;

        return new PocketMachining
        {
            Name = name,
            Side = resolvedSide,
            TechCode = techCode,
            Source = MachiningSource.FaceTag,
            Loops = new[] { boundary.AsReadOnly() }.ToList().AsReadOnly(),
            Depth = depth,
            ToolDiameter = toolDia,
            StepDown = stepDown
        };
    }

    private static RoutingMachining? CreateGrooveMachining(RhinoObject obj, int faceIndex, Dictionary<string, string> tags,
        BrepFace face, string name, MachiningSide side, bool hasExplicitSide, string? techCode, Plate? plate)
    {
        if (!GetDoubleTag(tags, "Depth", out var depth) || depth <= 0) return null;
        if (!GetDoubleTag(tags, "Width", out var width) || width <= 0) return null;

        var centerPoint = GetFeatureCenterPoint(face, tags);
        var resolvedSide = ResolveMachiningSide(side, hasExplicitSide, plate, centerPoint);

        // For groove faces, extract centerline path
        var path = ExtractGroovePath(face, plate);
        if (path.Count < 2)
        {
            RhinoApp.WriteLine($"[FeatureReader] GROOVE face {faceIndex}: could not extract path");
            return null;
        }

        double? stepDown = GetDoubleTag(tags, "StepDown", out var sd) ? sd : null;

        return new RoutingMachining
        {
            Name = name,
            Side = resolvedSide,
            TechCode = techCode,
            Source = MachiningSource.FaceTag,
            Points = path.AsReadOnly(),
            Depth = depth,
            ToolDiameter = width,
            StepDown = stepDown,
            IsClosed = false
        };
    }

    private static MacroMachining? CreateMacroMachining(RhinoObject obj, int faceIndex, Dictionary<string, string> tags,
        BrepFace face, string name, MachiningSide side, bool hasExplicitSide, string? techCode, Plate? plate)
    {
        if (!tags.TryGetValue("MacroName", out var macroName) || string.IsNullOrWhiteSpace(macroName))
        {
            RhinoApp.WriteLine($"[FeatureReader] MACRO face {faceIndex}: missing MacroName");
            return null;
        }

        var centerPoint = GetFeatureCenterPoint(face, tags);
        var resolvedSide = ResolveMachiningSide(side, hasExplicitSide, plate, centerPoint);

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
            Side = resolvedSide,
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
        return tags.TryGetValue(key, out var str)
            && double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
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

    private static bool TryGetFeatureId(Dictionary<string, string> tags, out string featureId)
    {
        featureId = string.Empty;
        if (!tags.TryGetValue("FeatureId", out var value) || string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        featureId = value.Trim();
        return featureId.Length > 0;
    }

    private static bool TryGetTaggedCenter(Dictionary<string, string> tags, out double x, out double y)
    {
        x = 0;
        y = 0;
        return GetDoubleTag(tags, "CenterX", out x) && GetDoubleTag(tags, "CenterY", out y);
    }

    private static bool TryGetTaggedCenterPoint(Dictionary<string, string> tags, Point3d fallbackPoint, out Point3d point)
    {
        point = fallbackPoint;
        if (!GetDoubleTag(tags, "CenterX", out var x) || !GetDoubleTag(tags, "CenterY", out var y))
        {
            return false;
        }

        var z = GetDoubleTag(tags, "CenterZ", out var taggedZ) ? taggedZ : fallbackPoint.Z;
        point = new Point3d(x, y, z);
        return true;
    }

    private static bool AreLikelySameDrill(DrillMachining existing, DrillMachining candidate, double positionTolerance = 0.05, double numericTolerance = 0.001)
    {
        return existing.Side == candidate.Side
            && Math.Abs(existing.X - candidate.X) <= positionTolerance
            && Math.Abs(existing.Y - candidate.Y) <= positionTolerance
            && Math.Abs(existing.Diameter - candidate.Diameter) <= numericTolerance
            && Math.Abs(existing.Depth - candidate.Depth) <= numericTolerance;
    }

    private static bool TryGetMachiningSide(Dictionary<string, string> tags, out MachiningSide side)
    {
        side = MachiningSide.Top;
        if (!tags.TryGetValue("Side", out var sideStr) || string.IsNullOrWhiteSpace(sideStr))
            return false;

        switch (sideStr.ToUpperInvariant())
        {
            case "TOP":
                side = MachiningSide.Top;
                return true;
            case "BOTTOM":
                side = MachiningSide.Bottom;
                return true;
            case "LEFT":
                side = MachiningSide.Left;
                return true;
            case "RIGHT":
                side = MachiningSide.Right;
                return true;
            case "FRONT":
                side = MachiningSide.Front;
                return true;
            case "BACK":
                side = MachiningSide.Back;
                return true;
            default:
                return false;
        }
    }

    private static MachiningSide ResolveMachiningSide(
        MachiningSide taggedSide,
        bool hasExplicitSide,
        Plate? plate,
        Point3d referencePoint,
        double tolerance = 1.0)
    {
        if (hasExplicitSide && taggedSide != MachiningSide.Top)
            return taggedSide;

        if (plate == null)
            return taggedSide;

        var (localX, localY, localZ) = ToPlateLocalPoint(referencePoint, plate);
        var inferredSide = CoordinateTransformer.DetermineFeatureSide(
            localX,
            localY,
            localZ,
            plate.LengthX,
            plate.WidthY,
            plate.Thickness,
            tolerance);

        return inferredSide == MachiningSide.Top && hasExplicitSide ? taggedSide : inferredSide;
    }

    private static Point3d GetFeatureCenterPoint(BrepFace face, Dictionary<string, string> tags)
    {
        var geometricCenter = GetFaceCenterPoint(face);
        return TryGetTaggedCenterPoint(tags, geometricCenter, out var taggedCenter)
            ? taggedCenter
            : geometricCenter;
    }

    private static (double X, double Y, double Z) ToPlateLocalPoint(Point3d point, Plate? plate)
    {
        if (plate == null)
            return (point.X, point.Y, point.Z);

        return CoordinateTransformer.WorldToPlateLocal(plate.Origin, point.X, point.Y, point.Z);
    }

    private static (double X, double Y) ToMachinePoint(Point3d point, Plate? plate)
    {
        var (x, y, _) = ToPlateLocalPoint(point, plate);
        return (x, y);
    }

    private static Point3d GetFaceCenterPoint(BrepFace face)
    {
        try
        {
            // Use the trimmed-face centroid first. This is much more reliable for drill bores
            // than sampling the midpoint of the underlying surface domain.
            var area = AreaMassProperties.Compute(face.ToBrep());
            if (area != null)
            {
                return area.Centroid;
            }
        }
        catch (Exception)
        {
            // Fall back to the underlying surface below.
        }

        try
        {
            var surface = face.UnderlyingSurface();
            var uDomain = face.Domain(0);
            var vDomain = face.Domain(1);
            return surface.PointAt(uDomain.Mid, vDomain.Mid);
        }
        catch (Exception)
        {
            return Point3d.Origin;
        }
    }

    private static List<(double X, double Y)> ExtractFaceBoundary(BrepFace face, Plate? plate)
    {
        var boundary = new List<(double X, double Y)>();

        try
        {
            var loop = face.OuterLoop;
            var curve = loop.To3dCurve();

            if (curve != null)
            {
                if (curve.TryGetPolyline(out var poly) && poly != null)
                {
                    boundary.AddRange(poly.Select(pt => ToMachinePoint(pt, plate)));
                }
                else
                {
                    var pts = curve.DivideByCount(20, true);
                    if (pts != null)
                    {
                        boundary.AddRange(pts.Select(t => ToMachinePoint(curve.PointAt(t), plate)));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"[FeatureReader] Error extracting face boundary: {ex.Message}");
        }

        return boundary;
    }

    private static List<(double X, double Y)> ExtractGroovePath(BrepFace face, Plate? plate)
    {
        var path = new List<(double X, double Y)>();

        try
        {
            var boundary = ExtractFaceBoundary(face, plate);
            if (boundary.Count >= 4)
            {
                var start = boundary[0];
                var end = boundary[boundary.Count / 2];
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
