using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using RhinoCNCExporter.Core.Blocks;
using RhinoCNCExporter.Core.Profiles;
using RhinoCNCExporter.Helpers;
using RhinoCNCExporter.Services;
using RhinoCNCExporter.UI;

namespace RhinoCNCExporter.Commands;

/// <summary>
/// Interactive command to add groove (nut) operations to selected lines/curves.
/// When a Brep edge is selected, it is extracted as a standalone curve.
/// </summary>
public sealed class CNCAddGrooveCommand : Command
{
    public override string EnglishName => "CNCAddGroove";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        try
        {
            // Get curve selection for groove path
            var go = new Rhino.Input.Custom.GetObject();
            go.SetCommandPrompt("Linien oder Kurven für Nut auswählen");
            go.GeometryFilter = ObjectType.Curve | ObjectType.EdgeFilter;
            go.GetMultiple(1, 0);

            if (go.CommandResult() != Result.Success)
                return go.CommandResult();

            // Resolve machine profile from document settings (default: xilog)
            var machineKey = doc.Strings.GetValue("CNC_MachineProfile") ?? "xilog";
            var profile = MachineProfileHelper.ResolveProfile(machineKey);

            // Load tool library
            var toolLibraryStore = new ToolLibraryStore();
            var toolLibrary = toolLibraryStore.LoadOrCreate(profile);

            // Get defaults for the machine profile
            var defaults = OperationDefaults.GetDefaults(CncOperationSchema.TYPE_GROOVE, machineKey);

            // Show dialog for groove parameters (pre-filled with defaults)
            var dialog = new GrooveOperationDialog(toolLibraryStore, toolLibrary, defaults);
            var parameters = dialog.ShowModalOnTop();

            if (parameters == null)
                return Result.Cancel;

            // Apply machine-aware defaults for any missing values
            OperationDefaults.ApplyDefaults(parameters, CncOperationSchema.TYPE_GROOVE, machineKey);

            var grooveWidth = GetGrooveWidth(parameters);

            // Begin undo record
            var undoSerial = doc.BeginUndoRecord("CNC Add Groove");

            try
            {
                int count = 0;
                foreach (var objRef in go.Objects())
                {
                    var color = CncOperationService.GetOperationColor(CncOperationSchema.TYPE_GROOVE);
                    if (!EdgeCurveHelper.TryResolveCurveTarget(doc, objRef, color, out var targetObj, out var curve) || targetObj == null)
                        continue;

                    if (!EdgeCurveHelper.IsExtractedEdgeCurve(targetObj))
                        CncOperationService.SetOperationColor(targetObj, CncOperationSchema.TYPE_GROOVE);

                    CncOperationService.SetOperation(targetObj, CncOperationSchema.TYPE_GROOVE, parameters);

                    if (grooveWidth > 0 && curve != null)
                    {
                        var toolpathGeometry = ToolpathVisualizer.CreateContourToolpath(curve, grooveWidth);
                        ToolpathVisualizer.AddToolpathToDocument(doc, targetObj, CncOperationSchema.TYPE_GROOVE, toolpathGeometry);
                    }

                    count++;
                }

                doc.EndUndoRecord(undoSerial);
                doc.Views.Redraw();
                RhinoApp.WriteLine($"Nut-Bearbeitung zu {count} Objekt(en) hinzugefügt.");
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
            RhinoApp.WriteLine($"[CNCAddGroove] Fehler: {ex.Message}");
            return Result.Failure;
        }
    }

    private static double GetGrooveWidth(Dictionary<string, object> parameters)
    {
        if (parameters.TryGetValue(CncOperationSchema.CNC_WIDTH, out var widthObj) && widthObj is double w)
            return w;
        if (parameters.TryGetValue(CncOperationSchema.CNC_WIDTH, out var widthStr) && double.TryParse(widthStr?.ToString(), out var parsed))
            return parsed;
        if (parameters.TryGetValue(CncOperationSchema.CNC_DIAMETER, out var diamObj) && diamObj is double d)
            return d;
        if (parameters.TryGetValue(CncOperationSchema.CNC_DIAMETER, out var diamStr) && double.TryParse(diamStr?.ToString(), out var parsed2))
            return parsed2;
        return 0;
    }
}
