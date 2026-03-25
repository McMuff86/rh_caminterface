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
/// Command to add a groove to a plate by clicking start and end points on its surface.
/// Creates an extruded groove shape via boolean difference and tags the resulting faces.
/// </summary>
public sealed class AddGrooveCommand : Command
{
    public override string EnglishName => "AddGroove";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        // Step 1: Get user input for groove parameters
        var width = 8.0;  // Default 8mm
        var depth = 10.0; // Default 10mm

        var getNumber = new GetNumber();
        getNumber.SetCommandPrompt("Groove width in mm");
        getNumber.SetDefaultNumber(width);
        getNumber.SetLowerLimit(0.5, false);
        getNumber.SetUpperLimit(50.0, false);

        var result = getNumber.Get();
        if (result != GetResult.Number) return Result.Cancel;
        width = getNumber.Number();

        getNumber.SetCommandPrompt("Groove depth in mm");
        getNumber.SetDefaultNumber(depth);
        getNumber.SetLowerLimit(0.1, false);
        getNumber.SetUpperLimit(200.0, false);

        result = getNumber.Get();
        if (result != GetResult.Number) return Result.Cancel;
        depth = getNumber.Number();

        // Step 2: Select face on a solid
        var getFace = new GetObject();
        getFace.SetCommandPrompt("Select surface for groove");
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

        // Step 3: Get start point on face
        var getStartPoint = new GetPoint();
        getStartPoint.SetCommandPrompt("Click start point for groove");
        getStartPoint.Constrain(selectedFace, false);

        result = getStartPoint.Get();
        if (result != GetResult.Point) return Result.Cancel;

        var startPoint = getStartPoint.Point();

        // Step 4: Get end point on face
        var getEndPoint = new GetPoint();
        getEndPoint.SetCommandPrompt("Click end point for groove");
        getEndPoint.Constrain(selectedFace, false);
        getEndPoint.SetBasePoint(startPoint, true);
        getEndPoint.DrawLineFromPoint(startPoint, false);

        result = getEndPoint.Get();
        if (result != GetResult.Point) return Result.Cancel;

        var endPoint = getEndPoint.Point();

        // Check if points are different
        if (startPoint.DistanceTo(endPoint) < doc.ModelAbsoluteTolerance * 10)
        {
            RhinoApp.WriteLine("Start and end points are too close");
            return Result.Failure;
        }

        try
        {
            // Step 5: Create groove
            var success = CreateGroove(doc, obj, brep, startPoint, endPoint, selectedFace, width, depth);
            if (success)
            {
                var length = startPoint.DistanceTo(endPoint);
                RhinoApp.WriteLine($"Added groove: {length:F1}mm long, {width:F1}mm wide, {depth:F1}mm deep");
                return Result.Success;
            }
            else
            {
                return Result.Failure;
            }
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"Error creating groove: {ex.Message}");
            return Result.Failure;
        }
    }

    /// <summary>
    /// Create a groove by extruding a rectangular profile along a line and performing boolean subtraction.
    /// </summary>
    private bool CreateGroove(RhinoDoc doc, RhinoObject plateObj, Brep plateBrep, Point3d startPoint, Point3d endPoint, 
        BrepFace selectedFace, double width, double depth)
    {
        // Begin undo record
        var undoSerial = doc.BeginUndoRecord("Add Groove");

        try
        {
            // Calculate face coordinate system at start point
            var surface = selectedFace.UnderlyingSurface();
            double u, v;
            surface.ClosestPoint(startPoint, out u, out v);
            
            var normal = selectedFace.NormalAt(u, v);
            normal.Unitize();

            // Calculate groove direction
            var grooveDirection = endPoint - startPoint;
            grooveDirection.Unitize();

            // Calculate perpendicular direction for groove width (perpendicular to groove direction and in face plane)
            var widthDirection = Vector3d.CrossProduct(normal, grooveDirection);
            widthDirection.Unitize();

            // Create rectangle profile centered on the groove line
            var halfWidth = width / 2.0;
            var corner1 = startPoint + widthDirection * halfWidth;
            var corner2 = startPoint - widthDirection * halfWidth;
            var corner3 = endPoint - widthDirection * halfWidth;
            var corner4 = endPoint + widthDirection * halfWidth;

            // Create closed polyline for the groove profile
            var profilePoints = new Point3d[] { corner1, corner2, corner3, corner4, corner1 };
            var profileCurve = new PolylineCurve(profilePoints);

            if (profileCurve == null)
            {
                RhinoApp.WriteLine("Failed to create groove profile curve");
                return false;
            }

            // Extrude the profile in the normal direction (into the material)
            var extrusionVector = normal * depth;
            var grooveBrep = Surface.CreateExtrusion(profileCurve, extrusionVector)?.ToBrep();

            profileCurve.Dispose();

            if (grooveBrep == null)
            {
                RhinoApp.WriteLine("Failed to create groove extrusion");
                return false;
            }

            // Make it a closed solid if possible
            grooveBrep.MergeCoplanarFaces(doc.ModelAbsoluteTolerance);
            if (grooveBrep.SolidOrientation != BrepSolidOrientation.Outward)
            {
                grooveBrep.Flip();
            }

            // Perform boolean subtraction
            var tolerance = doc.ModelAbsoluteTolerance;
            var result = Brep.CreateBooleanDifference(plateBrep, grooveBrep, tolerance);

            if (result == null || result.Length == 0)
            {
                RhinoApp.WriteLine("Boolean subtraction failed - groove may be outside the plate");
                grooveBrep.Dispose();
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
                grooveBrep.Dispose();
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

            // Calculate groove length
            var length = startPoint.DistanceTo(endPoint);

            // Tag the new groove faces with CNC_* attributes
            var tags = new Dictionary<string, string>
            {
                ["Type"] = "GROOVE",
                ["Width"] = width.ToString("F1"),
                ["Depth"] = depth.ToString("F1"),
                ["Length"] = length.ToString("F1"),
                ["Side"] = "TOP",
                ["ToolDia"] = width.ToString("F1"), // Tool diameter typically matches groove width
                ["Description"] = $"Groove_{length:F1}x{width:F1}x{depth:F1}"
            };

            bool tagSuccess = FaceTagger.TagFaces(doc, newObjectId, newFaceIndices, tags);
            if (!tagSuccess)
            {
                RhinoApp.WriteLine("Warning: Failed to tag groove faces");
            }

            // Clean up
            grooveBrep.Dispose();
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
            RhinoApp.WriteLine($"Error in CreateGroove: {ex.Message}");
            return false;
        }
    }
}