using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using RhinoCNCExporter.Core.Blocks;
using RhinoCNCExporter.Core.Models;
using RhinoCNCExporter.Core.Emitters;

namespace RhinoCNCExporter.Services;

/// <summary>
/// Reads CNC operations from object UserText and converts them to Machining objects.
/// Plugin-specific service with RhinoCommon dependencies.
/// </summary>
public class UserTextMachiningReader
{
    /// <summary>
    /// Gets all UserText-based machinings from objects on the specified layer.
    /// </summary>
    public IReadOnlyList<Machining> GetMachiningsByLayer(RhinoDoc doc, string layerName)
    {
        var machinings = new List<Machining>();
        
        // Find layer index
        var layerIndex = doc.Layers.FindName(layerName);
        if (layerIndex < 0)
            return machinings;

        // Get all objects on this layer with CNC operations
        var objectsWithOperations = doc.Objects
            .Where(obj => obj.Attributes.LayerIndex == layerIndex)
            .Where(obj => !string.IsNullOrEmpty(obj.Attributes.GetUserString(CncOperationSchema.CNC_TYPE)))
            .ToList();

        foreach (var obj in objectsWithOperations)
        {
            var machining = ConvertToMachining(obj);
            if (machining != null)
            {
                machinings.Add(machining);
            }
        }

        return machinings;
    }

    /// <summary>
    /// Gets all UserText-based machinings from the entire document.
    /// </summary>
    public IReadOnlyList<Machining> GetAllMachinings(RhinoDoc doc)
    {
        var machinings = new List<Machining>();

        var objectsWithOperations = CncOperationService.GetAllOperationsInDocument(doc);

        foreach (var obj in objectsWithOperations)
        {
            var machining = ConvertToMachining(obj);
            if (machining != null)
            {
                machinings.Add(machining);
            }
        }

        return machinings;
    }

    /// <summary>
    /// Converts a RhinoObject with UserText to a Machining object.
    /// </summary>
    private Machining? ConvertToMachining(RhinoObject obj)
    {
        var operation = CncOperationService.GetOperation(obj);
        if (operation == null)
            return null;

        var layerName = RhinoDoc.ActiveDoc.Layers[obj.Attributes.LayerIndex]?.Name ?? "Unknown";
        var objectName = $"{layerName}_{obj.Id.ToString()[..8]}"; // Use layer + short ID as name

        try
        {
            return operation.Type.ToUpperInvariant() switch
            {
                CncOperationSchema.TYPE_CONTOUR => CreateContourMachining(obj, operation, objectName),
                CncOperationSchema.TYPE_POCKET => CreatePocketMachining(obj, operation, objectName),
                CncOperationSchema.TYPE_DRILL => CreateDrillMachining(obj, operation, objectName),
                CncOperationSchema.TYPE_GROOVE => CreateGrooveMachining(obj, operation, objectName),
                _ => null
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[UserTextMachiningReader] Error converting {operation.Type}: {ex.Message}");
            return null;
        }
    }

    private Machining? CreateContourMachining(RhinoObject obj, MachiningOperation operation, string name)
    {
        if (obj.Geometry is not Curve curve)
            return null;

        var points = ExtractCurvePoints(curve);
        if (points.Count == 0)
            return null;

        var toolDiameter = GetToolDiameter(operation.Tool);
        var depth = operation.Depth ?? 10.0;

        return new RoutingMachining
        {
            Name = name,
            Points = points,
            Depth = depth,
            ToolDiameter = toolDiameter,
            IsClosed = curve.IsClosed,
            Source = MachiningSource.Manual,
            TechCode = DetermineRouterTechCode(operation)
        };
    }

    private Machining? CreatePocketMachining(RhinoObject obj, MachiningOperation operation, string name)
    {
        if (obj.Geometry is not Curve curve || !curve.IsClosed)
            return null;

        var points = ExtractCurvePoints(curve);
        if (points.Count == 0)
            return null;

        var toolDiameter = GetToolDiameter(operation.Tool);
        var depth = operation.Depth ?? 10.0;

        return new PocketMachining
        {
            Name = name,
            Loops = new[] { points }, // Single boundary loop
            Depth = depth,
            ToolDiameter = toolDiameter,
            Source = MachiningSource.Manual,
            TechCode = DetermineRouterTechCode(operation)
        };
    }

    private Machining? CreateDrillMachining(RhinoObject obj, MachiningOperation operation, string name)
    {
        var center = GetObjectCenter(obj);
        if (center == null)
            return null;

        var diameter = operation.Diameter ?? 5.0;
        var depth = operation.Depth ?? 10.0;

        return new DrillMachining
        {
            Name = name,
            X = center.Value.X,
            Y = center.Value.Y,
            Depth = depth,
            Diameter = diameter,
            Source = MachiningSource.Manual,
            TechCode = DetermineDrillTechCode(diameter)
        };
    }

    private Machining? CreateGrooveMachining(RhinoObject obj, MachiningOperation operation, string name)
    {
        if (obj.Geometry is not Curve curve)
            return null;

        var points = ExtractCurvePoints(curve);
        if (points.Count == 0)
            return null;

        var width = operation.Width ?? 5.0;
        var depth = operation.Depth ?? 8.0;
        var toolDiameter = GetToolDiameter(operation.Tool);

        return new RoutingMachining
        {
            Name = name,
            Points = points,
            Depth = depth,
            ToolDiameter = toolDiameter,
            IsClosed = false, // Grooves are typically open paths
            Source = MachiningSource.Manual,
            TechCode = DetermineRouterTechCode(operation)
        };
    }

    private IReadOnlyList<(double X, double Y)> ExtractCurvePoints(Curve curve)
    {
        var points = new List<(double X, double Y)>();

        // Sample curve to polyline points
        var polyline = curve.ToPolyline(0, 0, 0.5, 0.1, 0, 0, 0, 0, true);
        if (polyline != null)
        {
            for (int i = 0; i < polyline.Count; i++)
            {
                var pt = polyline[i];
                points.Add((pt.X, pt.Y));
            }
        }
        else
        {
            // Fallback: use domain sampling
            var domain = curve.Domain;
            var count = Math.Max(10, (int)(curve.GetLength() / 2.0)); // ~2mm segments
            
            for (int i = 0; i <= count; i++)
            {
                var t = domain.ParameterAt((double)i / count);
                var pt = curve.PointAt(t);
                points.Add((pt.X, pt.Y));
            }
        }

        return points;
    }

    private Point3d? GetObjectCenter(RhinoObject obj)
    {
        var bbox = obj.Geometry.GetBoundingBox(true);
        return bbox.IsValid ? bbox.Center : null;
    }

    private double GetToolDiameter(string? toolName)
    {
        if (string.IsNullOrEmpty(toolName))
            return 6.0; // Default tool diameter

        // Try to extract diameter from tool name (e.g., "Fräser 8mm" -> 8.0)
        var match = System.Text.RegularExpressions.Regex.Match(toolName, @"(\d+(?:\.\d+)?)\s*mm");
        if (match.Success && double.TryParse(match.Groups[1].Value, out var diameter))
        {
            return diameter;
        }

        return 6.0; // Fallback
    }

    private string DetermineRouterTechCode(MachiningOperation operation)
    {
        // Use E010 for general routing, could be enhanced with tool-specific logic
        return "E010";
    }

    private string DetermineDrillTechCode(double diameter)
    {
        // Simple heuristic for tech codes based on drill diameter
        return diameter <= 5.0 ? "E013" : "E009";
    }
}