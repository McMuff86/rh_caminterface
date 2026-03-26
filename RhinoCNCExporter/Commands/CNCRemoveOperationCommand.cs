using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using RhinoCNCExporter.Services;

namespace RhinoCNCExporter.Commands;

/// <summary>
/// Interactive command to remove CNC operations from selected objects.
/// </summary>
public sealed class CNCRemoveOperationCommand : Command
{
    public override string EnglishName => "CNCRemoveOperation";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        try
        {
            // Get object selection
            var go = new Rhino.Input.Custom.GetObject();
            go.SetCommandPrompt("Objekte auswählen um CNC-Bearbeitungen zu entfernen");
            go.GetMultiple(1, 0);

            if (go.CommandResult() != Result.Success)
                return go.CommandResult();

            var objectsWithOperations = new List<RhinoObject>();
            var textDotsToRemove = new List<Guid>();

            foreach (var objRef in go.Objects())
            {
                if (objRef.Object() is RhinoObject rhinoObj)
                {
                    var operation = CncOperationService.GetOperation(rhinoObj);
                    if (operation != null)
                    {
                        objectsWithOperations.Add(rhinoObj);
                        
                        // Find associated text dots on the same layer
                        FindAndMarkAssociatedTextDots(doc, rhinoObj, textDotsToRemove);
                    }
                }
            }

            if (objectsWithOperations.Count == 0)
            {
                RhinoApp.WriteLine("Keine Objekte mit CNC-Bearbeitungen in der Auswahl gefunden.");
                return Result.Nothing;
            }

            // Confirm removal
            var result = Rhino.UI.Dialogs.ShowMessage(
                $"CNC-Bearbeitungen von {objectsWithOperations.Count} Objekt(en) entfernen?",
                "Bestätigung",
                Rhino.UI.ShowMessageButton.YesNo,
                Rhino.UI.ShowMessageIcon.Question);

            if (result != Rhino.UI.ShowMessageResult.Yes)
                return Result.Cancel;

            // Remove operations and restore colors
            foreach (var obj in objectsWithOperations)
            {
                CncOperationService.RemoveOperation(obj);
                CncOperationService.RestoreDefaultColor(obj);
            }

            // Remove associated text dots
            foreach (var dotId in textDotsToRemove)
            {
                doc.Objects.Delete(dotId, true);
            }

            doc.Views.Redraw();
            RhinoApp.WriteLine($"CNC-Bearbeitungen von {objectsWithOperations.Count} Objekt(en) entfernt.");

            return Result.Success;
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"[CNCRemoveOperation] Fehler: {ex.Message}");
            return Result.Failure;
        }
    }

    private void FindAndMarkAssociatedTextDots(RhinoDoc doc, RhinoObject operationObj, List<Guid> textDotsToRemove)
    {
        try
        {
            // Find text dots on the same layer that are likely operation summaries
            var operationBBox = operationObj.Geometry.GetBoundingBox(true);
            var searchRadius = Math.Max(operationBBox.Diagonal.Length * 0.5, 10.0); // Search within reasonable distance

            var allTextDots = doc.Objects.FindByObjectType(ObjectType.TextDot);
            foreach (var dotObj in allTextDots)
            {
                if (dotObj.Attributes.LayerIndex == operationObj.Attributes.LayerIndex &&
                    dotObj.Geometry is TextDot textDot)
                {
                    // Check if text dot is close to the operation object
                    var distance = operationBBox.Center.DistanceTo(textDot.Point);
                    if (distance <= searchRadius)
                    {
                        // Check if text contains typical operation keywords
                        var text = textDot.Text.ToUpperInvariant();
                        if (text.Contains("CONTOUR") || text.Contains("POCKET") || 
                            text.Contains("DRILL") || text.Contains("GROOVE") ||
                            text.Contains("ROUGH") || text.Contains("FINISH"))
                        {
                            textDotsToRemove.Add(dotObj.Id);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"[CNCRemoveOperation] Warning: Error finding text dots: {ex.Message}");
        }
    }
}