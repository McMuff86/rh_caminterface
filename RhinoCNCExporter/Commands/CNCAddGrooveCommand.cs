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

            var selectedObjects = new List<RhinoObject>();
            foreach (var objRef in go.Objects())
            {
                if (objRef.Object() is RhinoObject rhinoObj && rhinoObj.Geometry is Curve)
                    selectedObjects.Add(rhinoObj);
            }

            if (selectedObjects.Count == 0)
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
            var parameters = dialog.ShowModal();

            if (parameters == null)
                return Result.Cancel;

            // Apply operation to selected objects
            foreach (var obj in selectedObjects)
            {
                CncOperationService.SetOperation(obj, CncOperationSchema.TYPE_GROOVE, parameters);
                CncOperationService.SetOperationColor(obj, CncOperationSchema.TYPE_GROOVE);

                // Add text dot with operation summary
                AddOperationSummaryDot(doc, obj, CncOperationSchema.TYPE_GROOVE, parameters);
            }

            doc.Views.Redraw();
            RhinoApp.WriteLine($"Nut-Bearbeitung zu {selectedObjects.Count} Objekt(en) hinzugefügt.");

            return Result.Success;
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"[CNCAddGroove] Fehler: {ex.Message}");
            return Result.Failure;
        }
    }

    private void AddOperationSummaryDot(RhinoDoc doc, RhinoObject obj, string operationType, Dictionary<string, object> parameters)
    {
        try
        {
            // Get curve midpoint or start for text placement
            Point3d textPoint;
            if (obj.Geometry is Curve curve)
            {
                textPoint = curve.PointAtNormalizedLength(0.5); // Midpoint
            }
            else
            {
                var bbox = obj.Geometry.GetBoundingBox(true);
                textPoint = bbox.Center;
            }

            // Create summary text
            var tool = parameters.GetValueOrDefault(CncOperationSchema.CNC_TOOL, "?");
            var width = parameters.GetValueOrDefault(CncOperationSchema.CNC_WIDTH, "?");
            var depth = parameters.GetValueOrDefault(CncOperationSchema.CNC_DEPTH, "?");
            
            var summary = $"{operationType}\n{tool}\nB{width}\nZ{depth}";

            // Create text dot
            var textDot = new TextDot(summary, textPoint);
            var dotId = doc.Objects.AddTextDot(textDot);
            
            if (dotId != Guid.Empty)
            {
                // Put text dot on same layer as the operation object
                var dotObj = doc.Objects.FindId(dotId);
                if (dotObj != null && obj.Attributes.LayerIndex >= 0)
                {
                    dotObj.Attributes.LayerIndex = obj.Attributes.LayerIndex;
                    dotObj.CommitChanges();
                }
            }
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"[CNCAddGroove] Warning: Could not add summary dot: {ex.Message}");
        }
    }
}