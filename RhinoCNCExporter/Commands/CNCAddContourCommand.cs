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

            // Apply operation to selected objects
            foreach (var obj in selectedObjects)
            {
                CncOperationService.SetOperation(obj, CncOperationSchema.TYPE_CONTOUR, parameters);
                CncOperationService.SetOperationColor(obj, CncOperationSchema.TYPE_CONTOUR);

                // Add text dot with operation summary
                AddOperationSummaryDot(doc, obj, CncOperationSchema.TYPE_CONTOUR, parameters);
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

    private void AddOperationSummaryDot(RhinoDoc doc, RhinoObject obj, string operationType, Dictionary<string, object> parameters)
    {
        try
        {
            // Get object center or first point for text placement
            var bbox = obj.Geometry.GetBoundingBox(true);
            var center = bbox.Center;

            // Create summary text
            var tool = parameters.GetValueOrDefault(CncOperationSchema.CNC_TOOL, "?");
            var depth = parameters.GetValueOrDefault(CncOperationSchema.CNC_DEPTH, "?");
            var strategy = parameters.GetValueOrDefault(CncOperationSchema.CNC_STRATEGY, CncOperationSchema.STRATEGY_BOTH);
            
            var summary = $"{operationType}\n{tool}\nZ{depth}\n{strategy}";

            // Create text dot
            var textDot = new TextDot(summary, center);
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
            RhinoApp.WriteLine($"[CNCAddContour] Warning: Could not add summary dot: {ex.Message}");
        }
    }
}