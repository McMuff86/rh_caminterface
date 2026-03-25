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
[assembly: Rhino.Commands.CommandClass(typeof(RhinoCNCExporter.Commands.AddClamexCommand))]

namespace RhinoCNCExporter.Commands;

/// <summary>
/// Command to add a CLAMEX P14 connector cut to a plate by clicking on its surface.
/// Creates a simplified CLAMEX-shaped boolean difference and tags the resulting faces.
/// </summary>
public sealed class AddClamexCommand : Command
{
    public override string EnglishName => "AddClamex";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        // Step 1: Get orientation from user
        var orientation = 0; // Default 0 degrees

        var getOption = new GetOption();
        getOption.SetCommandPrompt("Select CLAMEX orientation");
        
        int option0 = getOption.AddOption("0");
        int option90 = getOption.AddOption("90");
        int option180 = getOption.AddOption("180");
        int option270 = getOption.AddOption("270");

        getOption.SetDefaultString("0");

        var result = getOption.Get();
        if (result == GetResult.Option)
        {
            var selectedOption = getOption.Option();
            if (selectedOption.Index == option0) orientation = 0;
            else if (selectedOption.Index == option90) orientation = 90;
            else if (selectedOption.Index == option180) orientation = 180;
            else if (selectedOption.Index == option270) orientation = 270;
        }
        else if (result != GetResult.Nothing) // User can press Enter for default
        {
            return Result.Cancel;
        }

        // Step 2: Select face on a solid
        var getFace = new GetObject();
        getFace.SetCommandPrompt("Select surface for CLAMEX");
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
        getPoint.SetCommandPrompt("Click center point for CLAMEX");
        getPoint.Constrain(selectedFace, false);

        result = getPoint.Get();
        if (result != GetResult.Point) return Result.Cancel;

        var clamexCenter = getPoint.Point();

        try
        {
            // Step 4: Create CLAMEX cut
            var success = CreateClamexCut(doc, obj, brep, clamexCenter, selectedFace, orientation);
            if (success)
            {
                RhinoApp.WriteLine($"Added CLAMEX P14 at orientation {orientation}°");
                return Result.Success;
            }
            else
            {
                return Result.Failure;
            }
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"Error creating CLAMEX: {ex.Message}");
            return Result.Failure;
        }
    }

    /// <summary>
    /// Create a simplified CLAMEX P14 cut by boolean subtraction and tag the resulting faces.
    /// Uses a simplified rectangular shape approximating the CLAMEX form.
    /// </summary>
    private bool CreateClamexCut(RhinoDoc doc, RhinoObject plateObj, Brep plateBrep, Point3d clamexCenter, 
        BrepFace selectedFace, int orientation)
    {
        // Begin undo record
        doc.BeginUndoRecord("Add CLAMEX");

        try
        {
            // CLAMEX P14 approximate dimensions (simplified)
            double length = 37.0; // Main slot length
            double width = 9.5;   // Main slot width  
            double depth = 12.5;  // Cut depth

            // Calculate face coordinate system
            var surface = selectedFace.UnderlyingSurface();
            double u, v;
            surface.ClosestPoint(clamexCenter, out u, out v);
            
            var normal = selectedFace.NormalAt(u, v);
            normal.Unitize();

            // Get surface U and V directions
            var uDir = surface.UDirection(u, v);
            var vDir = surface.VDirection(u, v);
            uDir.Unitize();
            vDir.Unitize();

            // Apply orientation rotation
            var radians = orientation * Math.PI / 180.0;
            var cos = Math.Cos(radians);
            var sin = Math.Sin(radians);

            // Rotate the U direction by the orientation angle
            var rotatedU = uDir * cos + vDir * sin;
            var rotatedV = -uDir * sin + vDir * cos;

            // Create the CLAMEX cutting geometry (simplified as rectangular slot)
            var plane = new Plane(clamexCenter, rotatedU, rotatedV);
            var intervalU = new Interval(-length / 2.0, length / 2.0);
            var intervalV = new Interval(-width / 2.0, width / 2.0);
            var intervalZ = new Interval(0, depth);

            var box = new Box(plane, intervalU, intervalV, intervalZ);
            var clamexBrep = box.ToBrep();

            if (clamexBrep == null)
            {
                RhinoApp.WriteLine("Failed to create CLAMEX cutting geometry");
                return false;
            }

            // For more realistic CLAMEX shape, add rounded ends (simplified)
            // This could be enhanced later with the actual CLAMEX profile
            
            // Perform boolean subtraction
            var tolerance = doc.ModelAbsoluteTolerance;
            var result = Brep.CreateBooleanDifference(plateBrep, clamexBrep, tolerance);

            if (result == null || result.Length == 0)
            {
                RhinoApp.WriteLine("Boolean subtraction failed - CLAMEX may be outside the plate");
                clamexBrep.Dispose();
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
                clamexBrep.Dispose();
                return false;
            }

            // Find new faces created by the boolean operation
            var newFaceIndices = FaceTagger.FindNewFaces(plateBrep, newBrep, tolerance);

            // Replace the original object with the new one
            var newObjectId = doc.Objects.Replace(plateObj.Id, newBrep);
            var newObject = doc.Objects.FindId(newObjectId);

            if (newObject == null)
            {
                RhinoApp.WriteLine("Failed to replace object after boolean");
                return false;
            }

            // Tag the new CLAMEX faces with CNC_* attributes for MACRO operation
            var tags = new Dictionary<string, string>
            {
                ["Type"] = "MACRO",
                ["MacroName"] = "SawCut_Lamello",
                ["Orientation"] = orientation.ToString(),
                ["Side"] = "TOP",
                ["Description"] = $"CLAMEX_P14_{orientation}deg"
            };

            bool tagSuccess = FaceTagger.TagFaces(doc, newObjectId, newFaceIndices, tags);
            if (!tagSuccess)
            {
                RhinoApp.WriteLine("Warning: Failed to tag CLAMEX faces");
            }

            // Clean up
            clamexBrep.Dispose();
            foreach (var brep in result)
            {
                if (brep != newBrep)
                    brep?.Dispose();
            }

            doc.Views.Redraw();
            doc.EndUndoRecord();
            return true;
        }
        catch (Exception ex)
        {
            doc.EndUndoRecord();
            RhinoApp.WriteLine($"Error in CreateClamexCut: {ex.Message}");
            return false;
        }
    }
}