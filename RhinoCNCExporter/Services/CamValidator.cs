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
/// Severity levels for validation issues.
/// </summary>
public enum Severity
{
    Info,
    Warning,
    Error
}

/// <summary>
/// A single validation issue found during pre-export checks.
/// </summary>
public class ValidationIssue
{
    public Severity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid? ObjectId { get; set; }
    public string Category { get; set; } = string.Empty;
}

/// <summary>
/// Result of a validation run containing all discovered issues.
/// </summary>
public class ValidationResult
{
    public List<ValidationIssue> Issues { get; } = new();
    public bool HasErrors => Issues.Any(i => i.Severity == Severity.Error);
    public bool HasWarnings => Issues.Any(i => i.Severity == Severity.Warning);
    public bool IsClean => Issues.Count == 0;

    public int ErrorCount => Issues.Count(i => i.Severity == Severity.Error);
    public int WarningCount => Issues.Count(i => i.Severity == Severity.Warning);
    public int InfoCount => Issues.Count(i => i.Severity == Severity.Info);

    /// <summary>
    /// Returns a short summary string like "2 Fehler, 3 Warnungen".
    /// </summary>
    public string FormatSummary()
    {
        var parts = new List<string>();
        if (ErrorCount > 0)
            parts.Add($"{ErrorCount} Fehler");
        if (WarningCount > 0)
            parts.Add($"{WarningCount} Warnung{(WarningCount != 1 ? "en" : "")}");
        if (InfoCount > 0)
            parts.Add($"{InfoCount} Info{(InfoCount != 1 ? "s" : "")}");
        return parts.Count > 0 ? string.Join(", ", parts) : "Keine Probleme gefunden";
    }
}

/// <summary>
/// Pre-export validation system for CNC operations.
/// Checks all operations in the document for common issues before export.
/// </summary>
public static class CamValidator
{
    /// <summary>
    /// Validates all operations in the document. Returns a ValidationResult
    /// containing all discovered issues.
    /// </summary>
    /// <param name="doc">Active Rhino document.</param>
    /// <param name="tools">Available tools from the tool library.</param>
    /// <param name="targetFormat">Target machine format for emitter compatibility check.</param>
    public static ValidationResult Validate(
        RhinoDoc doc,
        IReadOnlyList<ToolDefinition> tools,
        MachineFormat? targetFormat = null)
    {
        var result = new ValidationResult();

        if (doc == null)
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = Severity.Error,
                Message = "Kein aktives Rhino-Dokument geöffnet.",
                Category = "Dokument"
            });
            return result;
        }

        var operations = CncOperationService.GetAllOperationsInDocument(doc).ToList();

        // Check: Empty operations list
        if (operations.Count == 0)
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = Severity.Error,
                Message = "Keine CNC-Operationen zum Exportieren vorhanden.",
                Category = "Operationen"
            });
            return result;
        }

        // Track duplicates: objectId → list of operation types
        var operationsByObject = new Dictionary<Guid, List<string>>();

        foreach (var obj in operations)
        {
            var op = CncOperationService.GetOperation(obj);
            if (op == null) continue;

            var objId = obj.Id;

            // Track for duplicate detection
            if (!operationsByObject.TryGetValue(objId, out var opTypes))
            {
                opTypes = new List<string>();
                operationsByObject[objId] = opTypes;
            }
            opTypes.Add(op.Type);

            // Check: Tool not assigned
            ValidateToolAssigned(result, obj, op, tools);

            // Check: Depth exceeds material thickness
            ValidateDepthVsMaterial(result, doc, obj, op);

            // Check: Tool diameter vs feature size (pocket)
            ValidateToolVsFeatureSize(result, obj, op, tools);

            // Check: Missing feedrate
            ValidateFeedrate(result, obj, op);

            // Check: Orphan edge curves
            ValidateOrphanEdgeCurve(result, doc, obj);

            // Check: Invalid geometry
            ValidateGeometry(result, obj, op);

            // Check: Unsupported operation for emitter
            if (targetFormat.HasValue)
                ValidateEmitterSupport(result, obj, op, targetFormat.Value);
        }

        // Check: Duplicate operations on same object
        ValidateDuplicateOperations(result, operationsByObject);

        return result;
    }

    /// <summary>
    /// Checks that a tool is assigned to the operation.
    /// </summary>
    private static void ValidateToolAssigned(
        ValidationResult result, RhinoObject obj, MachiningOperation op,
        IReadOnlyList<ToolDefinition> tools)
    {
        if (string.IsNullOrEmpty(op.Tool) || op.Tool == "—")
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = Severity.Error,
                Message = $"Kein Werkzeug zugewiesen für {op.Type}-Operation auf '{GetObjectName(obj)}'.",
                ObjectId = obj.Id,
                Category = "Werkzeug"
            });
            return;
        }

        // Check tool exists in library
        if (tools.Count > 0)
        {
            var toolExists = tools.Any(t =>
                t.Name.Equals(op.Tool, StringComparison.OrdinalIgnoreCase));
            if (!toolExists)
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = Severity.Warning,
                    Message = $"Werkzeug '{op.Tool}' für {op.Type} auf '{GetObjectName(obj)}' nicht in der Bibliothek gefunden.",
                    ObjectId = obj.Id,
                    Category = "Werkzeug"
                });
            }
        }
    }

    /// <summary>
    /// Checks if depth exceeds material thickness (when plate thickness is known).
    /// </summary>
    private static void ValidateDepthVsMaterial(
        ValidationResult result, RhinoDoc doc, RhinoObject obj, MachiningOperation op)
    {
        if (!op.Depth.HasValue || op.Depth.Value <= 0) return;

        // Try to find plate thickness from source brep
        double? plateThickness = null;

        var sourceBrepId = obj.Attributes.GetUserString(CncOperationSchema.CNC_SOURCE_BREP);
        if (!string.IsNullOrEmpty(sourceBrepId) && Guid.TryParse(sourceBrepId, out var brepGuid))
        {
            var brepObj = doc.Objects.FindId(brepGuid);
            if (brepObj?.Geometry is Brep brep)
            {
                var bbox = brep.GetBoundingBox(true);
                if (bbox.IsValid)
                {
                    // Thickness is the smallest dimension
                    var dims = new[]
                    {
                        bbox.Max.X - bbox.Min.X,
                        bbox.Max.Y - bbox.Min.Y,
                        bbox.Max.Z - bbox.Min.Z
                    };
                    Array.Sort(dims);
                    plateThickness = dims[0]; // smallest = thickness
                }
            }
        }

        if (plateThickness.HasValue && op.Depth.Value > plateThickness.Value)
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = Severity.Warning,
                Message = $"Tiefe ({op.Depth.Value:F1}mm) überschreitet Materialstärke ({plateThickness.Value:F1}mm) " +
                          $"bei {op.Type} auf '{GetObjectName(obj)}'.",
                ObjectId = obj.Id,
                Category = "Tiefe"
            });
        }
    }

    /// <summary>
    /// Checks if a pocket's feature size is smaller than the tool diameter.
    /// </summary>
    private static void ValidateToolVsFeatureSize(
        ValidationResult result, RhinoObject obj, MachiningOperation op,
        IReadOnlyList<ToolDefinition> tools)
    {
        // Only relevant for pocket operations
        if (!op.Type.Equals("Pocket", StringComparison.OrdinalIgnoreCase))
            return;

        if (obj.Geometry is not Curve curve || !curve.IsClosed)
            return;

        var toolDiameter = ResolveToolDiameter(op, tools);
        if (toolDiameter <= 0) return;

        // Get the bounding box of the curve and check minimum dimension
        var bbox = curve.GetBoundingBox(true);
        if (!bbox.IsValid) return;

        var minDim = Math.Min(
            bbox.Max.X - bbox.Min.X,
            bbox.Max.Y - bbox.Min.Y);

        if (minDim < toolDiameter)
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = Severity.Error,
                Message = $"Tasche ({minDim:F1}mm) ist kleiner als Werkzeugdurchmesser ({toolDiameter:F1}mm) " +
                          $"auf '{GetObjectName(obj)}'. Werkzeug passt nicht in die Kontur.",
                ObjectId = obj.Id,
                Category = "Geometrie"
            });
        }
    }

    /// <summary>
    /// Checks for missing feedrate (warns and suggests default).
    /// </summary>
    private static void ValidateFeedrate(
        ValidationResult result, RhinoObject obj, MachiningOperation op)
    {
        if (!op.Feedrate.HasValue || op.Feedrate.Value <= 0)
        {
            var defaultFeed = GetDefaultFeedrate(op.Type);
            result.Issues.Add(new ValidationIssue
            {
                Severity = Severity.Warning,
                Message = $"Kein Vorschub gesetzt für {op.Type} auf '{GetObjectName(obj)}'. " +
                          $"Standard ({defaultFeed:F0} mm/min) wird verwendet.",
                ObjectId = obj.Id,
                Category = "Vorschub"
            });
        }
    }

    /// <summary>
    /// Checks if an extracted edge curve's source Brep still exists.
    /// </summary>
    private static void ValidateOrphanEdgeCurve(
        ValidationResult result, RhinoDoc doc, RhinoObject obj)
    {
        if (!EdgeCurveHelper.IsExtractedEdgeCurve(obj))
            return;

        var sourceBrepId = obj.Attributes.GetUserString(CncOperationSchema.CNC_SOURCE_BREP);
        if (string.IsNullOrEmpty(sourceBrepId))
            return;

        if (!Guid.TryParse(sourceBrepId, out var brepGuid))
            return;

        var sourceBrep = doc.Objects.FindId(brepGuid);
        if (sourceBrep == null)
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = Severity.Warning,
                Message = $"Kantenkurve '{GetObjectName(obj)}' verweist auf einen gelöschten Brep. " +
                          $"Die Kurve ist verwaist und sollte überprüft werden.",
                ObjectId = obj.Id,
                Category = "Geometrie"
            });
        }
    }

    /// <summary>
    /// Validates that geometry is valid for the operation type.
    /// </summary>
    private static void ValidateGeometry(
        ValidationResult result, RhinoObject obj, MachiningOperation op)
    {
        var geometry = obj.Geometry;
        if (geometry == null)
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = Severity.Error,
                Message = $"Objekt '{GetObjectName(obj)}' hat keine gültige Geometrie.",
                ObjectId = obj.Id,
                Category = "Geometrie"
            });
            return;
        }

        switch (op.Type.ToUpperInvariant())
        {
            case "CONTOUR":
            case "GROOVE":
                if (geometry is not Curve contourCurve)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = Severity.Error,
                        Message = $"{op.Type} erwartet eine Kurve, aber '{GetObjectName(obj)}' ist {geometry.ObjectType}.",
                        ObjectId = obj.Id,
                        Category = "Geometrie"
                    });
                }
                else if (contourCurve.GetLength() < 0.01)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = Severity.Warning,
                        Message = $"Kurve '{GetObjectName(obj)}' ist sehr kurz ({contourCurve.GetLength():F2}mm). " +
                                  $"Möglicherweise degenerierte Geometrie.",
                        ObjectId = obj.Id,
                        Category = "Geometrie"
                    });
                }
                break;

            case "POCKET":
                if (geometry is not Curve pocketCurve)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = Severity.Error,
                        Message = $"Tasche erwartet eine geschlossene Kurve, aber '{GetObjectName(obj)}' ist {geometry.ObjectType}.",
                        ObjectId = obj.Id,
                        Category = "Geometrie"
                    });
                }
                else if (!pocketCurve.IsClosed)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = Severity.Error,
                        Message = $"Taschenkurve '{GetObjectName(obj)}' ist nicht geschlossen. " +
                                  $"Pockets benötigen geschlossene Konturen.",
                        ObjectId = obj.Id,
                        Category = "Geometrie"
                    });
                }
                break;

            case "DRILL":
                // Drills can be points or small circles — both are valid
                if (geometry is not Rhino.Geometry.Point && geometry is not Curve)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = Severity.Error,
                        Message = $"Bohrung erwartet einen Punkt oder Kreis, aber '{GetObjectName(obj)}' ist {geometry.ObjectType}.",
                        ObjectId = obj.Id,
                        Category = "Geometrie"
                    });
                }
                break;
        }
    }

    /// <summary>
    /// Checks if the target emitter supports the operation type.
    /// </summary>
    private static void ValidateEmitterSupport(
        ValidationResult result, RhinoObject obj, MachiningOperation op,
        MachineFormat format)
    {
        // Both XilogEmitter and BiesseEmitter support all basic operation types
        // (Contour/Pocket/Drill/Groove). Only Homag is not yet implemented.
        if (format == MachineFormat.Homag)
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = Severity.Error,
                Message = $"Homag (.mpr) Export ist noch nicht implementiert. " +
                          $"Operation '{op.Type}' auf '{GetObjectName(obj)}' kann nicht exportiert werden.",
                ObjectId = obj.Id,
                Category = "Emitter"
            });
        }
    }

    /// <summary>
    /// Checks for duplicate operations (same type) on the same object.
    /// </summary>
    private static void ValidateDuplicateOperations(
        ValidationResult result, Dictionary<Guid, List<string>> operationsByObject)
    {
        foreach (var kvp in operationsByObject)
        {
            var types = kvp.Value;
            var duplicates = types
                .GroupBy(t => t, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .ToList();

            foreach (var dup in duplicates)
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = Severity.Warning,
                    Message = $"Objekt hat {dup.Count()}× die gleiche Operation '{dup.Key}'. " +
                              $"Möglicherweise ein Duplikat.",
                    ObjectId = kvp.Key,
                    Category = "Duplikat"
                });
            }
        }
    }

    /// <summary>
    /// Resolves tool diameter from operation or tool library.
    /// </summary>
    private static double ResolveToolDiameter(MachiningOperation op, IReadOnlyList<ToolDefinition> tools)
    {
        if (op.Diameter.HasValue && op.Diameter.Value > 0)
            return op.Diameter.Value;

        if (!string.IsNullOrEmpty(op.Tool) && tools.Count > 0)
        {
            var tool = tools.FirstOrDefault(t =>
                t.Name.Equals(op.Tool, StringComparison.OrdinalIgnoreCase));
            if (tool != null)
                return tool.NominalDiameter;
        }

        return 0;
    }

    /// <summary>
    /// Gets a display name for an object.
    /// </summary>
    private static string GetObjectName(RhinoObject obj)
    {
        if (!string.IsNullOrEmpty(obj.Name))
            return obj.Name;
        return obj.Id.ToString()[..8];
    }

    /// <summary>
    /// Returns default feedrate for an operation type.
    /// </summary>
    private static double GetDefaultFeedrate(string operationType)
    {
        return operationType.ToUpperInvariant() switch
        {
            "CONTOUR" => 3000.0,
            "POCKET" => 2000.0,
            "DRILL" => 1000.0,
            "GROOVE" => 2500.0,
            _ => 2000.0
        };
    }
}
