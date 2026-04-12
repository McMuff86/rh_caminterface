using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using RhinoCNCExporter.Core.Blocks;
using RhinoCNCExporter.Core.Emitters;
using RhinoCNCExporter.Core.Models;
using RhinoCNCExporter.Core.PlateDetection;

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

        var layer = doc.Layers.FindName(layerName);
        if (layer == null)
            return machinings;

        var layerIndex = layer.Index;

        var objectsWithOperations = doc.Objects
            .Where(obj => obj.Attributes.LayerIndex == layerIndex)
            .Where(obj => !string.IsNullOrEmpty(obj.Attributes.GetUserString(CncOperationSchema.CNC_TYPE)))
            .ToList();

        foreach (var obj in objectsWithOperations)
        {
            var machining = ConvertToMachining(doc, obj, null);
            if (machining != null)
                machinings.Add(machining);
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
            var machining = ConvertToMachining(doc, obj, null);
            if (machining != null)
                machinings.Add(machining);
        }

        return machinings;
    }

    /// <summary>
    /// Gets all UserText-based machinings that belong to the given plate.
    /// Coordinates are transformed into plate-local space.
    /// </summary>
    public IReadOnlyList<Machining> GetMachiningsForPlate(RhinoDoc doc, Plate plate)
    {
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentNullException.ThrowIfNull(plate);

        var machinings = new List<Machining>();
        var candidateLayerIndexes = new HashSet<int>();

        if (!string.IsNullOrWhiteSpace(plate.LayerPath))
        {
            var fullPathIndex = doc.Layers.FindByFullPath(plate.LayerPath, -1);
            if (fullPathIndex >= 0)
                candidateLayerIndexes.Add(fullPathIndex);

            var leafName = plate.LayerPath.Split(new[] { "::" }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
            if (!string.IsNullOrWhiteSpace(leafName))
            {
                var leafLayer = doc.Layers.FindName(leafName);
                if (leafLayer != null)
                    candidateLayerIndexes.Add(leafLayer.Index);
            }
        }

        if (!string.IsNullOrWhiteSpace(plate.Name))
        {
            var namedLayer = doc.Layers.FindName(plate.Name);
            if (namedLayer != null)
                candidateLayerIndexes.Add(namedLayer.Index);
        }

        if (candidateLayerIndexes.Count == 0)
            return machinings;

        var objectsWithOperations = doc.Objects
            .Where(obj => !string.IsNullOrEmpty(obj.Attributes.GetUserString(CncOperationSchema.CNC_TYPE)))
            .Where(obj => candidateLayerIndexes.Contains(obj.Attributes.LayerIndex))
            .ToList();

        foreach (var obj in objectsWithOperations)
        {
            var machining = ConvertToMachining(doc, obj, plate);
            if (machining != null)
                machinings.Add(machining);
        }

        return machinings;
    }

    /// <summary>
    /// Converts a RhinoObject with UserText to a Machining object.
    /// </summary>
    private Machining? ConvertToMachining(RhinoDoc doc, RhinoObject obj, Plate? plate)
    {
        var operation = CncOperationService.GetOperation(obj);
        if (operation == null)
            return null;

        var layerName = doc.Layers[obj.Attributes.LayerIndex]?.Name ?? "Unknown";
        var objectName = !string.IsNullOrWhiteSpace(obj.Name)
            ? obj.Name
            : $"{layerName}_{obj.Id.ToString()[..8]}";

        try
        {
            return operation.Type.ToUpperInvariant() switch
            {
                CncOperationSchema.TYPE_CONTOUR => CreateContourMachining(obj, operation, objectName, plate),
                CncOperationSchema.TYPE_POCKET => CreatePocketMachining(obj, operation, objectName, plate),
                CncOperationSchema.TYPE_DRILL => CreateDrillMachining(obj, operation, objectName, plate),
                CncOperationSchema.TYPE_GROOVE => CreateGrooveMachining(obj, operation, objectName, plate),
                _ => null
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[UserTextMachiningReader] Error converting {operation.Type}: {ex.Message}");
            return null;
        }
    }

    private Machining? CreateContourMachining(RhinoObject obj, MachiningOperation operation, string name, Plate? plate)
    {
        if (obj.Geometry is not Curve curve)
            return null;

        var points = ExtractCurvePoints(curve, plate);
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

    private Machining? CreatePocketMachining(RhinoObject obj, MachiningOperation operation, string name, Plate? plate)
    {
        if (obj.Geometry is not Curve curve || !curve.IsClosed)
            return null;

        var points = ExtractCurvePoints(curve, plate);
        if (points.Count == 0)
            return null;

        var toolDiameter = GetToolDiameter(operation.Tool);
        var depth = operation.Depth ?? 10.0;

        return new PocketMachining
        {
            Name = name,
            Loops = new[] { points },
            Depth = depth,
            ToolDiameter = toolDiameter,
            Source = MachiningSource.Manual,
            TechCode = DetermineRouterTechCode(operation)
        };
    }

    private Machining? CreateDrillMachining(RhinoObject obj, MachiningOperation operation, string name, Plate? plate)
    {
        var center = GetObjectCenter(obj, plate);
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

    private Machining? CreateGrooveMachining(RhinoObject obj, MachiningOperation operation, string name, Plate? plate)
    {
        if (obj.Geometry is not Curve curve)
            return null;

        var points = ExtractCurvePoints(curve, plate);
        if (points.Count == 0)
            return null;

        var depth = operation.Depth ?? 8.0;
        var toolDiameter = GetToolDiameter(operation.Tool);

        return new RoutingMachining
        {
            Name = name,
            Points = points,
            Depth = depth,
            ToolDiameter = toolDiameter,
            IsClosed = false,
            Source = MachiningSource.Manual,
            TechCode = DetermineRouterTechCode(operation)
        };
    }

    private IReadOnlyList<(double X, double Y)> ExtractCurvePoints(Curve curve, Plate? plate)
    {
        var points = new List<(double X, double Y)>();

        var polyline = curve.ToPolyline(0, 0, 0.5, 0.1, 0, 0, 0, 0, true);
        if (polyline != null)
        {
            for (int i = 0; i < polyline.PointCount; i++)
            {
                var pt = polyline.Point(i);
                points.Add(ToLocalPoint(pt, plate));
            }
        }
        else
        {
            var domain = curve.Domain;
            var count = Math.Max(10, (int)(curve.GetLength() / 2.0));

            for (int i = 0; i <= count; i++)
            {
                var t = domain.ParameterAt((double)i / count);
                var pt = curve.PointAt(t);
                points.Add(ToLocalPoint(pt, plate));
            }
        }

        return points;
    }

    private Point3d? GetObjectCenter(RhinoObject obj, Plate? plate)
    {
        var bbox = obj.Geometry.GetBoundingBox(true);
        if (!bbox.IsValid)
            return null;

        var center = bbox.Center;
        if (plate == null)
            return center;

        var (x, y, _) = CoordinateTransformer.WorldToPlateLocal(plate.Origin, center.X, center.Y, center.Z);
        return new Point3d(x, y, 0);
    }

    private static (double X, double Y) ToLocalPoint(Point3d point, Plate? plate)
    {
        if (plate == null)
            return (point.X, point.Y);

        var (x, y, _) = CoordinateTransformer.WorldToPlateLocal(plate.Origin, point.X, point.Y, point.Z);
        return (x, y);
    }

    private double GetToolDiameter(string? toolName)
    {
        if (string.IsNullOrEmpty(toolName))
            return 6.0;

        var match = System.Text.RegularExpressions.Regex.Match(toolName, @"(\d+(?:\.\d+)?)\s*mm");
        if (match.Success && double.TryParse(match.Groups[1].Value, out var diameter))
            return diameter;

        return 6.0;
    }

    private string DetermineRouterTechCode(MachiningOperation operation)
    {
        return "E010";
    }

    private string DetermineDrillTechCode(double diameter)
    {
        return diameter <= 5.0 ? "E013" : "E009";
    }
}
