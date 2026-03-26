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
/// Interactive command to add contour machining operations to selected curves/edges.
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

            var selectedObjects = new List<RhinoObject>();
            foreach (var objRef in go.Objects())
            {
                if (objRef.Object() is RhinoObject rhinoObj)
                    selectedObjects.Add(rhinoObj);
            }

            if (selectedObjects.Count == 0)
            {
                RhinoApp.WriteLine("Keine gültigen Objekte ausgewählt.");
                return Result.Nothing;
            }

            // Load tool library
            var toolLibraryStore = new ToolLibraryStore();
            var profile = new ScmProfile(); // Use default SCM profile
            var toolLibrary = toolLibraryStore.LoadOrCreate(profile);

            // Show dialog for operation parameters
            var dialog = new ContourOperationDialog(toolLibraryStore, toolLibrary);
            var parameters = dialog.ShowModalOnTop();

            if (parameters == null)
                return Result.Cancel;

            // Get tool diameter from parameters
            var toolDiameter = GetToolDiameter(parameters);

            // Apply operation to selected objects
            foreach (var obj in selectedObjects)
            {
                CncOperationService.SetOperation(obj, CncOperationSchema.TYPE_CONTOUR, parameters);
                CncOperationService.SetOperationColor(obj, CncOperationSchema.TYPE_CONTOUR);

                // Generate and add toolpath visualization
                if (obj.Geometry is Curve curve && toolDiameter > 0)
                {
                    var toolpathGeometry = ToolpathVisualizer.CreateContourToolpath(curve, toolDiameter);
                    ToolpathVisualizer.AddToolpathToDocument(doc, obj, CncOperationSchema.TYPE_CONTOUR, toolpathGeometry);
                }
            }

            doc.Views.Redraw();
            RhinoApp.WriteLine($"Konturbearbeitung zu {selectedObjects.Count} Objekt(en) hinzugefügt.");

            return Result.Success;
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
        // Try parsing from string
        if (parameters.TryGetValue(CncOperationSchema.CNC_DIAMETER, out var diamStr) && double.TryParse(diamStr?.ToString(), out var parsed))
            return parsed;
        return 0;
    }
}
