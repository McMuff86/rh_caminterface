using System.Drawing;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using RhinoCNCExporter.Core.Models;
using RhinoCNCExporter.Core.Pipeline;
using RhinoCNCExporter.Core.Profiles;

namespace RhinoCNCExporter.Services;

/// <summary>
/// Generates color-coded preview geometry on Rhino layers from planned toolpaths.
/// </summary>
public sealed class ToolpathPreviewService
{
    public const string RootLayerName = "RhinoCNC Preview";

    private readonly ToolLibraryStore _toolLibraryStore = new();

    public ToolpathPreviewResult GeneratePreview(
        RhinoDoc doc,
        MachineFormat format,
        DocumentExportAnalysis analysis,
        IReadOnlySet<string>? selectedPlateKeys = null,
        ToolpathPlanningOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentNullException.ThrowIfNull(analysis);

        if (!TryCreateProfile(format, out var profile, out var error))
        {
            return new ToolpathPreviewResult
            {
                Error = error ?? "Preview profile could not be created."
            };
        }

        var previews = analysis.Plates
            .Where(preview => selectedPlateKeys == null
                || selectedPlateKeys.Count == 0
                || selectedPlateKeys.Contains(BuildPlateSelectionKey(preview)))
            .ToList();

        if (previews.Count == 0)
        {
            return new ToolpathPreviewResult
            {
                Error = "Keine Platte für die Vorschau ausgewählt."
            };
        }

        var toolLibrary = _toolLibraryStore.LoadOrCreate(profile!);
        ClearPreview(doc);

        var objectCount = 0;
        var operationCount = 0;

        foreach (var preview in previews)
        {
            var plate = preview.Plate with
            {
                Machinings = ExportService3D.BuildMachiningsForPlate(preview.Plate, preview.Blocks)
            };

            var plan = ToolpathPlanner.PlanPlate(plate, toolLibrary, options);
            operationCount += plan.OperationCount;
            objectCount += DrawPlan(doc, plan);
        }

        doc.Views.Redraw();

        return new ToolpathPreviewResult
        {
            Success = true,
            PlateCount = previews.Count,
            OperationCount = operationCount,
            ObjectCount = objectCount,
            ToolLibraryPath = _toolLibraryStore.GetDefaultPath(profile!)
        };
    }

    public int ClearPreview(RhinoDoc doc)
    {
        ArgumentNullException.ThrowIfNull(doc);

        var objectIds = doc.Objects
            .Where(obj => obj != null)
            .Where(obj =>
            {
                var layer = doc.Layers[obj.Attributes.LayerIndex];
                return layer != null
                    && layer.FullPath != null
                    && layer.FullPath.StartsWith(RootLayerName, StringComparison.OrdinalIgnoreCase);
            })
            .Select(obj => obj.Id)
            .ToArray();

        foreach (var objectId in objectIds)
            doc.Objects.Delete(objectId, true);

        doc.Views.Redraw();
        return objectIds.Length;
    }

    private int DrawPlan(RhinoDoc doc, ToolpathPlan plan)
    {
        var count = 0;

        foreach (var operation in plan.Operations)
            count += DrawOperation(doc, plan.Plate, operation);

        return count;
    }

    private int DrawOperation(RhinoDoc doc, Plate plate, ToolpathOperationPlan operation)
    {
        var layerPath = $"{RootLayerName}::{SanitizeLayerName(plate.Name)}::{operation.PassType}";
        var layerIndex = EnsureLayer(doc, layerPath, GetColor(operation.PassType));
        var hiddenLinetype = FindLinetypeIndex(doc, "Hidden");
        var objectCount = 0;

        foreach (var primitive in operation.Primitives)
        {
            var attributes = new ObjectAttributes
            {
                LayerIndex = layerIndex,
                Name = operation.DisplayLabel
            };

            attributes.SetUserString("RhinoCNC.PassType", operation.PassType.ToString());
            attributes.SetUserString("RhinoCNC.MachiningType", operation.MachiningType.ToString());
            attributes.SetUserString("RhinoCNC.Tool", operation.Tool?.Name ?? string.Empty);
            attributes.SetUserString("RhinoCNC.TechCode", operation.Tool?.TechCode ?? string.Empty);
            attributes.SetUserString("RhinoCNC.OperationKey", operation.OperationKey ?? string.Empty);
            attributes.SetUserString("RhinoCNC.Depth", operation.Depth.ToString("0.###"));
            if (operation.StockToLeave.HasValue)
            {
                attributes.SetUserString("RhinoCNC.StockToLeave", operation.StockToLeave.Value.ToString("0.###"));
            }

            if (operation.PassType == ToolpathPassType.Rapid && hiddenLinetype >= 0)
            {
                attributes.LinetypeSource = ObjectLinetypeSource.LinetypeFromObject;
                attributes.LinetypeIndex = hiddenLinetype;
            }

            switch (primitive)
            {
                case ToolpathPolylinePrimitive polyline:
                {
                    var points = polyline.Points
                        .Select(point => LocalToWorld(plate, point.X, point.Y))
                        .ToList();
                    if (polyline.Closed && points.Count > 0 && points[0] != points[^1])
                        points.Add(points[0]);

                    var curve = new PolylineCurve(points);
                    if (doc.Objects.AddCurve(curve, attributes) != Guid.Empty)
                        objectCount++;
                    break;
                }
                case ToolpathLinePrimitive line:
                {
                    var start = LocalToWorld(plate, line.StartX, line.StartY);
                    var end = LocalToWorld(plate, line.EndX, line.EndY);
                    var curve = new LineCurve(start, end);
                    if (doc.Objects.AddCurve(curve, attributes) != Guid.Empty)
                        objectCount++;
                    break;
                }
                case ToolpathCirclePrimitive circle:
                {
                    var center = LocalToWorld(plate, circle.CenterX, circle.CenterY);
                    var plane = CreateLocalPlaneAt(plate, center);
                    var curve = new Circle(plane, circle.Diameter / 2.0).ToNurbsCurve();
                    if (doc.Objects.AddCurve(curve, attributes) != Guid.Empty)
                        objectCount++;
                    break;
                }
            }
        }

        return objectCount;
    }

    private static Point3d LocalToWorld(Plate plate, double x, double y)
    {
        var origin = plate.Origin;
        return new Point3d(
            origin.OriginX + x * origin.XAxis.X + y * origin.YAxis.X,
            origin.OriginY + x * origin.XAxis.Y + y * origin.YAxis.Y,
            origin.OriginZ + x * origin.XAxis.Z + y * origin.YAxis.Z);
    }

    private static Plane CreateLocalPlaneAt(Plate plate, Point3d originPoint)
    {
        return new Plane(
            originPoint,
            new Vector3d(plate.Origin.XAxis.X, plate.Origin.XAxis.Y, plate.Origin.XAxis.Z),
            new Vector3d(plate.Origin.YAxis.X, plate.Origin.YAxis.Y, plate.Origin.YAxis.Z));
    }

    private static string BuildPlateSelectionKey(PlatePreview preview)
    {
        if (!string.IsNullOrWhiteSpace(preview.Plate.LayerPath))
            return preview.Plate.LayerPath;

        return preview.Plate.Name;
    }

    private static bool TryCreateProfile(
        MachineFormat format,
        out IMachineProfile? profile,
        out string? error)
    {
        error = null;
        profile = format switch
        {
            MachineFormat.Xilog => new MaestroCadTProfile(),
            MachineFormat.Biesse => new BiesseProfile(),
            _ => null
        };

        if (profile == null)
        {
            error = $"Vorschau für {format} ist noch nicht implementiert.";
            return false;
        }

        return true;
    }

    private static string SanitizeLayerName(string name)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }

        return string.IsNullOrWhiteSpace(name) ? "Plate" : name;
    }

    private static Color GetColor(ToolpathPassType passType)
    {
        return passType switch
        {
            ToolpathPassType.Rapid => Color.DodgerBlue,
            ToolpathPassType.Roughing => Color.DarkOrange,
            ToolpathPassType.Finishing => Color.LimeGreen,
            ToolpathPassType.Drill => Color.Gold,
            ToolpathPassType.Macro => Color.DarkCyan,
            _ => Color.Red
        };
    }

    private static int FindLinetypeIndex(RhinoDoc doc, string name)
    {
        foreach (var linetype in doc.Linetypes)
        {
            if (linetype != null && string.Equals(linetype.Name, name, StringComparison.OrdinalIgnoreCase))
                return linetype.Index;
        }

        return -1;
    }

    private static int EnsureLayer(RhinoDoc doc, string fullPath, Color color)
    {
        var parts = fullPath.Split(new[] { "::" }, StringSplitOptions.RemoveEmptyEntries);
        var currentPath = string.Empty;
        var parentId = Guid.Empty;
        var lastIndex = -1;

        foreach (var part in parts)
        {
            currentPath = string.IsNullOrEmpty(currentPath) ? part : $"{currentPath}::{part}";
            var existing = doc.Layers
                .FirstOrDefault(layer => layer != null
                    && !layer.IsDeleted
                    && string.Equals(layer.FullPath, currentPath, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                lastIndex = existing.Index;
                parentId = existing.Id;
                continue;
            }

            var layer = new Layer
            {
                Name = part,
                Color = color,
                ParentLayerId = parentId
            };

            lastIndex = doc.Layers.Add(layer);
            if (lastIndex < 0)
                throw new InvalidOperationException($"Layer '{currentPath}' could not be created.");

            parentId = doc.Layers[lastIndex].Id;
        }

        if (lastIndex >= 0)
        {
            var layer = doc.Layers[lastIndex];
            if (layer != null && layer.Color != color)
            {
                layer.Color = color;
                doc.Layers.Modify(layer, lastIndex, false);
            }
        }

        return lastIndex;
    }
}

public sealed record ToolpathPreviewResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public int PlateCount { get; init; }
    public int OperationCount { get; init; }
    public int ObjectCount { get; init; }
    public string? ToolLibraryPath { get; init; }
}
