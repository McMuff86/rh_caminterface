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
/// Command to add a rectangular pocket to a plate by clicking on its surface.
/// Creates a box boolean difference and tags the resulting faces.
/// </summary>
public sealed class AddPocketCommand : Command
{
    public override string EnglishName => "AddPocket";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        // Step 1: Get user input for pocket parameters
        var length = 50.0; // Default 50mm
        var width = 25.0;  // Default 25mm
        var depth = 10.0;  // Default 10mm

        var getNumber = new GetNumber();
        getNumber.SetCommandPrompt("Pocket length in mm");
        getNumber.SetDefaultNumber(length);
        getNumber.SetLowerLimit(1.0, false);
        getNumber.SetUpperLimit(500.0, false);

        var result = getNumber.Get();
        if (result != GetResult.Number) return Result.Cancel;
        length = getNumber.Number();

        getNumber.SetCommandPrompt("Pocket width in mm");
        getNumber.SetDefaultNumber(width);
        getNumber.SetLowerLimit(1.0, false);
        getNumber.SetUpperLimit(500.0, false);

        result = getNumber.Get();
        if (result != GetResult.Number) return Result.Cancel;
        width = getNumber.Number();

        getNumber.SetCommandPrompt("Pocket depth in mm");
        getNumber.SetDefaultNumber(depth);
        getNumber.SetLowerLimit(0.1, false);
        getNumber.SetUpperLimit(200.0, false);

        result = getNumber.Get();
        if (result != GetResult.Number) return Result.Cancel;
        depth = getNumber.Number();

        // Step 2: Select face on a solid
        var getFace = new GetObject();
        getFace.SetCommandPrompt("Select surface for pocket");
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
        getPoint.SetCommandPrompt("Click center point for pocket");
        getPoint.Constrain(selectedFace, false);

        result = getPoint.Get();
        if (result != GetResult.Point) return Result.Cancel;

        var pocketCenter = getPoint.Point();

        try
        {
            // Step 4: Create pocket
            var success = CreatePocket(doc, obj, brep, pocketCenter, selectedFace, length, width, depth);
            if (success)
            {
                RhinoApp.WriteLine($"Added pocket: {length}x{width}mm, depth {depth}mm");
                return Result.Success;
            }
            else
            {
                return Result.Failure;
            }
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"Error creating pocket: {ex.Message}");
            return Result.Failure;
        }
    }

    /// <summary>
    /// Create a rectangular pocket by boolean subtraction and tag the resulting faces.
    /// </summary>
    private bool CreatePocket(RhinoDoc doc, RhinoObject plateObj, Brep plateBrep, Point3d pocketCenter, 
        BrepFace selectedFace, double length, double width, double depth)
    {
        // Begin undo record
        var undoSerial = doc.BeginUndoRecord("Add Pocket");

        try
        {
            // Calculate face coordinate system
            var surface = selectedFace.UnderlyingSurface();
            double u, v;
            surface.ClosestPoint(pocketCenter, out u, out v);
            
            var normal = selectedFace.NormalAt(u, v);
            normal.Unitize();

            // Create a local coordinate system on the face
            // Use the surface U and V directions as X and Y axes
            surface.FrameAt(u, v, out var surfFrame);
            var uDir = surfFrame.XAxis; var vDir = surfFrame.YAxis;
            uDir.Unitize();
            vDir.Unitize();

            // Make sure we have a right-handed coordinate system
            var cross = Vector3d.CrossProduct(uDir, vDir);
            if (cross * normal < 0)
            {
                vDir = -vDir;
            }

            // Create the box for boolean subtraction
            var plane = new Plane(pocketCenter, uDir, vDir);
            var interval = new Interval(-length / 2.0, length / 2.0);
            var intervalV = new Interval(-width / 2.0, width / 2.0);
            var intervalZ = new Interval(0, depth);

            var box = new Box(plane, interval, intervalV, intervalZ);
            var boxBrep = box.ToBrep();

            if (boxBrep == null)
            {
                RhinoApp.WriteLine("Failed to create pocket box");
                return false;
            }

            // Perform boolean subtraction
            var tolerance = doc.ModelAbsoluteTolerance;
            var result = Brep.CreateBooleanDifference(plateBrep, boxBrep, tolerance);

            if (result == null || result.Length == 0)
            {
                RhinoApp.WriteLine("Boolean subtraction failed - pocket may be outside the plate");
                boxBrep.Dispose();
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
                boxBrep.Dispose();
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

            // Tag the new pocket faces with CNC_* attributes
            var tags = new Dictionary<string, string>
            {
                ["Type"] = "POCKET",
                ["Length"] = length.ToString("F1"),
                ["Width"] = width.ToString("F1"),
                ["Depth"] = depth.ToString("F1"),
                ["Side"] = "TOP",
                ["ToolDia"] = "8.0", // Default tool diameter
                ["Description"] = $"Pocket_{length:F1}x{width:F1}x{depth:F1}"
            };

            bool tagSuccess = FaceTagger.TagFaces(doc, newObjectId, newFaceIndices, tags);
            if (!tagSuccess)
            {
                RhinoApp.WriteLine("Warning: Failed to tag pocket faces");
            }

            // Clean up
            boxBrep.Dispose();
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
            RhinoApp.WriteLine($"Error in CreatePocket: {ex.Message}");
            return false;
        }
    }
}