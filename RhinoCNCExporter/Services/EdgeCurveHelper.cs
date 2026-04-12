using System.Drawing;
using System.Globalization;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using RhinoCNCExporter.Core.Blocks;

namespace RhinoCNCExporter.Services;

/// <summary>
/// Helper to extract Brep edges as standalone curves on a dedicated layer.
/// When a user selects a Brep edge for a CNC operation, we duplicate the edge
/// as an independent curve so that UserText is stored on the curve (not the
/// parent Brep). This allows multiple operations on different edges of the
/// same Brep without overwriting each other.
/// </summary>
public static class EdgeCurveHelper
{
    /// <summary>Layer name for extracted edge curves.</summary>
    public const string EdgeCurveLayerName = "CNC_EdgeCurves";

    /// <summary>
    /// Checks whether the given ObjRef refers to a Brep sub-object (edge).
    /// </summary>
    public static bool IsBrepEdge(Rhino.DocObjects.ObjRef objRef)
    {
        if (objRef == null) return false;

        if (objRef.Edge() != null)
            return true;

        var ci = objRef.GeometryComponentIndex;
        if (ci.ComponentIndexType == ComponentIndexType.BrepEdge)
            return true;

        var parentGeometry = objRef.Object()?.Geometry;
        return objRef.Curve() != null && parentGeometry is Brep;
    }

    /// <summary>
    /// Extracts an edge curve from a Brep and adds it to the document as a
    /// standalone curve on the <see cref="EdgeCurveLayerName"/> layer.
    /// Returns the new RhinoObject for the extracted curve, or null on failure.
    /// </summary>
    /// <param name="doc">Active Rhino document.</param>
    /// <param name="objRef">The ObjRef pointing at a Brep edge.</param>
    /// <param name="operationColor">Color to apply to the extracted curve (operation-specific).</param>
    /// <returns>The new RhinoObject for the extracted curve, or null on failure.</returns>
    public static RhinoObject? ExtractEdgeCurve(RhinoDoc doc, Rhino.DocObjects.ObjRef objRef, Color operationColor)
    {
        var parentObj = objRef.Object();
        if (parentObj == null) return null;

        var edge = objRef.Edge();
        var edgeIndex = objRef.GeometryComponentIndex.Index;
        Curve? duplicatedCurve = edge?.DuplicateCurve();

        if (duplicatedCurve == null)
        {
            duplicatedCurve = objRef.Curve()?.DuplicateCurve();
            if (duplicatedCurve == null) return null;
        }

        if (edgeIndex < 0)
        {
            edgeIndex = TryFindMatchingEdgeIndex(parentObj.Geometry as Brep, duplicatedCurve, doc.ModelAbsoluteTolerance);
        }

        if (edgeIndex < 0) return null;

        // Ensure the target layer exists
        var layerIndex = EnsureEdgeCurveLayer(doc);

        // Build attributes for the new curve
        var attributes = new ObjectAttributes
        {
            LayerIndex = layerIndex,
            ColorSource = ObjectColorSource.ColorFromObject,
            ObjectColor = operationColor,
            PlotWeight = 0.35,
            PlotWeightSource = ObjectPlotWeightSource.PlotWeightFromObject,
            Name = $"Edge {edgeIndex} of {parentObj.Name ?? parentObj.Id.ToString()[..8]}"
        };

        // Store reference back to source Brep
        attributes.SetUserString(CncOperationSchema.CNC_SOURCE_BREP, parentObj.Id.ToString());
        attributes.SetUserString(CncOperationSchema.CNC_SOURCE_EDGE_INDEX, edgeIndex.ToString(CultureInfo.InvariantCulture));

        var curveId = doc.Objects.AddCurve(duplicatedCurve, attributes);
        if (curveId == Guid.Empty) return null;

        return doc.Objects.FindId(curveId);
    }

    /// <summary>
    /// Resolves a selection into the object that should carry the operation.
    /// Brep edges are extracted as standalone proxy curves. Standalone curves are used as-is.
    /// </summary>
    public static bool TryResolveCurveTarget(RhinoDoc doc, Rhino.DocObjects.ObjRef objRef, Color operationColor, out RhinoObject? targetObj, out Curve? curve)
    {
        targetObj = null;
        curve = null;

        if (IsBrepEdge(objRef))
        {
            var extracted = ExtractEdgeCurve(doc, objRef, operationColor);
            if (extracted == null)
                return false;

            targetObj = extracted;
            curve = extracted.Geometry as Curve;
            return curve != null;
        }

        var rhinoObj = objRef.Object();
        if (rhinoObj == null)
            return false;

        curve = objRef.Curve() ?? rhinoObj.Geometry as Curve;
        if (curve == null)
            return false;

        targetObj = rhinoObj;
        return true;
    }

    /// <summary>
    /// Ensures the CNC_EdgeCurves layer exists, creating it if necessary.
    /// The layer uses a dashed linetype when available.
    /// </summary>
    private static int EnsureEdgeCurveLayer(RhinoDoc doc)
    {
        var layerIndex = doc.Layers.FindByFullPath(EdgeCurveLayerName, -1);
        if (layerIndex >= 0) return layerIndex;

        var layer = new Layer
        {
            Name = EdgeCurveLayerName,
            Color = Color.FromArgb(180, 180, 180),
            IsVisible = true
        };

        // Try to assign a dashed linetype for visual distinction
        var dashedIndex = doc.Linetypes.FindName("Dashed")?.Index ?? -1;
        if (dashedIndex >= 0)
        {
            layer.LinetypeIndex = dashedIndex;
        }

        return doc.Layers.Add(layer);
    }

    /// <summary>
    /// Removes an extracted edge curve and its toolpath geometry from the document.
    /// Call this when removing a CNC operation that was applied to a Brep edge.
    /// </summary>
    public static void RemoveExtractedEdgeCurve(RhinoDoc doc, RhinoObject edgeCurveObj)
    {
        if (edgeCurveObj == null) return;

        // First remove toolpath geometry (if any)
        ToolpathVisualizer.RemoveToolpathGeometry(doc, edgeCurveObj);

        // Then delete the extracted curve itself
        doc.Objects.Delete(edgeCurveObj.Id, true);
    }

    /// <summary>
    /// Returns true if the given object is an extracted edge curve (has CNC_SourceBrep key).
    /// </summary>
    public static bool IsExtractedEdgeCurve(RhinoObject obj)
    {
        if (obj == null) return false;
        return !string.IsNullOrEmpty(obj.Attributes.GetUserString(CncOperationSchema.CNC_SOURCE_BREP));
    }

    private static int TryFindMatchingEdgeIndex(Brep? brep, Curve curve, double tolerance)
    {
        if (brep == null) return -1;

        var sample = curve.PointAtNormalizedLength(0.5);
        if (!sample.IsValid)
            sample = curve.PointAtStart;

        foreach (var brepEdge in brep.Edges)
        {
            if (brepEdge == null) continue;

            var edgeCurve = brepEdge.EdgeCurve;
            if (edgeCurve == null) continue;

            if (Math.Abs(edgeCurve.GetLength() - curve.GetLength()) > Math.Max(tolerance, 0.1))
                continue;

            if (!edgeCurve.ClosestPoint(sample, out var t))
                continue;

            var closest = edgeCurve.PointAt(t);
            if (closest.DistanceTo(sample) <= Math.Max(tolerance * 2.0, 0.1))
                return brepEdge.EdgeIndex;
        }

        return -1;
    }
}
