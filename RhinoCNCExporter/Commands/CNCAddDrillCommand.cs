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
/// Interactive command to add drill operations by clicking points or selecting existing points.
/// </summary>
public sealed class CNCAddDrillCommand : Command
{
    public override string EnglishName => "CNCAddDrill";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        try
        {
            // Load tool library first to have parameters ready
            var toolLibraryStore = new ToolLibraryStore();
            var profile = new ScmProfile(); // Use default SCM profile
            var toolLibrary = toolLibraryStore.LoadOrCreate(profile);

            // Show dialog for drill parameters
            var dialog = new DrillOperationDialog(toolLibraryStore, toolLibrary);
            var parameters = dialog.ShowModal();

            if (parameters == null)
                return Result.Cancel;

            var drillPoints = new List<Point3d>();

            // Option 1: Try to select existing points first
            var go = new Rhino.Input.Custom.GetObject();
            go.SetCommandPrompt("Punkte für Bohrungen auswählen (Enter für manuelle Punkteingabe)");
            go.GeometryFilter = ObjectType.Point;
            go.AcceptNothing(true);
            go.GetMultiple(0, 0);

            if (go.CommandResult() == Result.Success && go.ObjectCount > 0)
            {
                // Use selected points
                foreach (var objRef in go.Objects())
                {
                    if (objRef.Point() != null)
                    {
                        drillPoints.Add(objRef.Point().Location);
                    }
                }

                RhinoApp.WriteLine($"{drillPoints.Count} vorhandene Punkte ausgewählt.");
            }
            else
            {
                // Option 2: Manual point picking
                var gp = new Rhino.Input.Custom.GetPoint();
                gp.SetCommandPrompt("Erste Bohrposition anklicken");
                
                while (true)
                {
                    var result = gp.Get();
                    
                    if (result == GetResult.Point)
                    {
                        drillPoints.Add(gp.Point());
                        RhinoApp.WriteLine($"Bohrung {drillPoints.Count} hinzugefügt. Enter für weitere, Esc zum Beenden.");
                        gp.SetCommandPrompt("Nächste Bohrposition anklicken (Enter zum Beenden)");
                        gp.AcceptNothing(true);
                    }
                    else if (result == GetResult.Nothing)
                    {
                        break; // User pressed Enter to finish
                    }
                    else
                    {
                        // User cancelled or error
                        if (drillPoints.Count == 0)
                            return Result.Cancel;
                        break;
                    }
                }
            }

            if (drillPoints.Count == 0)
            {
                RhinoApp.WriteLine("Keine Bohrpunkte definiert.");
                return Result.Nothing;
            }

            // Create drill objects at each point
            var createdObjects = new List<RhinoObject>();
            var diameter = (double)parameters[CncOperationSchema.CNC_DIAMETER];

            foreach (var point in drillPoints)
            {
                // Create a circle at the drill point to visualize the hole
                var circle = new Circle(point, diameter / 2.0);
                var circleId = doc.Objects.AddCircle(circle);

                if (circleId != Guid.Empty)
                {
                    var circleObj = doc.Objects.FindId(circleId);
                    if (circleObj != null)
                    {
                        // Apply drill operation
                        CncOperationService.SetOperation(circleObj, CncOperationSchema.TYPE_DRILL, parameters);
                        CncOperationService.SetOperationColor(circleObj, CncOperationSchema.TYPE_DRILL);
                        createdObjects.Add(circleObj);

                        // Add text dot with operation summary
                        AddOperationSummaryDot(doc, circleObj, CncOperationSchema.TYPE_DRILL, parameters);
                    }
                }
            }

            doc.Views.Redraw();
            RhinoApp.WriteLine($"{createdObjects.Count} Bohrung(en) erstellt.");

            return Result.Success;
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"[CNCAddDrill] Fehler: {ex.Message}");
            return Result.Failure;
        }
    }

    private void AddOperationSummaryDot(RhinoDoc doc, RhinoObject obj, string operationType, Dictionary<string, object> parameters)
    {
        try
        {
            // Get object center for text placement
            var bbox = obj.Geometry.GetBoundingBox(true);
            var center = bbox.Center;

            // Create summary text
            var diameter = parameters.GetValueOrDefault(CncOperationSchema.CNC_DIAMETER, "?");
            var depth = parameters.GetValueOrDefault(CncOperationSchema.CNC_DEPTH, "?");
            
            var summary = $"{operationType}\n⌀{diameter}\nZ{depth}";
            
            // Add peck info if enabled
            if (parameters.ContainsKey(CncOperationSchema.CNC_PECK) && 
                parameters[CncOperationSchema.CNC_PECK] is true)
            {
                var peckDepth = parameters.GetValueOrDefault(CncOperationSchema.CNC_PECK_DEPTH, "?");
                summary += $"\nPeck {peckDepth}";
            }

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
            RhinoApp.WriteLine($"[CNCAddDrill] Warning: Could not add summary dot: {ex.Message}");
        }
    }
}