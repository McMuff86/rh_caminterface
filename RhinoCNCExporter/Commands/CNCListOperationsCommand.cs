using Rhino;
using Rhino.Commands;
using RhinoCNCExporter.Services;
using RhinoCNCExporter.Core.Blocks;

namespace RhinoCNCExporter.Commands;

/// <summary>
/// Command to list all CNC operations in the document.
/// </summary>
public sealed class CNCListOperationsCommand : Command
{
    public override string EnglishName => "CNCListOperations";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        try
        {
            var allOperations = CncOperationService.GetAllOperationsInDocument(doc).ToList();

            if (allOperations.Count == 0)
            {
                RhinoApp.WriteLine("Keine CNC-Bearbeitungen im Dokument gefunden.");
                return Result.Nothing;
            }

            RhinoApp.WriteLine($"\n=== CNC-Bearbeitungen im Dokument ({allOperations.Count}) ===");

            // Group by operation type for better overview
            var groupedOperations = allOperations
                .GroupBy(obj => CncOperationService.GetOperation(obj)?.Type ?? "Unknown")
                .OrderBy(g => g.Key);

            foreach (var group in groupedOperations)
            {
                RhinoApp.WriteLine($"\n{group.Key} ({group.Count()}):");
                
                foreach (var obj in group.Take(10)) // Limit to first 10 per type to avoid spam
                {
                    var operation = CncOperationService.GetOperation(obj);
                    if (operation != null)
                    {
                        var summary = CreateOperationSummary(obj, operation);
                        RhinoApp.WriteLine($"  • {summary}");
                    }
                }

                if (group.Count() > 10)
                {
                    RhinoApp.WriteLine($"  ... und {group.Count() - 10} weitere");
                }
            }

            // Show summary statistics
            RhinoApp.WriteLine($"\n=== Zusammenfassung ===");
            RhinoApp.WriteLine($"Gesamt: {allOperations.Count} Bearbeitungen");
            
            foreach (var group in groupedOperations)
            {
                RhinoApp.WriteLine($"{group.Key}: {group.Count()}");
            }

            // Show layer distribution
            var layerGroups = allOperations
                .GroupBy(obj => doc.Layers[obj.Attributes.LayerIndex].FullPath)
                .OrderBy(g => g.Key);

            if (layerGroups.Count() > 1)
            {
                RhinoApp.WriteLine($"\n=== Verteilung nach Layern ===");
                foreach (var layerGroup in layerGroups)
                {
                    RhinoApp.WriteLine($"{layerGroup.Key}: {layerGroup.Count()} Bearbeitungen");
                }
            }

            return Result.Success;
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"[CNCListOperations] Fehler: {ex.Message}");
            return Result.Failure;
        }
    }

    private string CreateOperationSummary(Rhino.DocObjects.RhinoObject obj, MachiningOperation operation)
    {
        try
        {
            var layer = RhinoDoc.ActiveDoc.Layers[obj.Attributes.LayerIndex];
            var layerName = layer?.Name ?? "?";
            
            var summary = $"{layerName}: ";

            switch (operation.Type.ToUpperInvariant())
            {
                case "CONTOUR":
                    summary += $"{operation.Tool}, Z{operation.Depth}, {operation.Strategy}";
                    if (operation.Feedrate.HasValue)
                        summary += $", F{operation.Feedrate:F0}";
                    break;

                case "POCKET":
                    summary += $"{operation.Tool}, Z{operation.Depth}";
                    if (operation.Stepover.HasValue)
                        summary += $", {operation.Stepover:F0}%";
                    summary += $", {operation.Strategy}";
                    if (!string.IsNullOrEmpty(operation.RampEntry))
                        summary += $", {operation.RampEntry}";
                    break;

                case "DRILL":
                    summary += $"⌀{operation.Diameter}, Z{operation.Depth}";
                    if (operation.Peck == true)
                        summary += $", Peck {operation.PeckDepth}";
                    break;

                case "GROOVE":
                    summary += $"{operation.Tool}, B{operation.Width}, Z{operation.Depth}";
                    break;

                default:
                    summary += operation.Type;
                    break;
            }

            return summary;
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }
}