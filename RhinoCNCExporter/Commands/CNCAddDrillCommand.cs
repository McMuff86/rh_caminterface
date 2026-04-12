using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using System.Drawing;
using RhinoCNCExporter.Core.Blocks;
using RhinoCNCExporter.Core.Profiles;
using RhinoCNCExporter.Helpers;
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
            // Resolve machine profile from document settings (default: xilog)
            var machineKey = doc.Strings.GetValue("CNC_MachineProfile") ?? "xilog";
            var profile = MachineProfileHelper.ResolveProfile(machineKey);

            // Load tool library first to have parameters ready
            var toolLibraryStore = new ToolLibraryStore();
            var toolLibrary = toolLibraryStore.LoadOrCreate(profile);

            // Get defaults for the machine profile
            var defaults = OperationDefaults.GetDefaults(CncOperationSchema.TYPE_DRILL, machineKey);

            // Show dialog for drill parameters (pre-filled with defaults)
            var dialog = new DrillOperationDialog(toolLibraryStore, toolLibrary, defaults);
            var parameters = dialog.ShowModalOnTop();

            if (parameters == null)
                return Result.Cancel;

            // Apply machine-aware defaults for any missing values
            OperationDefaults.ApplyDefaults(parameters, CncOperationSchema.TYPE_DRILL, machineKey);

            var diameter = parameters.TryGetValue(CncOperationSchema.CNC_DIAMETER, out var dObj) && dObj is double d
                ? d
                : 5.0;

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
                gp.SetCommandPrompt("Bohrposition anklicken (Enter zum Fertigstellen)");
                gp.AcceptNothing(true);
                gp.DynamicDraw += (_, e) => DrawDrillPreviews(e, drillPoints, diameter);

                while (true)
                {
                    var result = gp.Get();

                    if (result == GetResult.Point)
                    {
                        drillPoints.Add(gp.Point());
                        RhinoApp.WriteLine($"Bohrung {drillPoints.Count} vorgemerkt. Enter zum Fertigstellen, nächster Klick für weitere Bohrung.");
                        gp.SetCommandPrompt("Nächste Bohrposition anklicken (Enter zum Beenden)");
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

                    var toolpathGeometry = ToolpathVisualizer.CreateDrillToolpath(circleObj.Geometry, diameter);
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

    private static void DrawDrillPreviews(Rhino.Input.Custom.GetPointDrawEventArgs e, IEnumerable<Point3d> drillPoints, double diameter)
    {
        var color = Color.Gold;
        foreach (var point in drillPoints)
        {
            DrawSingleDrillPreview(e, point, diameter, color, 1);
        }

        DrawSingleDrillPreview(e, e.CurrentPoint, diameter, color, 2);
    }

    private static void DrawSingleDrillPreview(Rhino.Input.Custom.GetPointDrawEventArgs e, Point3d center, double diameter, Color color, int thickness)
    {
        if (!center.IsValid || diameter <= 0)
            return;

        var radius = diameter / 2.0;
        if (radius <= 0.01)
            return;

        e.Display.DrawCircle(new Circle(center, radius), color, thickness);

        var crossSize = radius * 0.6;
        e.Display.DrawLine(
            new Point3d(center.X - crossSize, center.Y, center.Z),
            new Point3d(center.X + crossSize, center.Y, center.Z),
            color,
            thickness);
        e.Display.DrawLine(
            new Point3d(center.X, center.Y - crossSize, center.Z),
            new Point3d(center.X, center.Y + crossSize, center.Z),
            color,
            thickness);
    }
}
