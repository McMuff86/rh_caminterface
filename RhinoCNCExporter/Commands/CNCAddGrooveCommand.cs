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
/// Interactive command to add groove (nut) operations to selected lines/curves.
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
            go.GeometryFilter = ObjectType.Curve;
            go.GetMultiple(1, 0);

            if (go.CommandResult() != Result.Success)
                return go.CommandResult();

            // Collect both the RhinoObject (for UserText) and the actual curve geometry
            var selections = new List<(RhinoObject obj, Curve curve)>();
            foreach (var objRef in go.Objects())
            {
                var rhinoObj = objRef.Object();
                if (rhinoObj == null) continue;

                var curve = objRef.Curve();
                if (curve == null && rhinoObj.Geometry is Curve directCurve)
                    curve = directCurve;

                if (curve != null)
                    selections.Add((rhinoObj, curve));
            }

            if (selections.Count == 0)
            {
                RhinoApp.WriteLine("Keine gültigen Kurven ausgewählt.");
                return Result.Nothing;
            }

            // Load tool library
            var toolLibraryStore = new ToolLibraryStore();
            var profile = new ScmProfile(); // Use default SCM profile
            var toolLibrary = toolLibraryStore.LoadOrCreate(profile);

            // Show dialog for groove parameters
            var dialog = new GrooveOperationDialog(toolLibraryStore, toolLibrary);
            var parameters = dialog.ShowModalOnTop();

            if (parameters == null)
                return Result.Cancel;

            // Get groove width (tool diameter for visualization)
            var grooveWidth = GetGrooveWidth(parameters);

            // Apply operation to selected objects
            foreach (var (obj, curve) in selections)
            {
                CncOperationService.SetOperation(obj, CncOperationSchema.TYPE_GROOVE, parameters);
                CncOperationService.SetOperationColor(obj, CncOperationSchema.TYPE_GROOVE);

                // Generate and add toolpath visualization (same as contour — offset curves show groove width)
                if (grooveWidth > 0)
                {
                    var toolpathGeometry = ToolpathVisualizer.CreateContourToolpath(curve, grooveWidth);
                    ToolpathVisualizer.AddToolpathToDocument(doc, obj, CncOperationSchema.TYPE_GROOVE, toolpathGeometry);
                }
            }

            doc.Views.Redraw();
            RhinoApp.WriteLine($"Nut-Bearbeitung zu {selections.Count} Objekt(en) hinzugefügt.");

            return Result.Success;
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"[CNCAddGroove] Fehler: {ex.Message}");
            return Result.Failure;
        }
    }

    private static double GetGrooveWidth(Dictionary<string, object> parameters)
    {
        // Try CNC_Width first (groove-specific), then CNC_Diameter
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
