using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using RhinoCNCExporter.Core.Blocks;
using RhinoCNCExporter.Core.Profiles;
using RhinoCNCExporter.Services;
using RhinoCNCExporter.UI;

namespace RhinoCNCExporter.Commands;

/// <summary>
/// Interactive command to add pocket machining operations to selected closed curves.
/// When a Brep edge is selected, it is extracted as a standalone curve.
/// </summary>
public sealed class CNCAddPocketCommand : Command
{
    public override string EnglishName => "CNCAddPocket";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        try
        {
            // Get closed curve selection
            var go = new Rhino.Input.Custom.GetObject();
            go.SetCommandPrompt("Geschlossene Kurven für Taschenbearbeitung auswählen");
            go.GeometryFilter = ObjectType.Curve | ObjectType.EdgeFilter;
            go.GetMultiple(1, 0);

            if (go.CommandResult() != Result.Success)
                return go.CommandResult();

            // Load tool library
            var toolLibraryStore = new ToolLibraryStore();
            var profile = new ScmProfile();
            var toolLibrary = toolLibraryStore.LoadOrCreate(profile);

            // Show dialog for operation parameters
            var dialog = new PocketOperationDialog(toolLibraryStore, toolLibrary);
            var parameters = dialog.ShowModalOnTop();

            if (parameters == null)
                return Result.Cancel;

            var toolDiameter = GetToolDiameter(parameters);
            var stepover = GetStepover(parameters);

            // Begin undo record
            var undoSerial = doc.BeginUndoRecord("CNC Add Pocket");

            try
            {
                int count = 0;
                foreach (var objRef in go.Objects())
                {
                    RhinoObject targetObj;
                    Curve? curve;

                    if (EdgeCurveHelper.IsBrepEdge(objRef))
                    {
                        var color = CncOperationService.GetOperationColor(CncOperationSchema.TYPE_POCKET);
                        var extracted = EdgeCurveHelper.ExtractEdgeCurve(doc, objRef, color);
                        if (extracted == null) continue;

                        targetObj = extracted;
                        curve = extracted.Geometry as Curve;
                    }
                    else
                    {
                        var rhinoObj = objRef.Object();
                        if (rhinoObj == null) continue;

                        curve = objRef.Curve() ?? rhinoObj.Geometry as Curve;
                        if (curve == null) continue;

                        targetObj = rhinoObj;
                        CncOperationService.SetOperationColor(targetObj, CncOperationSchema.TYPE_POCKET);
                    }

                    // Verify curve is closed for pocket operations
                    if (curve != null && !curve.IsClosed)
                    {
                        RhinoApp.WriteLine($"Kurve übersprungen (nicht geschlossen): {targetObj.Name ?? targetObj.Id.ToString()[..8]}");
                        continue;
                    }

                    CncOperationService.SetOperation(targetObj, CncOperationSchema.TYPE_POCKET, parameters);

                    if (toolDiameter > 0 && curve != null)
                    {
                        var toolpathGeometry = ToolpathVisualizer.CreatePocketToolpath(curve, toolDiameter, stepover);
                        ToolpathVisualizer.AddToolpathToDocument(doc, targetObj, CncOperationSchema.TYPE_POCKET, toolpathGeometry);
                    }

                    count++;
                }

                doc.EndUndoRecord(undoSerial);
                doc.Views.Redraw();
                RhinoApp.WriteLine($"Taschenbearbeitung zu {count} Objekt(en) hinzugefügt.");
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
            RhinoApp.WriteLine($"[CNCAddPocket] Fehler: {ex.Message}");
            return Result.Failure;
        }
    }

    private static double GetToolDiameter(Dictionary<string, object> parameters)
    {
        if (parameters.TryGetValue(CncOperationSchema.CNC_DIAMETER, out var diamObj) && diamObj is double d)
            return d;
        if (parameters.TryGetValue(CncOperationSchema.CNC_DIAMETER, out var diamStr) && double.TryParse(diamStr?.ToString(), out var parsed))
            return parsed;
        return 0;
    }

    private static double GetStepover(Dictionary<string, object> parameters)
    {
        if (parameters.TryGetValue(CncOperationSchema.CNC_STEPOVER, out var stepObj) && stepObj is double s)
            return s;
        if (parameters.TryGetValue(CncOperationSchema.CNC_STEPOVER, out var stepStr) && double.TryParse(stepStr?.ToString(), out var parsed))
            return parsed;
        return 45.0;
    }
}
