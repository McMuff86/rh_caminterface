using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using RhinoCNCExporter.Services;

namespace RhinoCNCExporter.Commands;

/// <summary>
/// Interactive command to remove CNC operations from selected objects.
/// Handles both standalone curves and extracted edge curves.
/// </summary>
public sealed class CNCRemoveOperationCommand : Command
{
    public override string EnglishName => "CNCRemoveOperation";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        try
        {
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
                        objectsWithOperations.Add(rhinoObj);
                }
            }

            if (objectsWithOperations.Count == 0)
            {
                RhinoApp.WriteLine("Keine Objekte mit CNC-Bearbeitungen in der Auswahl gefunden.");
                return Result.Nothing;
            }

            var result = Rhino.UI.Dialogs.ShowMessage(
                $"CNC-Bearbeitungen von {objectsWithOperations.Count} Objekt(en) entfernen?",
                "Bestätigung",
                Rhino.UI.ShowMessageButton.YesNo,
                Rhino.UI.ShowMessageIcon.Question);

            if (result != Rhino.UI.ShowMessageResult.Yes)
                return Result.Cancel;

            // Begin undo record
            var undoSerial = doc.BeginUndoRecord("CNC Remove Operation");

            try
            {
                foreach (var obj in objectsWithOperations)
                {
                    ToolpathVisualizer.RemoveToolpathGeometry(doc, obj);
                    CncOperationService.RemoveOperation(obj);

                    if (EdgeCurveHelper.IsExtractedEdgeCurve(obj))
                    {
                        // Delete the extracted edge curve entirely
                        doc.Objects.Delete(obj.Id, true);
                    }
                    else
                    {
                        CncOperationService.RestoreDefaultColor(obj);
                    }
                }

                doc.EndUndoRecord(undoSerial);
                doc.Views.Redraw();
                RhinoApp.WriteLine($"CNC-Bearbeitungen von {objectsWithOperations.Count} Objekt(en) entfernt.");
                return Result.Success;
            }
            catch
            {
                doc.EndUndoRecord(undoSerial);
                throw;
            }
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"[CNCRemoveOperation] Fehler: {ex.Message}");
            return Result.Failure;
        }
    }
}
