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
        var radius = toolDiameter / 2.0;

        if (radius <= 0.01) return result;

        // Determine the best plane for offsetting — use the curve's own plane
        // instead of WorldXY to handle curves at any Z height or orientation
        var plane = GetCurvePlane(sourceCurve);
        var tolerance = RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;

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
        var radius = toolDiameter / 2.0;
        var stepover = toolDiameter * (stepoverPercent / 100.0);

        if (radius <= 0.01 || stepover <= 0.01) return result;

        var tolerance = RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;
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
    public static List<GeometryBase> CreateDrillToolpath(Point3d center, double diameter)
    {
        var result = new List<GeometryBase>();
        var radius = diameter / 2.0;

        if (radius <= 0.01) return result;

        // Circle showing hole diameter
        var circle = new ArcCurve(new Circle(center, radius));
        result.Add(circle);

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
    /// Adds toolpath geometry to the document, groups it with the source object,
    /// and stores the group index as UserText.
    /// </summary>
    public static void AddToolpathToDocument(
        RhinoDoc doc,
        RhinoObject sourceObject,
        string operationType,
        List<GeometryBase> toolpathGeometry)
    {
        if (toolpathGeometry.Count == 0) return;

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

    /// <summary>
    /// Removes all toolpath geometry grouped with the source object.
    /// </summary>
    public static void RemoveToolpathGeometry(RhinoDoc doc, RhinoObject sourceObject)
    {
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
        if (curve.TryGetPlane(out var curvePlane, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance))
            return curvePlane;

        // For non-planar curves, use WorldXY at the curve's start Z
        var startPoint = curve.PointAtStart;
        var plane = Plane.WorldXY;
        plane.Origin = new Point3d(0, 0, startPoint.Z);
        return plane;
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
