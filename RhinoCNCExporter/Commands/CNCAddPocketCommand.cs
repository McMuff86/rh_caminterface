using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using RhinoCNCExporter.Core.Blocks;
using RhinoCNCExporter.Core.Profiles;
using RhinoCNCExporter.Services;
using RhinoCNCExporter.UI;

namespace RhinoCNCExporter.Commands;

/// <summary>
/// Interactive command to add pocket machining operations to selected closed curves.
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
            go.GeometryFilter = ObjectType.Curve;
            go.GeometryAttributeFilter = Rhino.Input.Custom.GeometryAttributeFilter.ClosedCurve;
            go.GetMultiple(1, 0);

            if (go.CommandResult() != Result.Success)
                return go.CommandResult();

            var selectedObjects = new List<RhinoObject>();
            foreach (var objRef in go.Objects())
            {
                if (objRef.Object() is RhinoObject rhinoObj)
                {
                    // Verify curve is closed
                    if (rhinoObj.Geometry is Curve curve && curve.IsClosed)
                        selectedObjects.Add(rhinoObj);
                }
            }

            if (selectedObjects.Count == 0)
            {
                RhinoApp.WriteLine("Keine geschlossenen Kurven ausgewählt.");
                return Result.Nothing;
            }

            // Load tool library
            var toolLibraryStore = new ToolLibraryStore();
            var profile = new ScmProfile(); // Use default SCM profile
            var toolLibrary = toolLibraryStore.LoadOrCreate(profile);

            // Show dialog for operation parameters
            var dialog = new PocketOperationDialog(toolLibraryStore, toolLibrary);
            var parameters = dialog.ShowModalOnTop();

            if (parameters == null)
                return Result.Cancel;

            // Get tool diameter and stepover from parameters
            var toolDiameter = GetToolDiameter(parameters);
            var stepover = GetStepover(parameters);

            // Apply operation to selected objects
            foreach (var obj in selectedObjects)
            {
                CncOperationService.SetOperation(obj, CncOperationSchema.TYPE_POCKET, parameters);
                CncOperationService.SetOperationColor(obj, CncOperationSchema.TYPE_POCKET);

                // Generate and add toolpath visualization
                if (obj.Geometry is Curve curve && toolDiameter > 0)
                {
                    var toolpathGeometry = ToolpathVisualizer.CreatePocketToolpath(curve, toolDiameter, stepover);
                    ToolpathVisualizer.AddToolpathToDocument(doc, obj, CncOperationSchema.TYPE_POCKET, toolpathGeometry);
                }
            }

            doc.Views.Redraw();
            RhinoApp.WriteLine($"Taschenbearbeitung zu {selectedObjects.Count} Objekt(en) hinzugefügt.");

            return Result.Success;
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
        return 45.0; // Default 45% stepover
    }
}
