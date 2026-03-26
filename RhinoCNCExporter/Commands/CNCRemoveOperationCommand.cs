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

            foreach (var objRef in go.Objects())
            {
                if (objRef.Object() is RhinoObject rhinoObj)
                {
                    var operation = CncOperationService.GetOperation(rhinoObj);
                    if (operation != null)
                    {
                        objectsWithOperations.Add(rhinoObj);
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

            // Remove operations, toolpath geometry, and restore colors
            foreach (var obj in objectsWithOperations)
            {
                // Remove grouped toolpath visualization geometry
                ToolpathVisualizer.RemoveToolpathGeometry(doc, obj);

                // Remove CNC operation UserText
                CncOperationService.RemoveOperation(obj);
                CncOperationService.RestoreDefaultColor(obj);
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
}
