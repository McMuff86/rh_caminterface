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
            var profile = new ScmProfile();
            var toolLibrary = toolLibraryStore.LoadOrCreate(profile);

            // Show dialog for drill parameters
            var dialog = new DrillOperationDialog(toolLibraryStore, toolLibrary);
            var parameters = dialog.ShowModalOnTop();

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
                foreach (var objRef in go.Objects())
                {
                    if (objRef.Point() != null)
                        drillPoints.Add(objRef.Point().Location);
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
                        break;
                    }
                    else
                    {
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

            // Begin undo record
            var undoSerial = doc.BeginUndoRecord("CNC Add Drill");

            try
            {
                var diameter = parameters.TryGetValue(CncOperationSchema.CNC_DIAMETER, out var dObj) && dObj is double d
                    ? d
                    : 5.0;

                int count = 0;
                foreach (var point in drillPoints)
                {
                    var circle = new Circle(point, diameter / 2.0);
                    var circleId = doc.Objects.AddCircle(circle);

                    if (circleId == Guid.Empty) continue;

                    var circleObj = doc.Objects.FindId(circleId);
                    if (circleObj == null) continue;

                    CncOperationService.SetOperation(circleObj, CncOperationSchema.TYPE_DRILL, parameters);
                    CncOperationService.SetOperationColor(circleObj, CncOperationSchema.TYPE_DRILL);

                    var toolpathGeometry = ToolpathVisualizer.CreateDrillToolpath(point, diameter);
                    ToolpathVisualizer.AddToolpathToDocument(doc, circleObj, CncOperationSchema.TYPE_DRILL, toolpathGeometry);
                    count++;
                }

                doc.EndUndoRecord(undoSerial);
                doc.Views.Redraw();
                RhinoApp.WriteLine($"{count} Bohrung(en) erstellt.");
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
            RhinoApp.WriteLine($"[CNCAddDrill] Fehler: {ex.Message}");
            return Result.Failure;
        }
    }
}
