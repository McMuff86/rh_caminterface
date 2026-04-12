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
/// Interactive command to add contour machining operations to selected curves/edges.
/// When a Brep edge is selected, it is extracted as a standalone curve so that
/// UserText is stored independently of the parent Brep.
/// </summary>
public sealed class CNCAddContourCommand : Command
{
    public override string EnglishName => "CNCAddContour";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        try
        {
            // Get curve selection
            var go = new Rhino.Input.Custom.GetObject();
            go.SetCommandPrompt("Kurven oder Kanten für Konturbearbeitung auswählen");
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
            var defaults = OperationDefaults.GetDefaults(CncOperationSchema.TYPE_CONTOUR, machineKey);

            // Show dialog for operation parameters (pre-filled with defaults)
            var dialog = new ContourOperationDialog(toolLibraryStore, toolLibrary, defaults);
            var parameters = dialog.ShowModalOnTop();

            if (parameters == null)
                return Result.Cancel;

            // Apply machine-aware defaults for any missing values
            OperationDefaults.ApplyDefaults(parameters, CncOperationSchema.TYPE_CONTOUR, machineKey);

            var toolDiameter = GetToolDiameter(parameters);

            // Begin undo record
            var undoSerial = doc.BeginUndoRecord("CNC Add Contour");

            try
            {
                int count = 0;
                foreach (var objRef in go.Objects())
                {
                    var color = CncOperationService.GetOperationColor(CncOperationSchema.TYPE_CONTOUR);
                    if (!EdgeCurveHelper.TryResolveCurveTarget(doc, objRef, color, out var targetObj, out var curve) || targetObj == null)
                        continue;

                    if (!EdgeCurveHelper.IsExtractedEdgeCurve(targetObj))
                        CncOperationService.SetOperationColor(targetObj, CncOperationSchema.TYPE_CONTOUR);

                    CncOperationService.SetOperation(targetObj, CncOperationSchema.TYPE_CONTOUR, parameters);

                    if (toolDiameter > 0 && curve != null)
                    {
                        var toolpathGeometry = ToolpathVisualizer.CreateContourToolpath(curve, toolDiameter);
                        ToolpathVisualizer.AddToolpathToDocument(doc, targetObj, CncOperationSchema.TYPE_CONTOUR, toolpathGeometry);
                    }

                    count++;
                }

                doc.EndUndoRecord(undoSerial);
                doc.Views.Redraw();
                RhinoApp.WriteLine($"Konturbearbeitung zu {count} Objekt(en) hinzugefügt.");
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
            RhinoApp.WriteLine($"[CNCAddContour] Fehler: {ex.Message}");
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
}
