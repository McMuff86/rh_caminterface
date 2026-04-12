using System.Drawing;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using RhinoCNCExporter.Core.Blocks;

namespace RhinoCNCExporter.Services;

/// <summary>
/// Generates visible toolpath preview geometry for CNC operations.
/// Replaces TextDots with meaningful visual feedback showing cut width,
/// direction, and toolpath patterns.
/// </summary>
public static class ToolpathVisualizer
{
    // Layer names
    private const string RootLayerName = "CNC_Toolpaths";
    private const string ContourSubLayer = "Contour";
    private const string PocketSubLayer = "Pocket";
    private const string DrillSubLayer = "Drill";
    private const string GrooveSubLayer = "Groove";

    // Arrow spacing along curves (mm)
    private const double ArrowSpacing = 50.0;
    // Arrow size (mm)
    private const double ArrowSize = 3.0;
    // Plot weight for toolpath lines
    private const double PlotWeight = 0.25;

    /// <summary>
    /// Operation-type colors matching CncOperationService.SetOperationColor.
    /// </summary>
    private static Color GetOperationColor(string operationType) => operationType switch
    {
        CncOperationSchema.TYPE_CONTOUR => Color.Red,
        CncOperationSchema.TYPE_POCKET => Color.Blue,
        CncOperationSchema.TYPE_DRILL => Color.Yellow,
        CncOperationSchema.TYPE_GROOVE => Color.Green,
        _ => Color.Gray
    };

    /// <summary>
    /// Creates toolpath visualization for a contour or groove operation.
    /// Generates offset curves (left/right) and direction arrows.
    /// </summary>
    public static List<GeometryBase> CreateContourToolpath(Curve sourceCurve, double toolDiameter)
    {
        var result = new List<GeometryBase>();
        if (sourceCurve == null) return result;

        var radius = toolDiameter / 2.0;

        if (radius <= 0.01) return result;

        // Determine the best plane for offsetting — use the curve's own plane
        // instead of WorldXY to handle curves at any Z height or orientation
        var plane = GetCurvePlane(sourceCurve);
        var doc = RhinoDoc.ActiveDoc;
        if (doc == null) return result;
        var tolerance = doc.ModelAbsoluteTolerance;

        var offsets = sourceCurve.Offset(plane, radius, tolerance, CurveOffsetCornerStyle.Sharp);
        if (offsets != null)
            result.AddRange(offsets);

        offsets = sourceCurve.Offset(plane, -radius, tolerance, CurveOffsetCornerStyle.Sharp);
        if (offsets != null)
            result.AddRange(offsets);

        // Add direction arrows along the curve
        result.AddRange(CreateDirectionArrows(sourceCurve));

        return result;
    }

    /// <summary>
    /// Creates toolpath visualization for a pocket operation.
    /// Generates concentric inward offset curves and an entry point marker.
    /// </summary>
    public static List<GeometryBase> CreatePocketToolpath(Curve boundaryCurve, double toolDiameter, double stepoverPercent)
    {
        var result = new List<GeometryBase>();
        if (boundaryCurve == null) return result;

        var radius = toolDiameter / 2.0;
        var stepover = toolDiameter * (stepoverPercent / 100.0);

        if (radius <= 0.01 || stepover <= 0.01) return result;

        var doc = RhinoDoc.ActiveDoc;
        if (doc == null) return result;
        var tolerance = doc.ModelAbsoluteTolerance;
        var plane = GetCurvePlane(boundaryCurve);

        // Generate concentric offset curves inward
        var currentOffset = radius;
        var maxIterations = 200; // Safety limit
        var iteration = 0;

        while (iteration < maxIterations)
        {
            var offsets = boundaryCurve.Offset(plane, -currentOffset, tolerance, CurveOffsetCornerStyle.Sharp);
            if (offsets == null || offsets.Length == 0) break;

            var addedAny = false;
            foreach (var offset in offsets)
            {
                // Only keep valid offsets (area > 0 means it hasn't collapsed)
                if (offset.GetLength() > toolDiameter * 0.5)
                {
                    result.Add(offset);
                    addedAny = true;
                }
            }

            if (!addedAny) break;

            currentOffset += stepover;
            iteration++;
        }

        // Add entry point marker (small circle at start of boundary)
        var entryPoint = boundaryCurve.PointAtStart;
        var entryCircle = new ArcCurve(new Circle(entryPoint, toolDiameter * 0.3));
        result.Add(entryCircle);

        return result;
    }

    /// <summary>
    /// Creates toolpath visualization for a drill operation.
    /// Generates a circle showing hole diameter and a crosshair at center.
    /// </summary>
    public static List<GeometryBase> CreateDrillToolpath(Point3d center, double diameter, bool includeOutline = true)
    {
        var result = new List<GeometryBase>();
        var radius = diameter / 2.0;

        if (radius <= 0.01 || !center.IsValid) return result;

        if (includeOutline)
        {
            // Circle showing hole diameter
            var circle = new ArcCurve(new Circle(center, radius));
            result.Add(circle);
        }

        // Crosshair (+) at center
        var crossSize = radius * 0.6;
        var hLine = new LineCurve(
            new Point3d(center.X - crossSize, center.Y, center.Z),
            new Point3d(center.X + crossSize, center.Y, center.Z));
        var vLine = new LineCurve(
            new Point3d(center.X, center.Y - crossSize, center.Z),
            new Point3d(center.X, center.Y + crossSize, center.Z));

        result.Add(hLine);
        result.Add(vLine);

        return result;
    }

    /// <summary>
    /// Creates drill visualization directly from source geometry.
    /// When the source object is already a drill circle of the same diameter,
    /// only the crosshair is generated to avoid a confusing duplicate outline.
    /// </summary>
    public static List<GeometryBase> CreateDrillToolpath(GeometryBase geometry, double diameter)
    {
        if (!TryResolveDrillPreview(geometry, diameter, out var center, out var includeOutline))
            return new List<GeometryBase>();

        return CreateDrillToolpath(center, diameter, includeOutline);
    }

    /// <summary>
    /// Adds toolpath geometry to the document, groups it with the source object,
    /// and stores the group index as UserText.
    /// </summary>
    public static void AddToolpathToDocument(
        RhinoDoc doc,
        RhinoObject sourceObject,
        string operationType,
        List<GeometryBase> toolpathGeometry)
    {
        if (doc == null || sourceObject == null || toolpathGeometry == null || toolpathGeometry.Count == 0) return;

        var layerIndex = EnsureToolpathSubLayer(doc, operationType);
        var color = GetOperationColor(operationType);

        var objectIds = new List<Guid> { sourceObject.Id };

        foreach (var geom in toolpathGeometry)
        {
            var attributes = new ObjectAttributes
            {
                LayerIndex = layerIndex,
                ColorSource = ObjectColorSource.ColorFromObject,
                ObjectColor = color,
                PlotWeight = PlotWeight,
                PlotWeightSource = ObjectPlotWeightSource.PlotWeightFromObject,
            };

            var id = Guid.Empty;
            if (geom is Curve curve)
                id = doc.Objects.AddCurve(curve, attributes);
            else if (geom is Rhino.Geometry.Point point)
                id = doc.Objects.AddPoint(point.Location, attributes);

            if (id != Guid.Empty)
                objectIds.Add(id);
        }

        // Group everything together (source + toolpath geometry)
        if (objectIds.Count > 1)
        {
            var groupIndex = doc.Groups.Add(objectIds);
            // Store group index on the source object for later removal
            sourceObject.Attributes.SetUserString(CncOperationSchema.CNC_GROUP_INDEX, groupIndex.ToString());
            sourceObject.CommitChanges();
        }
    }

    // --- 3D Toolpath Layer ---
    private const string Root3DLayerName = "CNC_Toolpaths_3D";

    /// <summary>
    /// Creates 3D toolpath visualization for a contour/groove at actual cutting depth.
    /// Shows top curve, bottom curve (at depth), and vertical connection lines.
    /// </summary>
    public static List<GeometryBase> CreateContourToolpath3D(Curve sourceCurve, double toolDiameter, double depth)
    {
        var result = new List<GeometryBase>();
        if (sourceCurve == null) return result;
        if (depth <= 0.001) return CreateContourToolpath(sourceCurve, toolDiameter);

        var radius = toolDiameter / 2.0;
        var plane = GetCurvePlane(sourceCurve);
        var doc = RhinoDoc.ActiveDoc;
        if (doc == null) return result;
        var tolerance = doc.ModelAbsoluteTolerance;

        // Top offset curves (at surface level)
        var topLeft = sourceCurve.Offset(plane, radius, tolerance, CurveOffsetCornerStyle.Sharp);
        var topRight = sourceCurve.Offset(plane, -radius, tolerance, CurveOffsetCornerStyle.Sharp);

        if (topLeft != null) result.AddRange(topLeft);
        if (topRight != null) result.AddRange(topRight);

        // Bottom curves (at cutting depth) — translate down by depth along the plane normal
        var depthTransform = Transform.Translation(-plane.ZAxis * depth);

        if (topLeft != null)
        {
            foreach (var c in topLeft)
            {
                var bottomCurve = c.DuplicateCurve();
                bottomCurve.Transform(depthTransform);
                result.Add(bottomCurve);
            }
        }

        if (topRight != null)
        {
            foreach (var c in topRight)
            {
                var bottomCurve = c.DuplicateCurve();
                bottomCurve.Transform(depthTransform);
                result.Add(bottomCurve);
            }
        }

        // Vertical connection lines at intervals along the source curve
        var curveLength = sourceCurve.GetLength();
        var numConnections = Math.Max(4, (int)(curveLength / 50.0));

        for (int i = 0; i <= numConnections; i++)
        {
            var t = (double)i / numConnections;
            sourceCurve.NormalizedLengthParameter(t, out var param);
            var topPoint = sourceCurve.PointAt(param);

            // Left side connection
            var topLeftPt = topPoint + plane.XAxis * 0 + GetPerpendicularOffset(sourceCurve, param, radius);
            var bottomLeftPt = topLeftPt - plane.ZAxis * depth;
            result.Add(new LineCurve(topLeftPt, bottomLeftPt));

            // Right side connection
            var topRightPt = topPoint - GetPerpendicularOffset(sourceCurve, param, radius);
            var bottomRightPt = topRightPt - plane.ZAxis * depth;
            result.Add(new LineCurve(topRightPt, bottomRightPt));
        }

        // Direction arrows on top curve
        result.AddRange(CreateDirectionArrows(sourceCurve));

        return result;
    }

    /// <summary>
    /// Creates 3D toolpath visualization for a drill at actual cutting depth.
    /// Shows a cylinder outline (two circles + vertical lines) and cone tip.
    /// </summary>
    public static List<GeometryBase> CreateDrillToolpath3D(Point3d center, double diameter, double depth)
    {
        var result = new List<GeometryBase>();
        if (!center.IsValid) return result;
        if (depth <= 0.001) return CreateDrillToolpath(center, diameter);

        var radius = diameter / 2.0;
        if (radius <= 0.01) return result;

        // Top circle
        var topCircle = new ArcCurve(new Circle(center, radius));
        result.Add(topCircle);

        // Bottom circle at depth
        var bottomCenter = new Point3d(center.X, center.Y, center.Z - depth);
        var bottomCircle = new ArcCurve(new Circle(bottomCenter, radius));
        result.Add(bottomCircle);

        // Vertical lines connecting top and bottom circles (4 lines at cardinal directions)
        for (int i = 0; i < 4; i++)
        {
            var angle = i * Math.PI / 2.0;
            var dx = radius * Math.Cos(angle);
            var dy = radius * Math.Sin(angle);
            var topPt = new Point3d(center.X + dx, center.Y + dy, center.Z);
            var bottomPt = new Point3d(center.X + dx, center.Y + dy, center.Z - depth);
            result.Add(new LineCurve(topPt, bottomPt));
        }

        // Crosshair at top
        var crossSize = radius * 0.6;
        result.Add(new LineCurve(
            new Point3d(center.X - crossSize, center.Y, center.Z),
            new Point3d(center.X + crossSize, center.Y, center.Z)));
        result.Add(new LineCurve(
            new Point3d(center.X, center.Y - crossSize, center.Z),
            new Point3d(center.X, center.Y + crossSize, center.Z)));

        // Drill point indicator at bottom (X lines)
        var tipSize = radius * 0.4;
        result.Add(new LineCurve(
            new Point3d(center.X - tipSize, center.Y - tipSize, center.Z - depth),
            new Point3d(center.X + tipSize, center.Y + tipSize, center.Z - depth)));
        result.Add(new LineCurve(
            new Point3d(center.X - tipSize, center.Y + tipSize, center.Z - depth),
            new Point3d(center.X + tipSize, center.Y - tipSize, center.Z - depth)));

        return result;
    }

    /// <summary>
    /// Creates 3D toolpath visualization for a pocket at actual cutting depth.
    /// Shows concentric offset curves at depth with connecting ramp indicators.
    /// </summary>
    public static List<GeometryBase> CreatePocketToolpath3D(Curve boundaryCurve, double toolDiameter, double stepoverPercent, double depth)
    {
        var result = new List<GeometryBase>();
        if (boundaryCurve == null) return result;
        if (depth <= 0.001) return CreatePocketToolpath(boundaryCurve, toolDiameter, stepoverPercent);

        var radius = toolDiameter / 2.0;
        var stepover = toolDiameter * (stepoverPercent / 100.0);
        if (radius <= 0.01 || stepover <= 0.01) return result;

        var doc = RhinoDoc.ActiveDoc;
        if (doc == null) return result;
        var tolerance = doc.ModelAbsoluteTolerance;
        var plane = GetCurvePlane(boundaryCurve);
        var depthTransform = Transform.Translation(-plane.ZAxis * depth);

        // Top boundary (at surface)
        result.Add(boundaryCurve.DuplicateCurve());

        // Bottom boundary (at depth)
        var bottomBoundary = boundaryCurve.DuplicateCurve();
        bottomBoundary.Transform(depthTransform);
        result.Add(bottomBoundary);

        // Vertical connection lines on boundary
        var boundaryLength = boundaryCurve.GetLength();
        var numConnections = Math.Max(4, (int)(boundaryLength / 60.0));
        for (int i = 0; i < numConnections; i++)
        {
            var t = (double)i / numConnections;
            boundaryCurve.NormalizedLengthParameter(t, out var param);
            var topPt = boundaryCurve.PointAt(param);
            var bottomPt = topPt - plane.ZAxis * depth;
            result.Add(new LineCurve(topPt, bottomPt));
        }

        // Concentric offsets at depth level
        var currentOffset = radius;
        var maxIterations = 200;
        var iteration = 0;

        while (iteration < maxIterations)
        {
            var offsets = boundaryCurve.Offset(plane, -currentOffset, tolerance, CurveOffsetCornerStyle.Sharp);
            if (offsets == null || offsets.Length == 0) break;

            var addedAny = false;
            foreach (var offset in offsets)
            {
                if (offset.GetLength() > toolDiameter * 0.5)
                {
                    // Add at depth level
                    var depthOffset = offset.DuplicateCurve();
                    depthOffset.Transform(depthTransform);
                    result.Add(depthOffset);
                    addedAny = true;
                }
            }

            if (!addedAny) break;
            currentOffset += stepover;
            iteration++;
        }

        // Entry point marker at surface + ramp indicator to depth
        var entryPoint = boundaryCurve.PointAtStart;
        var entryCircle = new ArcCurve(new Circle(entryPoint, toolDiameter * 0.3));
        result.Add(entryCircle);

        // Ramp line from entry to depth
        var entryBottom = entryPoint - plane.ZAxis * depth;
        // Offset ramp slightly for visibility
        var rampEnd = entryBottom + plane.XAxis * (toolDiameter * 2.0);
        result.Add(new LineCurve(entryPoint, rampEnd));

        return result;
    }

    /// <summary>
    /// Adds 3D toolpath geometry to the document on the CNC_Toolpaths_3D layer tree.
    /// </summary>
    public static void AddToolpath3DToDocument(
        RhinoDoc doc,
        RhinoObject sourceObject,
        string operationType,
        List<GeometryBase> toolpathGeometry)
    {
        if (doc == null || sourceObject == null || toolpathGeometry == null || toolpathGeometry.Count == 0) return;

        var layerIndex = EnsureToolpath3DSubLayer(doc, operationType);
        var color = GetOperationColor(operationType);
        // Slightly lighter color for 3D to distinguish from 2D
        var color3D = System.Drawing.Color.FromArgb(
            Math.Min(255, color.R + 40),
            Math.Min(255, color.G + 40),
            Math.Min(255, color.B + 40));

        var objectIds = new List<Guid>();

        foreach (var geom in toolpathGeometry)
        {
            var attributes = new ObjectAttributes
            {
                LayerIndex = layerIndex,
                ColorSource = ObjectColorSource.ColorFromObject,
                ObjectColor = color3D,
                PlotWeight = PlotWeight,
                PlotWeightSource = ObjectPlotWeightSource.PlotWeightFromObject,
            };

            var id = Guid.Empty;
            if (geom is Curve curve)
                id = doc.Objects.AddCurve(curve, attributes);
            else if (geom is Rhino.Geometry.Point point)
                id = doc.Objects.AddPoint(point.Location, attributes);

            if (id != Guid.Empty)
                objectIds.Add(id);
        }

        // Store 3D group info on source object
        if (objectIds.Count > 0)
        {
            var groupIndex = doc.Groups.Add(objectIds);
            sourceObject.Attributes.SetUserString("CNC_GroupIndex3D", groupIndex.ToString());
            sourceObject.CommitChanges();
        }
    }

    /// <summary>
    /// Removes all 3D toolpath geometry grouped with the source object.
    /// </summary>
    public static void RemoveToolpath3DGeometry(RhinoDoc doc, RhinoObject sourceObject)
    {
        if (doc == null || sourceObject == null) return;
        var groupIndexStr = sourceObject.Attributes.GetUserString("CNC_GroupIndex3D");
        if (string.IsNullOrEmpty(groupIndexStr) || !int.TryParse(groupIndexStr, out var groupIndex))
            return;

        var groupMembers = doc.Objects.FindByGroup(groupIndex);
        if (groupMembers == null) return;

        foreach (var member in groupMembers)
        {
            var layer = doc.Layers[member.Attributes.LayerIndex];
            if (layer != null && layer.FullPath.StartsWith(Root3DLayerName, StringComparison.OrdinalIgnoreCase))
            {
                doc.Objects.Delete(member.Id, true);
            }
        }

        doc.Groups.Delete(groupIndex);
        sourceObject.Attributes.DeleteUserString("CNC_GroupIndex3D");
        sourceObject.CommitChanges();
    }

    /// <summary>
    /// Ensures the CNC_Toolpaths_3D root layer and sublayer exist.
    /// </summary>
    private static int EnsureToolpath3DSubLayer(RhinoDoc doc, string operationType)
    {
        var subLayerName = operationType switch
        {
            CncOperationSchema.TYPE_CONTOUR => ContourSubLayer,
            CncOperationSchema.TYPE_POCKET => PocketSubLayer,
            CncOperationSchema.TYPE_DRILL => DrillSubLayer,
            CncOperationSchema.TYPE_GROOVE => GrooveSubLayer,
            _ => operationType
        };

        var color = GetOperationColor(operationType);

        var rootLayer = doc.Layers.FindByFullPath(Root3DLayerName, -1);
        if (rootLayer < 0)
        {
            var layer = new Layer
            {
                Name = Root3DLayerName,
                Color = Color.Gray
            };
            rootLayer = doc.Layers.Add(layer);
        }

        var fullPath = $"{Root3DLayerName}::{subLayerName}";
        var subLayerIndex = doc.Layers.FindByFullPath(fullPath, -1);
        if (subLayerIndex < 0)
        {
            var subLayer = new Layer
            {
                Name = subLayerName,
                ParentLayerId = doc.Layers[rootLayer].Id,
                Color = color
            };
            subLayerIndex = doc.Layers.Add(subLayer);
        }

        return subLayerIndex;
    }

    /// <summary>
    /// Helper to get perpendicular offset point at a curve parameter.
    /// </summary>
    private static Vector3d GetPerpendicularOffset(Curve curve, double param, double distance)
    {
        var tangent = curve.TangentAt(param);
        if (tangent.IsZero) return Vector3d.Zero;
        tangent.Unitize();
        var perp = new Vector3d(-tangent.Y, tangent.X, 0);
        return perp * distance;
    }

    /// <summary>
    /// Removes all toolpath geometry grouped with the source object.
    /// </summary>
    public static void RemoveToolpathGeometry(RhinoDoc doc, RhinoObject sourceObject)
    {
        if (doc == null || sourceObject == null) return;
        var groupIndexStr = sourceObject.Attributes.GetUserString(CncOperationSchema.CNC_GROUP_INDEX);
        if (string.IsNullOrEmpty(groupIndexStr) || !int.TryParse(groupIndexStr, out var groupIndex))
            return;

        // Find all objects in the group
        var groupMembers = doc.Objects.FindByGroup(groupIndex);
        if (groupMembers == null) return;

        // Delete toolpath objects (not the source itself)
        foreach (var member in groupMembers)
        {
            if (member.Id != sourceObject.Id)
            {
                // Only delete objects on the CNC_Toolpaths layer tree
                var layer = doc.Layers[member.Attributes.LayerIndex];
                if (layer != null && layer.FullPath.StartsWith(RootLayerName, StringComparison.OrdinalIgnoreCase))
                {
                    doc.Objects.Delete(member.Id, true);
                }
            }
        }

        // Remove the group itself
        doc.Groups.Delete(groupIndex);

        // Clear the stored group index
        sourceObject.Attributes.DeleteUserString(CncOperationSchema.CNC_GROUP_INDEX);
        sourceObject.CommitChanges();
    }

    /// <summary>
    /// Gets the best plane for offsetting a curve. Uses the curve's own plane
    /// when planar, otherwise constructs a plane at the curve's midpoint using
    /// the tangent and world Z. Falls back to WorldXY moved to the curve's Z.
    /// </summary>
    private static Plane GetCurvePlane(Curve curve)
    {
        // If the curve is planar, use its own plane — handles Z offset automatically
        var tolerance = RhinoDoc.ActiveDoc?.ModelAbsoluteTolerance ?? 0.001;
        if (curve.TryGetPlane(out var curvePlane, tolerance))
            return curvePlane;

        // For non-planar curves, use WorldXY at the curve's start Z
        var startPoint = curve.PointAtStart;
        var plane = Plane.WorldXY;
        plane.Origin = new Point3d(0, 0, startPoint.Z);
        return plane;
    }

    private static bool TryResolveDrillPreview(GeometryBase geometry, double diameter, out Point3d center, out bool includeOutline)
    {
        center = Point3d.Unset;
        includeOutline = true;

        if (geometry == null)
            return false;

        switch (geometry)
        {
            case Rhino.Geometry.Point point:
                center = point.Location;
                return center.IsValid;

            case Curve curve:
            {
                var tolerance = Math.Max(RhinoDoc.ActiveDoc?.ModelAbsoluteTolerance ?? 0.01, 0.01);
                if (curve.TryGetCircle(out var circle, tolerance))
                {
                    center = circle.Center;
                    includeOutline = Math.Abs((circle.Radius * 2.0) - diameter) > Math.Max(tolerance * 2.0, 0.1);
                    return center.IsValid;
                }

                center = curve.PointAtStart;
                return center.IsValid;
            }

            default:
            {
                var bbox = geometry.GetBoundingBox(true);
                if (!bbox.IsValid)
                    return false;

                center = bbox.Center;
                return center.IsValid;
            }
        }
    }

    /// <summary>
    /// Creates small triangle/chevron arrows along a curve to indicate feed direction.
    /// </summary>
    private static List<GeometryBase> CreateDirectionArrows(Curve curve)
    {
        var arrows = new List<GeometryBase>();
        var length = curve.GetLength();
        if (length <= 0) return arrows;

        // Place arrows every ~ArrowSpacing mm, at least 1
        var numArrows = Math.Max(1, (int)(length / ArrowSpacing));
        var spacing = 1.0 / (numArrows + 1);

        for (int i = 1; i <= numArrows; i++)
        {
            var t = spacing * i;
            curve.NormalizedLengthParameter(t, out var param);
            var point = curve.PointAt(param);
            var tangent = curve.TangentAt(param);

            if (tangent.IsZero) continue;

            tangent.Unitize();

            // Create a small triangle (chevron) pointing in the feed direction
            var perpendicular = new Vector3d(-tangent.Y, tangent.X, 0);

            var tip = point + tangent * ArrowSize;
            var left = point - tangent * (ArrowSize * 0.3) + perpendicular * (ArrowSize * 0.5);
            var right = point - tangent * (ArrowSize * 0.3) - perpendicular * (ArrowSize * 0.5);

            // Two lines forming the chevron
            arrows.Add(new LineCurve(left, tip));
            arrows.Add(new LineCurve(right, tip));
        }

        return arrows;
    }

    /// <summary>
    /// Ensures the CNC_Toolpaths root layer and the operation-type sublayer exist.
    /// Returns the sublayer index.
    /// </summary>
    private static int EnsureToolpathSubLayer(RhinoDoc doc, string operationType)
    {
        var subLayerName = operationType switch
        {
            CncOperationSchema.TYPE_CONTOUR => ContourSubLayer,
            CncOperationSchema.TYPE_POCKET => PocketSubLayer,
            CncOperationSchema.TYPE_DRILL => DrillSubLayer,
            CncOperationSchema.TYPE_GROOVE => GrooveSubLayer,
            _ => operationType
        };

        var color = GetOperationColor(operationType);

        // Find or create root layer
        var rootLayer = doc.Layers.FindByFullPath(RootLayerName, -1);
        if (rootLayer < 0)
        {
            var layer = new Layer
            {
                Name = RootLayerName,
                Color = Color.Gray
            };
            rootLayer = doc.Layers.Add(layer);
        }

        // Find or create sublayer
        var fullPath = $"{RootLayerName}::{subLayerName}";
        var subLayerIndex = doc.Layers.FindByFullPath(fullPath, -1);
        if (subLayerIndex < 0)
        {
            var subLayer = new Layer
            {
                Name = subLayerName,
                ParentLayerId = doc.Layers[rootLayer].Id,
                Color = color
            };
            subLayerIndex = doc.Layers.Add(subLayer);
        }

        return subLayerIndex;
    }
}
