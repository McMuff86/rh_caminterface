#if RHINO_AVAILABLE
using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Geometry;
using RhinoCNCExporter.Core.Models;
using RhinoCNCExporter.Services;
using Xunit;

namespace RhinoCNCExporter.Tests;

/// <summary>
/// Integration tests for the complete face-tagging workflow.
/// Tests the end-to-end process from tagging faces to reading machining operations.
/// </summary>
public class FaceTaggingIntegrationTests : IDisposable
{
    private readonly RhinoDoc _doc;

    public FaceTaggingIntegrationTests()
    {
        _doc = RhinoDoc.CreateHeadless("test");
    }

    public void Dispose()
    {
        _doc?.Dispose();
    }

    [Fact]
    public void EndToEnd_TagAndRead_DrillWorkflow()
    {
        // Simulate the workflow: create object → boolean operation → tag faces → read features
        
        // Arrange: Create a simple plate (box)
        var plate = CreateTestPlate();
        var plateId = _doc.Objects.Add(plate);
        
        // Simulate drilling: create a hole and new faces
        var plateWithHole = CreatePlateWithDrillHole(plate);
        var newPlateId = _doc.Objects.Replace(plateId, plateWithHole);
        var plateObj = _doc.Objects.FindId(newPlateId);

        // Simulate finding new faces (indices 6,7,8 for cylindrical faces)
        var newFaces = new[] { 6, 7, 8 }; // Mock indices for new cylindrical faces

        // Act: Tag the faces as a drill operation
        var drillTags = new Dictionary<string, string>
        {
            ["Type"] = "DRILL",
            ["Diameter"] = "6.0",
            ["Depth"] = "12.0",
            ["Side"] = "TOP",
            ["TechCode"] = "E010",
            ["Description"] = "DrillTest_6x12"
        };

        bool tagResult = FaceTagger.TagFaces(_doc, newPlateId, newFaces, drillTags);
        Assert.True(tagResult);

        // Act: Read the tagged features
        var features = FeatureReader.ReadTaggedFeatures(plateObj);

        // Assert: Should get back the drill machining
        Assert.Single(features);
        var drill = Assert.IsType<DrillMachining>(features[0]);
        Assert.Equal("DrillTest_6x12", drill.Name);
        Assert.Equal(6.0, drill.Diameter);
        Assert.Equal(12.0, drill.Depth);
        Assert.Equal(MachiningSide.Top, drill.Side);
        Assert.Equal("E010", drill.TechCode);
        Assert.Equal(MachiningSource.FaceTag, drill.Source);
    }

    [Fact]
    public void EndToEnd_TagAndRead_PocketWorkflow()
    {
        // Arrange
        var plate = CreateTestPlate();
        var plateId = _doc.Objects.Add(plate);
        
        // Simulate pocket creation
        var plateWithPocket = CreatePlateWithPocket(plate);
        var newPlateId = _doc.Objects.Replace(plateId, plateWithPocket);
        var plateObj = _doc.Objects.FindId(newPlateId);

        var newFaces = new[] { 6, 7, 8, 9, 10 }; // Mock pocket faces

        // Act: Tag the faces
        var pocketTags = new Dictionary<string, string>
        {
            ["Type"] = "POCKET",
            ["Depth"] = "8.0",
            ["ToolDia"] = "10.0",
            ["StepDown"] = "2.0",
            ["Description"] = "PocketTest"
        };

        bool tagResult = FaceTagger.TagFaces(_doc, newPlateId, newFaces, pocketTags);
        var features = FeatureReader.ReadTaggedFeatures(plateObj);

        // Assert
        Assert.True(tagResult);
        Assert.Single(features);
        var pocket = Assert.IsType<PocketMachining>(features[0]);
        Assert.Equal("PocketTest", pocket.Name);
        Assert.Equal(8.0, pocket.Depth);
        Assert.Equal(10.0, pocket.ToolDiameter);
        Assert.Equal(2.0, pocket.StepDown);
    }

    [Fact]
    public void TagFaces_MultipleMachiningTypes_AllReadCorrectly()
    {
        // Test multiple different machining types on the same object
        
        // Arrange
        var plate = CreateTestPlate();
        var plateId = _doc.Objects.Add(plate);
        var plateObj = _doc.Objects.FindId(plateId);

        // Tag multiple faces with different machining types
        var drillTags = new Dictionary<string, string>
        {
            ["Type"] = "DRILL",
            ["Diameter"] = "5.0",
            ["Depth"] = "10.0"
        };
        FaceTagger.TagFaces(_doc, plateId, new[] { 0 }, drillTags);

        var pocketTags = new Dictionary<string, string>
        {
            ["Type"] = "POCKET",
            ["Depth"] = "6.0",
            ["ToolDia"] = "8.0"
        };
        FaceTagger.TagFaces(_doc, plateId, new[] { 1 }, pocketTags);

        var grooveTags = new Dictionary<string, string>
        {
            ["Type"] = "GROOVE",
            ["Width"] = "4.0",
            ["Depth"] = "8.0"
        };
        FaceTagger.TagFaces(_doc, plateId, new[] { 2 }, grooveTags);

        var macroTags = new Dictionary<string, string>
        {
            ["Type"] = "MACRO",
            ["MacroName"] = "TestMacro",
            ["Orientation"] = "180"
        };
        FaceTagger.TagFaces(_doc, plateId, new[] { 3 }, macroTags);

        // Act
        var features = FeatureReader.ReadTaggedFeatures(plateObj);

        // Assert
        Assert.Equal(4, features.Count);
        
        var machiningTypes = features.Select(f => f.GetType()).ToHashSet();
        Assert.Contains(typeof(DrillMachining), machiningTypes);
        Assert.Contains(typeof(PocketMachining), machiningTypes);
        Assert.Contains(typeof(RoutingMachining), machiningTypes); // Groove becomes routing
        Assert.Contains(typeof(MacroMachining), machiningTypes);
        
        Assert.All(features, f => Assert.Equal(MachiningSource.FaceTag, f.Source));
    }

    [Fact]
    public void FaceTagging_PersistsThroughDocumentSave()
    {
        // This test would verify that tags survive document save/load cycles
        // For now, we'll just test that tags persist in object attributes
        
        // Arrange
        var plate = CreateTestPlate();
        var plateId = _doc.Objects.Add(plate);
        
        var tags = new Dictionary<string, string>
        {
            ["Type"] = "DRILL",
            ["Diameter"] = "8.0",
            ["Depth"] = "15.0",
            ["Description"] = "PersistenceTest"
        };

        // Act: Tag and then retrieve the object again
        FaceTagger.TagFaces(_doc, plateId, new[] { 0 }, tags);
        
        // Simulate document operations by getting fresh object reference
        var plateObj = _doc.Objects.FindId(plateId);
        var retrievedTags = FaceTagger.ReadTags(plateObj, 0);

        // Assert: Tags should be identical
        Assert.Equal(tags.Count, retrievedTags.Count);
        foreach (var kvp in tags)
        {
            Assert.Equal(kvp.Value, retrievedTags[kvp.Key]);
        }
    }

    [Fact]
    public void ClearTags_RemovesOnlySpecificFace()
    {
        // Test that clearing tags for one face doesn't affect other faces
        
        // Arrange
        var plate = CreateTestPlate();
        var plateId = _doc.Objects.Add(plate);
        var plateObj = _doc.Objects.FindId(plateId);

        var drillTags = new Dictionary<string, string> { ["Type"] = "DRILL", ["Diameter"] = "5.0" };
        var pocketTags = new Dictionary<string, string> { ["Type"] = "POCKET", ["Depth"] = "8.0" };

        FaceTagger.TagFaces(_doc, plateId, new[] { 0 }, drillTags);
        FaceTagger.TagFaces(_doc, plateId, new[] { 1 }, pocketTags);

        // Act: Clear tags for face 0 only
        FaceTagger.ClearTags(plateObj, 0);

        // Assert
        var face0Tags = FaceTagger.ReadTags(plateObj, 0);
        var face1Tags = FaceTagger.ReadTags(plateObj, 1);

        Assert.Empty(face0Tags); // Should be cleared
        Assert.NotEmpty(face1Tags); // Should remain
        Assert.Equal("POCKET", face1Tags["Type"]);
    }

    [Fact]
    public void GetTaggedFaceIndices_ReturnsCorrectIndices()
    {
        // Arrange
        var plate = CreateTestPlate();
        var plateId = _doc.Objects.Add(plate);
        var plateObj = _doc.Objects.FindId(plateId);

        // Tag non-consecutive faces
        var tags = new Dictionary<string, string> { ["Type"] = "DRILL" };
        FaceTagger.TagFaces(_doc, plateId, new[] { 1 }, tags);
        FaceTagger.TagFaces(_doc, plateId, new[] { 3 }, tags);
        FaceTagger.TagFaces(_doc, plateId, new[] { 5 }, tags);

        // Act
        var indices = FaceTagger.GetTaggedFaceIndices(plateObj);

        // Assert
        Assert.Equal(3, indices.Count);
        Assert.Contains(1, indices);
        Assert.Contains(3, indices);
        Assert.Contains(5, indices);
    }

    /// <summary>
    /// Create a test plate (box representing a wooden panel).
    /// </summary>
    private static Brep CreateTestPlate()
    {
        var box = new Box(
            new Plane(Point3d.Origin, Vector3d.ZAxis),
            new Interval(-50, 50),  // 100mm wide
            new Interval(-30, 30),  // 60mm deep  
            new Interval(0, 18)     // 18mm thick (typical panel)
        );
        return box.ToBrep();
    }

    /// <summary>
    /// Create a plate with a simulated drill hole.
    /// </summary>
    private static Brep CreatePlateWithDrillHole(Brep originalPlate)
    {
        // Create a small cylinder to subtract (drill hole)
        var holeCenter = new Point3d(10, 10, 18); // Top surface
        var cylinder = new Cylinder(new Circle(holeCenter, 3.0), 12.0); // 6mm diameter, 12mm deep
        var cylinderBrep = cylinder.ToBrep(true, true);
        
        var result = Brep.CreateBooleanDifference(originalPlate, cylinderBrep, 0.01);
        cylinderBrep.Dispose();
        
        return result?[0] ?? originalPlate;
    }

    /// <summary>
    /// Create a plate with a simulated pocket.
    /// </summary>
    private static Brep CreatePlateWithPocket(Brep originalPlate)
    {
        // Create a rectangular box to subtract (pocket)
        var pocketCenter = new Point3d(-10, -10, 18); // Top surface
        var pocketBox = new Box(
            new Plane(pocketCenter, Vector3d.ZAxis),
            new Interval(-15, 15),  // 30mm wide
            new Interval(-10, 10),  // 20mm deep
            new Interval(-8, 0)     // 8mm depth
        );
        var pocketBrep = pocketBox.ToBrep();
        
        var result = Brep.CreateBooleanDifference(originalPlate, pocketBrep, 0.01);
        pocketBrep.Dispose();
        
        return result?[0] ?? originalPlate;
    }
}
#endif // RHINO_AVAILABLE
