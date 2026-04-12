using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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

        if (!brep.IsSolid)
        {
            RhinoApp.WriteLine("Please select a face on a closed solid");
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
        var undoSerial = doc.BeginUndoRecord("Add Drill");
        Brep? cylinderBrep = null;
        Brep[]? booleanResult = null;
        Brep? adoptedBrep = null;
        bool replacedObject = false;

        try
        {
            if (!selectedFace.ClosestPoint(drillCenter, out var u, out var v))
            {
                RhinoApp.WriteLine("Failed to locate drill center on selected face");
                return false;
            }

            var normal = selectedFace.NormalAt(u, v);
            if (!normal.Unitize())
            {
                RhinoApp.WriteLine("Failed to determine drill direction");
                return false;
            }

            // Brep face normals usually point outward for closed solids.
            // For drilling we need the tool axis to point into the material.
            var drillDirection = plateBrep.SolidOrientation == BrepSolidOrientation.Inward
                ? normal
                : -normal;
            drillDirection.Unitize();

            // Create the drill body aligned to the picked face normal.
            var cylinder = new Cylinder(new Circle(new Plane(drillCenter, drillDirection), diameter / 2.0), depth);
            cylinderBrep = cylinder.ToBrep(true, true);

            if (cylinderBrep == null)
            {
                RhinoApp.WriteLine("Failed to create drill cylinder");
                return false;
            }

            // Perform boolean subtraction
            var tolerance = doc.ModelAbsoluteTolerance;
            booleanResult = Brep.CreateBooleanDifference(plateBrep, cylinderBrep, tolerance);

            if (booleanResult == null || booleanResult.Length == 0)
            {
                RhinoApp.WriteLine("Boolean subtraction failed");
                return false;
            }

            foreach (var brep in booleanResult)
            {
                if (brep != null && brep.IsValid)
                {
                    adoptedBrep = brep;
                    break;
                }
            }

            if (adoptedBrep == null)
            {
                RhinoApp.WriteLine("No valid result from boolean operation");
                return false;
            }

            var newFaceIndices = FaceTagger.FindNewFaces(plateBrep, adoptedBrep, tolerance);
            var drillFaceIndices = GetPreferredDrillFaceIndices(adoptedBrep, newFaceIndices, tolerance);

            if (!doc.Objects.Replace(plateObj.Id, adoptedBrep))
            {
                RhinoApp.WriteLine("Failed to replace object");
                return false;
            }

            replacedObject = true;
            var newObjectId = plateObj.Id;
            var newObject = doc.Objects.FindId(newObjectId);

            if (newObject == null)
            {
                RhinoApp.WriteLine("Failed to replace object after boolean");
                return false;
            }

            // Tag the new cylindrical faces with CNC_* attributes
            var featureId = Guid.NewGuid().ToString("N");
            var tags = new Dictionary<string, string>
            {
                ["Type"] = "DRILL",
                ["FeatureId"] = featureId,
                ["Diameter"] = diameter.ToString("0.###", CultureInfo.InvariantCulture),
                ["Depth"] = depth.ToString("0.###", CultureInfo.InvariantCulture),
                ["Side"] = "TOP",
                ["CenterX"] = drillCenter.X.ToString("0.###", CultureInfo.InvariantCulture),
                ["CenterY"] = drillCenter.Y.ToString("0.###", CultureInfo.InvariantCulture),
                ["CenterZ"] = drillCenter.Z.ToString("0.###", CultureInfo.InvariantCulture),
                ["Description"] = $"Drill_{diameter.ToString("0.###", CultureInfo.InvariantCulture)}x{depth.ToString("0.###", CultureInfo.InvariantCulture)}"
            };

            bool tagSuccess = FaceTagger.TagFaces(doc, newObjectId, drillFaceIndices, tags);
            if (!tagSuccess)
            {
                RhinoApp.WriteLine("Warning: Failed to tag drill faces");
            }

            doc.Views.Redraw();
            return true;
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"Error in CreateDrillHole: {ex.Message}");
            return false;
        }
        finally
        {
            cylinderBrep?.Dispose();

            if (booleanResult != null)
            {
                foreach (var brep in booleanResult)
                {
                    if (brep == null)
                        continue;

                    if (replacedObject && ReferenceEquals(brep, adoptedBrep))
                        continue;

                    brep.Dispose();
                }
            }

            doc.EndUndoRecord(undoSerial);
        }
    }

    private static IReadOnlyList<int> GetPreferredDrillFaceIndices(Brep brep, IEnumerable<int> candidateFaceIndices, double tolerance)
    {
        var candidates = candidateFaceIndices
            .Where(index => index >= 0 && index < brep.Faces.Count)
            .Distinct()
            .ToList();

        if (candidates.Count == 0)
            return candidates;

        var nonPlanarFaces = candidates
            .Where(index => !brep.Faces[index].IsPlanar(tolerance))
            .ToList();

        return nonPlanarFaces.Count > 0 ? nonPlanarFaces : candidates;
    }
}
