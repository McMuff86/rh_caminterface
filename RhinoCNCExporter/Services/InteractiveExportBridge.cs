using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using RhinoCNCExporter.Core.Blocks;
using RhinoCNCExporter.Core.Emitters;
using RhinoCNCExporter.Core.Models;
using RhinoCNCExporter.Core.Naming;
using RhinoCNCExporter.Core.Pipeline;
using RhinoCNCExporter.Core.Profiles;
using RhinoCNCExporter.UI;

namespace RhinoCNCExporter.Services;

/// <summary>
/// Bridges interactive CAM operations (UserText on objects) to the export pipeline.
/// Reads all objects with CNC_OperationType UserText, converts to Machining objects,
/// groups by plate (if possible), and feeds into the existing IEmitter system.
/// </summary>
public class InteractiveExportBridge
{
    private readonly ToolLibraryStore _toolLibraryStore = new();

    /// <summary>
    /// Collect all interactive CAM operations from the document and convert to Machining objects.
    /// </summary>
    public IReadOnlyList<InteractiveOperation> CollectOperations(RhinoDoc doc)
    {
        if (doc == null) return Array.Empty<InteractiveOperation>();

        var result = new List<InteractiveOperation>();

        IEnumerable<RhinoObject> objects;
        try
        {
            objects = CncOperationService.GetAllOperationsInDocument(doc).ToList();
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"[ExportBridge] Fehler beim Lesen der Operationen: {ex.Message}");
            return result;
        }

        foreach (var obj in objects)
        {
            try
            {
                var op = CncOperationService.GetOperation(obj);
                if (op == null) continue;

                // Guard against malformed UserText
                if (string.IsNullOrEmpty(op.Type)) continue;

                var machining = ConvertToMachining(doc, obj, op);
                if (machining == null) continue;

                // Try to determine plate/source brep
                var sourceBrepId = obj.Attributes.GetUserString(CncOperationSchema.CNC_SOURCE_BREP);

                var layerIndex = obj.Attributes.LayerIndex;
                var layerName = layerIndex >= 0 && layerIndex < doc.Layers.Count
                    ? doc.Layers[layerIndex]?.Name ?? "Default"
                    : "Default";

                result.Add(new InteractiveOperation
                {
                    ObjectId = obj.Id,
                    Operation = op,
                    Machining = machining,
                    SourceBrepId = string.IsNullOrEmpty(sourceBrepId) ? null : sourceBrepId,
                    LayerName = layerName
                });
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"[ExportBridge] Fehler bei Objekt {obj.Id}: {ex.Message}");
            }
        }

        return result;
    }

    /// <summary>
    /// Group collected operations by plate (source brep) or layer.
    /// Returns groups where each group represents one CNC file/plate.
    /// </summary>
    public IReadOnlyList<PlateGroup> GroupByPlate(
        RhinoDoc doc,
        IReadOnlyList<InteractiveOperation> operations)
    {
        // Strategy 1: Group by CNC_SourceBrep (edge curves extracted from breps)
        // Strategy 2: Group by layer
        // Strategy 3: All in one group (flat export)

        var groups = new Dictionary<string, PlateGroup>(StringComparer.OrdinalIgnoreCase);

        foreach (var op in operations)
        {
            string groupKey;
            string plateName;
            double thickness = 19.0; // Default
            double lengthX = 1000.0;
            double widthY = 600.0;

            if (!string.IsNullOrEmpty(op.SourceBrepId))
            {
                groupKey = op.SourceBrepId!;
                // Try to get plate dimensions from the source brep
                if (Guid.TryParse(op.SourceBrepId, out var brepGuid))
                {
                    var brepObj = doc.Objects.FindId(brepGuid);
                    if (brepObj?.Geometry is Brep brep)
                    {
                        var bbox = brep.GetBoundingBox(true);
                        var dims = SortDimensions(bbox);
                        lengthX = dims.length;
                        widthY = dims.width;
                        thickness = dims.thickness;
                        plateName = !string.IsNullOrEmpty(brepObj.Name)
                            ? brepObj.Name
                            : doc.Layers[brepObj.Attributes.LayerIndex]?.Name ?? $"Plate_{brepGuid.ToString()[..8]}";
                    }
                    else
                    {
                        plateName = $"Plate_{op.SourceBrepId[..Math.Min(8, op.SourceBrepId.Length)]}";
                    }
                }
                else
                {
                    plateName = $"Plate_{op.SourceBrepId[..Math.Min(8, op.SourceBrepId.Length)]}";
                }
            }
            else
            {
                // Group by layer
                groupKey = $"layer:{op.LayerName}";
                plateName = op.LayerName;

                // Try to find a brep on the same layer for dimensions
                var layerIndex = doc.Layers.FindByFullPath(op.LayerName, -1);
                if (layerIndex >= 0)
                {
                    var layerBreps = doc.Objects
                        .Where(o => o.Attributes.LayerIndex == layerIndex && o.Geometry is Brep)
                        .Select(o => o.Geometry as Brep)
                        .Where(b => b != null)
                        .ToList();

                    if (layerBreps.Count > 0)
                    {
                        // Use largest brep as plate
                        var largest = layerBreps.OrderByDescending(b => b!.GetArea()).First();
                        var bbox = largest!.GetBoundingBox(true);
                        var dims = SortDimensions(bbox);
                        lengthX = dims.length;
                        widthY = dims.width;
                        thickness = dims.thickness;
                    }
                }
            }

            if (!groups.TryGetValue(groupKey, out var group))
            {
                group = new PlateGroup
                {
                    Key = groupKey,
                    PlateName = plateName,
                    LengthX = lengthX,
                    WidthY = widthY,
                    Thickness = thickness,
                    Operations = new List<InteractiveOperation>()
                };
                groups[groupKey] = group;
            }

            ((List<InteractiveOperation>)group.Operations).Add(op);
        }

        return groups.Values.ToList();
    }

    /// <summary>
    /// Export all interactive operations to CNC code using the specified emitter.
    /// </summary>
    public InteractiveExportResult Export(
        RhinoDoc doc,
        string outputPath,
        MachineFormat format,
        IMachineProfile profile)
    {
        var result = new InteractiveExportResult();

        try
        {
            var operations = CollectOperations(doc);
            if (operations.Count == 0)
            {
                result.Success = false;
                result.Error = "Keine interaktiven CNC-Operationen gefunden.";
                return result;
            }

            var groups = GroupByPlate(doc, operations);
            var nameService = new NameService();
            var emitter = CreateEmitter(format, nameService);

            if (emitter == null)
            {
                result.Success = false;
                result.Error = $"Kein Emitter für Format '{format}' verfügbar.";
                return result;
            }

            var router = new EmitterRouter(emitter, nameService, profile);

            if (groups.Count == 1)
            {
                // Single plate → single file
                var group = groups[0];
                var plate = BuildPlate(group);
                var program = router.GenerateProgram(plate);

                // Ensure correct extension
                var extension = GetFileExtension(format);
                if (!outputPath.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                    outputPath = Path.ChangeExtension(outputPath, extension);

                try
                {
                    File.WriteAllText(outputPath, program, System.Text.Encoding.UTF8);
                }
                catch (UnauthorizedAccessException)
                {
                    result.Success = false;
                    result.Error = $"Keine Schreibberechtigung für '{outputPath}'.";
                    return result;
                }
                catch (PathTooLongException)
                {
                    result.Success = false;
                    result.Error = $"Dateipfad zu lang: '{outputPath}'.";
                    return result;
                }
                catch (IOException ioEx)
                {
                    result.Success = false;
                    result.Error = $"Fehler beim Schreiben: {ioEx.Message}";
                    return result;
                }

                result.ExportedFiles.Add(outputPath);
            }
            else
            {
                // Multiple plates → directory with one file per plate
                var dir = Path.GetDirectoryName(outputPath) ?? outputPath;

                try
                {
                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                }
                catch (Exception dirEx)
                {
                    result.Success = false;
                    result.Error = $"Verzeichnis konnte nicht erstellt werden: {dirEx.Message}";
                    return result;
                }

                foreach (var group in groups)
                {
                    var plate = BuildPlate(group);
                    nameService = new NameService(); // Fresh name service per plate
                    emitter = CreateEmitter(format, nameService);
                    router = new EmitterRouter(emitter!, nameService, profile);

                    var program = router.GenerateProgram(plate);
                    var extension = GetFileExtension(format);
                    var fileName = SanitizeFileName(group.PlateName) + extension;
                    var filePath = Path.Combine(dir, fileName);

                    try
                    {
                        File.WriteAllText(filePath, program, System.Text.Encoding.UTF8);
                    }
                    catch (Exception writeEx)
                    {
                        result.Success = false;
                        result.Error = $"Fehler beim Schreiben von '{filePath}': {writeEx.Message}";
                        return result;
                    }

                    result.ExportedFiles.Add(filePath);
                }
            }

            result.Success = true;
            result.OperationCount = operations.Count;
            result.PlateCount = groups.Count;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// Build a Plate model from a group of interactive operations.
    /// </summary>
    private Plate BuildPlate(PlateGroup group)
    {
        return new Plate
        {
            Name = group.PlateName,
            LengthX = group.LengthX,
            WidthY = group.WidthY,
            Thickness = group.Thickness,
            Source = PlateSource.Manual,
            PreserveMachiningOrder = true,
            Machinings = group.Operations.Select(op => op.Machining).ToList()
        };
    }

    /// <summary>
    /// Convert a UserText-based operation to a Machining object.
    /// </summary>
    private Machining? ConvertToMachining(RhinoDoc doc, RhinoObject obj, MachiningOperation op)
    {
        var layerName = doc.Layers[obj.Attributes.LayerIndex]?.Name ?? "Unknown";
        var name = !string.IsNullOrEmpty(obj.Name)
            ? obj.Name
            : $"{layerName}_{obj.Id.ToString()[..8]}";

        try
        {
            return op.Type.ToUpperInvariant() switch
            {
                "CONTOUR" => CreateContourMachining(obj, op, name),
                "POCKET" => CreatePocketMachining(obj, op, name),
                "DRILL" => CreateDrillMachining(obj, op, name),
                "GROOVE" => CreateGrooveMachining(obj, op, name),
                _ => null
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[InteractiveExportBridge] Error converting {op.Type}: {ex.Message}");
            return null;
        }
    }

    private Machining? CreateContourMachining(RhinoObject obj, MachiningOperation op, string name)
    {
        if (obj.Geometry is not Curve curve)
            return null;

        var points = ExtractCurvePoints(curve);
        if (points.Count == 0)
            return null;

        var toolDiameter = ResolveToolDiameter(op);
        var depth = op.Depth ?? 10.0;

        return new RoutingMachining
        {
            Name = name,
            Points = points,
            Depth = depth,
            ToolDiameter = toolDiameter,
            IsClosed = curve.IsClosed,
            Source = MachiningSource.Manual,
            TechCode = "E010"
        };
    }

    private Machining? CreatePocketMachining(RhinoObject obj, MachiningOperation op, string name)
    {
        if (obj.Geometry is not Curve curve || !curve.IsClosed)
            return null;

        var points = ExtractCurvePoints(curve);
        if (points.Count == 0)
            return null;

        var toolDiameter = ResolveToolDiameter(op);
        var depth = op.Depth ?? 10.0;

        return new PocketMachining
        {
            Name = name,
            Loops = new[] { points },
            Depth = depth,
            ToolDiameter = toolDiameter,
            Source = MachiningSource.Manual,
            TechCode = "E010"
        };
    }

    private Machining? CreateDrillMachining(RhinoObject obj, MachiningOperation op, string name)
    {
        Point3d center;
        if (obj.Geometry is Rhino.Geometry.Point point)
            center = point.Location;
        else if (obj.Geometry is Curve drillCurve)
            center = drillCurve.PointAtStart;
        else
        {
            var bbox = obj.Geometry.GetBoundingBox(true);
            if (!bbox.IsValid) return null;
            center = bbox.Center;
        }

        var diameter = op.Diameter ?? 5.0;
        var depth = op.Depth ?? 10.0;

        return new DrillMachining
        {
            Name = name,
            X = center.X,
            Y = center.Y,
            Depth = depth,
            Diameter = diameter,
            Source = MachiningSource.Manual,
            TechCode = diameter <= 5.0 ? "E013" : "E009"
        };
    }

    private Machining? CreateGrooveMachining(RhinoObject obj, MachiningOperation op, string name)
    {
        if (obj.Geometry is not Curve curve)
            return null;

        var points = ExtractCurvePoints(curve);
        if (points.Count == 0)
            return null;

        var toolDiameter = ResolveToolDiameter(op);
        var depth = op.Depth ?? 8.0;

        return new RoutingMachining
        {
            Name = name,
            Points = points,
            Depth = depth,
            ToolDiameter = toolDiameter,
            IsClosed = false,
            Source = MachiningSource.Manual,
            TechCode = "E010"
        };
    }

    private double ResolveToolDiameter(MachiningOperation op)
    {
        // First try explicit diameter
        if (op.Diameter.HasValue && op.Diameter.Value > 0)
            return op.Diameter.Value;

        // Try tool name lookup
        if (!string.IsNullOrEmpty(op.Tool))
        {
            // Try regex extraction from tool name
            var match = System.Text.RegularExpressions.Regex.Match(op.Tool, @"(\d+(?:\.\d+)?)\s*mm");
            if (match.Success && double.TryParse(match.Groups[1].Value,
                NumberStyles.Float, CultureInfo.InvariantCulture, out var diameter))
            {
                return diameter;
            }
        }

        return 6.0; // Default fallback
    }

    private static IReadOnlyList<(double X, double Y)> ExtractCurvePoints(Curve curve)
    {
        var points = new List<(double X, double Y)>();

        var polyline = curve.ToPolyline(0, 0, 0.5, 0.1, 0, 0, 0, 0, true);
        if (polyline != null)
        {
            for (int i = 0; i < polyline.PointCount; i++)
            {
                var pt = polyline.Point(i);
                points.Add((pt.X, pt.Y));
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
                points.Add((pt.X, pt.Y));
            }
        }

        return points;
    }

    private static (double length, double width, double thickness) SortDimensions(BoundingBox bbox)
    {
        var dims = new[] { bbox.Max.X - bbox.Min.X, bbox.Max.Y - bbox.Min.Y, bbox.Max.Z - bbox.Min.Z };
        Array.Sort(dims);
        return (dims[2], dims[1], dims[0]); // largest = length, middle = width, smallest = thickness
    }

    private static IEmitter? CreateEmitter(MachineFormat format, NameService nameService)
    {
        return format switch
        {
            MachineFormat.Xilog => new XilogEmitter(nameService),
            MachineFormat.Biesse => new BiesseEmitter(nameService),
            _ => null
        };
    }

    private static string GetFileExtension(MachineFormat format)
    {
        return format switch
        {
            MachineFormat.Biesse => ".cix",
            MachineFormat.Homag => ".mpr",
            _ => ".xcs"
        };
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "program" : sanitized;
    }

    /// <summary>
    /// Generates CNC code strings for all plates WITHOUT writing to file.
    /// Used by ExportPreviewDialog to show generated code before export.
    /// Returns a list of PlatePreview objects with the generated code per plate.
    /// </summary>
    public IReadOnlyList<UI.PlatePreview> GenerateCode(
        RhinoDoc doc,
        MachineFormat format,
        IMachineProfile profile)
    {
        var result = new List<UI.PlatePreview>();

        var operations = CollectOperations(doc);
        if (operations.Count == 0)
            return result;

        var groups = GroupByPlate(doc, operations);
        var nameService = new NameService();
        var emitter = CreateEmitter(format, nameService);

        if (emitter == null)
            return result;

        foreach (var group in groups)
        {
            nameService = new NameService(); // Fresh per plate
            emitter = CreateEmitter(format, nameService);
            var router = new EmitterRouter(emitter!, nameService, profile);

            var plate = BuildPlate(group);
            string code;

            try
            {
                code = router.GenerateProgram(plate);
            }
            catch (Exception ex)
            {
                code = $"// FEHLER bei Code-Generierung: {ex.Message}";
            }

            result.Add(new UI.PlatePreview
            {
                Name = group.PlateName,
                LengthX = group.LengthX,
                WidthY = group.WidthY,
                Thickness = group.Thickness,
                OperationCount = group.Operations.Count,
                Code = code
            });
        }

        return result;
    }

    /// <summary>
    /// Get operation statistics for the current document.
    /// </summary>
    public static OperationStatistics GetStatistics(RhinoDoc doc, IReadOnlyList<ToolDefinition> tools)
    {
        var stats = new OperationStatistics();
        var operations = CncOperationService.GetAllOperationsInDocument(doc).ToList();

        var toolsUsed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var obj in operations)
        {
            var op = CncOperationService.GetOperation(obj);
            if (op == null) continue;

            stats.TotalOperations++;

            switch (op.Type.ToUpperInvariant())
            {
                case "CONTOUR": stats.ContourCount++; break;
                case "POCKET": stats.PocketCount++; break;
                case "DRILL": stats.DrillCount++; break;
                case "GROOVE": stats.GrooveCount++; break;
            }

            if (op.Depth.HasValue && op.Depth.Value > stats.MaxDepth)
                stats.MaxDepth = op.Depth.Value;

            if (!string.IsNullOrEmpty(op.Tool))
                toolsUsed.Add(op.Tool!);

            // Estimate machining time
            var feedrate = op.Feedrate ?? GetDefaultFeedrate(op.Type);
            var pathLength = GetPathLength(obj);
            if (feedrate > 0 && pathLength > 0)
            {
                stats.EstimatedTimeMinutes += pathLength / feedrate; // mm / (mm/min)
            }
        }

        stats.ToolChanges = Math.Max(0, toolsUsed.Count - 1);
        return stats;
    }

    private static double GetDefaultFeedrate(string operationType)
    {
        return operationType.ToUpperInvariant() switch
        {
            "CONTOUR" => 3000.0, // mm/min
            "POCKET" => 2000.0,
            "DRILL" => 1000.0,
            "GROOVE" => 2500.0,
            _ => 2000.0
        };
    }

    private static double GetPathLength(RhinoObject obj)
    {
        if (obj.Geometry is Curve curve)
            return curve.GetLength();

        if (obj.Geometry is Rhino.Geometry.Point)
        {
            // For drills, path length is just the depth (plunge)
            var depthStr = obj.Attributes.GetUserString(CncOperationSchema.CNC_DEPTH);
            if (double.TryParse(depthStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var depth))
                return depth;
            return 10.0;
        }

        return 0;
    }
}

/// <summary>
/// Represents a single interactive operation with its converted Machining.
/// </summary>
public class InteractiveOperation
{
    public Guid ObjectId { get; init; }
    public required MachiningOperation Operation { get; init; }
    public required Machining Machining { get; init; }
    public string? SourceBrepId { get; init; }
    public string LayerName { get; init; } = "Default";
}

/// <summary>
/// A group of operations belonging to the same plate/source.
/// </summary>
public class PlateGroup
{
    public required string Key { get; init; }
    public required string PlateName { get; init; }
    public double LengthX { get; init; } = 1000.0;
    public double WidthY { get; init; } = 600.0;
    public double Thickness { get; init; } = 19.0;
    public IReadOnlyList<InteractiveOperation> Operations { get; init; } = Array.Empty<InteractiveOperation>();
}

/// <summary>
/// Result of an interactive export operation.
/// </summary>
public class InteractiveExportResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int OperationCount { get; set; }
    public int PlateCount { get; set; }
    public List<string> ExportedFiles { get; set; } = new();
}

// OperationStatistics is now in RhinoCNCExporter.Core.Models
