using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;
using RhinoCNCExporter.Services;

// Register the command

namespace RhinoCNCExporter.Commands;

/// <summary>
/// Command to add a drill hole to a plate by clicking on its surface.
/// Creates a cylindrical boolean difference and tags the resulting faces.
/// </summary>
public sealed class AddDrillCommand : Command
{
    public override string EnglishName => "AddDrill";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        // Step 1: Get user input for drill parameters
        var diameter = 5.0; // Default 5mm
        var depth = 13.0; // Default 13mm

        var getNumber = new GetNumber();
        getNumber.SetCommandPrompt("Drill diameter in mm");
        getNumber.SetDefaultNumber(diameter);
        getNumber.SetLowerLimit(0.1, false);
        getNumber.SetUpperLimit(100.0, false);

        var result = getNumber.Get();
        if (result != GetResult.Number) return Result.Cancel;
        diameter = getNumber.Number();

        getNumber.SetCommandPrompt("Drill depth in mm");
        getNumber.SetDefaultNumber(depth);
        getNumber.SetLowerLimit(0.1, false);
        getNumber.SetUpperLimit(200.0, false);

        result = getNumber.Get();
        if (result != GetResult.Number) return Result.Cancel;
        depth = getNumber.Number();

        // Step 2: Select face on a solid
        var getFace = new GetObject();
        getFace.SetCommandPrompt("Select surface to drill");
        getFace.GeometryFilter = ObjectType.Surface;
        getFace.SubObjectSelect = true;
        getFace.EnablePreSelect(false, true);

        result = getFace.Get();
        if (result != GetResult.Object) return Result.Cancel;

        var objRef = getFace.Object(0);
        var obj = objRef.Object();
        if (obj?.Geometry is not Brep brep)
        {
            RhinoApp.WriteLine("Selected object is not a Brep");
            return Result.Failure;
        }

        // Get the selected face
        var faceComponent = objRef.GeometryComponentIndex;
        if (faceComponent.ComponentIndexType != ComponentIndexType.BrepFace)
        {
            RhinoApp.WriteLine("Please select a face, not an edge or vertex");
            return Result.Failure;
        }

        int faceIndex = faceComponent.Index;
        var selectedFace = brep.Faces[faceIndex];

        // Step 3: Get click point on face
        var getPoint = new GetPoint();
        getPoint.SetCommandPrompt("Click point for drill center");
        getPoint.Constrain(selectedFace, false);

        result = getPoint.Get();
        if (result != GetResult.Point) return Result.Cancel;

        var drillCenter = getPoint.Point();

        try
        {
            // Step 4: Create cylinder for boolean subtraction
            var success = CreateDrillHole(doc, obj, brep, drillCenter, selectedFace, diameter, depth);
            if (success)
            {
                RhinoApp.WriteLine($"Added drill: Ø{diameter}mm, depth {depth}mm");
                return Result.Success;
            }
            else
            {
                return Result.Failure;
            }
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"Error creating drill: {ex.Message}");
            return Result.Failure;
        }
    }

    /// <summary>
    /// Create a drill hole by boolean subtraction and tag the resulting cylindrical faces.
    /// </summary>
    private bool CreateDrillHole(RhinoDoc doc, RhinoObject plateObj, Brep plateBrep, Point3d drillCenter, 
        BrepFace selectedFace, double diameter, double depth)
    {
        // Begin undo record
        var undoSerial = doc.BeginUndoRecord("Add Drill");

        try
        {
            // Calculate drill direction (surface normal, pointing into material)
            var surface = selectedFace.UnderlyingSurface();
            double u, v;
            surface.ClosestPoint(drillCenter, out u, out v);
            var normal = selectedFace.NormalAt(u, v);

            // Make sure normal points into the material (typically negative Z for top faces)
            // For safety, we'll use the normal as-is from the face
            var drillDirection = normal;
            drillDirection.Unitize();

            // Create cylinder
            var drillAxis = new Line(drillCenter, drillCenter + drillDirection * depth);
            var cylinder = new Cylinder(new Circle(drillCenter, diameter / 2.0), depth);
            var cylinderBrep = cylinder.ToBrep(true, true);

            if (cylinderBrep == null)
            {
                RhinoApp.WriteLine("Failed to create drill cylinder");
                return false;
            }

            // Perform boolean subtraction
            var tolerance = doc.ModelAbsoluteTolerance;
            var result = Brep.CreateBooleanDifference(plateBrep, cylinderBrep, tolerance);

            if (result == null || result.Length == 0)
            {
                RhinoApp.WriteLine("Boolean subtraction failed");
                cylinderBrep.Dispose();
                return false;
            }

            // Find the first valid result
            Brep newBrep = null;
            foreach (var brep in result)
            {
                if (brep != null && brep.IsValid)
                {
                    newBrep = brep;
                    break;
                }
            }

            if (newBrep == null)
            {
                RhinoApp.WriteLine("No valid result from boolean operation");
                foreach (var brep in result)
                    brep?.Dispose();
                cylinderBrep.Dispose();
                return false;
            }

            // Find new faces created by the boolean operation
            var newFaceIndices = FaceTagger.FindNewFaces(plateBrep, newBrep, tolerance);

            // Replace the original object with the new one
            if (!doc.Objects.Replace(plateObj.Id, newBrep))
            {
                RhinoApp.WriteLine("Failed to replace object");
                return false;
            }
            var newObjectId = plateObj.Id;
            var newObject = doc.Objects.FindId(newObjectId);

            if (newObject == null)
            {
                RhinoApp.WriteLine("Failed to replace object after boolean");
                return false;
            }

            // Tag the new cylindrical faces with CNC_* attributes
            var tags = new Dictionary<string, string>
            {
                ["Type"] = "DRILL",
                ["Diameter"] = diameter.ToString("F1"),
                ["Depth"] = depth.ToString("F1"),
                ["Side"] = "TOP",
                ["Description"] = $"Drill_{diameter:F1}x{depth:F1}"
            };

            bool tagSuccess = FaceTagger.TagFaces(doc, newObjectId, newFaceIndices, tags);
            if (!tagSuccess)
            {
                RhinoApp.WriteLine("Warning: Failed to tag drill faces");
            }

            // Clean up
            cylinderBrep.Dispose();
            foreach (var brep in result)
            {
                if (brep != newBrep)
                    brep?.Dispose();
            }

            doc.Views.Redraw();
            doc.EndUndoRecord(undoSerial);
            return true;
        }
        catch (Exception ex)
        {
            doc.EndUndoRecord(undoSerial);
            RhinoApp.WriteLine($"Error in CreateDrillHole: {ex.Message}");
            return false;
        }
    }
}