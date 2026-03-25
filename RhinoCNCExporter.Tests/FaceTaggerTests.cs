#if RHINO_AVAILABLE
using System;
using System.Collections.Generic;
using System.Linq;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using RhinoCNCExporter.Services;
using Xunit;

namespace RhinoCNCExporter.Tests;

/// <summary>
/// Tests for FaceTagger service functionality.
/// Note: These tests require a Rhino context, so they may not run in standard CI environments.
/// </summary>
public class FaceTaggerTests : IDisposable
{
    private readonly RhinoDoc _doc;

    public FaceTaggerTests()
    {
        // Create a test document for Rhino operations
        _doc = RhinoDoc.CreateHeadless("test");
    }

    public void Dispose()
    {
        _doc?.Dispose();
    }

    [Fact]
    public void TagFaces_ValidObject_SetsUserText()
    {
        // Arrange
        var box = CreateTestBox();
        var objectId = _doc.Objects.Add(box);
        var faceIndices = new[] { 0, 1 };
        var tags = new Dictionary<string, string>
        {
            ["Type"] = "DRILL",
            ["Diameter"] = "5.0",
            ["Depth"] = "13.0"
        };

        // Act
        var result = FaceTagger.TagFaces(_doc, objectId, faceIndices, tags);

        // Assert
        Assert.True(result);
        var obj = _doc.Objects.FindId(objectId);
        Assert.NotNull(obj);

        // Check that tags were set with correct prefixes
        var userStrings = obj.Attributes.GetUserStrings();
        Assert.Contains("CNC_Face_0_Type", userStrings.AllKeys);
        Assert.Contains("CNC_Face_1_Type", userStrings.AllKeys);
        Assert.Equal("DRILL", userStrings["CNC_Face_0_Type"]);
        Assert.Equal("5.0", userStrings["CNC_Face_0_Diameter"]);
    }

    [Fact]
    public void ReadTags_ValidObject_ReturnsCorrectTags()
    {
        // Arrange
        var box = CreateTestBox();
        var objectId = _doc.Objects.Add(box);
        var obj = _doc.Objects.FindId(objectId);

        // Set up test tags manually
        obj.Attributes.SetUserString("CNC_Face_2_Type", "POCKET");
        obj.Attributes.SetUserString("CNC_Face_2_Width", "25.0");
        obj.Attributes.SetUserString("CNC_Face_2_Length", "50.0");
        obj.Attributes.SetUserString("CNC_Face_2_Depth", "10.0");
        obj.CommitChanges();

        // Act
        var tags = FaceTagger.ReadTags(obj, 2);

        // Assert
        Assert.Equal(4, tags.Count);
        Assert.Equal("POCKET", tags["Type"]);
        Assert.Equal("25.0", tags["Width"]);
        Assert.Equal("50.0", tags["Length"]);
        Assert.Equal("10.0", tags["Depth"]);
    }

    [Fact]
    public void ReadTags_NonExistentFace_ReturnsEmptyDictionary()
    {
        // Arrange
        var box = CreateTestBox();
        var objectId = _doc.Objects.Add(box);
        var obj = _doc.Objects.FindId(objectId);

        // Act
        var tags = FaceTagger.ReadTags(obj, 99); // Non-existent face index

        // Assert
        Assert.Empty(tags);
    }

    [Fact]
    public void ClearTags_ValidObject_RemovesTags()
    {
        // Arrange
        var box = CreateTestBox();
        var objectId = _doc.Objects.Add(box);
        var obj = _doc.Objects.FindId(objectId);

        // Set up test tags
        obj.Attributes.SetUserString("CNC_Face_1_Type", "DRILL");
        obj.Attributes.SetUserString("CNC_Face_1_Diameter", "8.0");
        obj.Attributes.SetUserString("CNC_Face_2_Type", "POCKET");
        obj.CommitChanges();

        // Act
        var result = FaceTagger.ClearTags(obj, 1);

        // Assert
        Assert.True(result);
        var userStrings = obj.Attributes.GetUserStrings();
        
        // Face 1 tags should be gone
        Assert.DoesNotContain("CNC_Face_1_Type", userStrings.AllKeys);
        Assert.DoesNotContain("CNC_Face_1_Diameter", userStrings.AllKeys);
        
        // Face 2 tags should remain
        Assert.Contains("CNC_Face_2_Type", userStrings.AllKeys);
        Assert.Equal("POCKET", userStrings["CNC_Face_2_Type"]);
    }

    [Fact]
    public void GetTaggedFaceIndices_ValidObject_ReturnsCorrectIndices()
    {
        // Arrange
        var box = CreateTestBox();
        var objectId = _doc.Objects.Add(box);
        var obj = _doc.Objects.FindId(objectId);

        // Set up tags for faces 1, 3, and 5
        obj.Attributes.SetUserString("CNC_Face_1_Type", "DRILL");
        obj.Attributes.SetUserString("CNC_Face_3_Type", "POCKET");
        obj.Attributes.SetUserString("CNC_Face_5_Type", "GROOVE");
        obj.Attributes.SetUserString("SomeOtherKey", "NotRelevant"); // Should be ignored
        obj.CommitChanges();

        // Act
        var indices = FaceTagger.GetTaggedFaceIndices(obj);

        // Assert
        Assert.Equal(3, indices.Count);
        Assert.Contains(1, indices);
        Assert.Contains(3, indices);
        Assert.Contains(5, indices);
    }

    [Fact]
    public void FindNewFaces_SimpleDifference_IdentifiesNewFaces()
    {
        // Arrange
        var originalBox = CreateTestBox();
        
        // Create a smaller box to subtract (simulating a drill hole)
        var holeCenter = new Point3d(0, 0, 1); // Top of the box
        var holeRadius = 2.5;
        var holeDepth = 5.0;
        var cylinder = new Cylinder(new Circle(holeCenter, holeRadius), holeDepth);
        var cylinderBrep = cylinder.ToBrep(true, true);

        // Perform boolean difference
        var tolerance = 0.01;
        var result = Brep.CreateBooleanDifference(originalBox, cylinderBrep, tolerance);
        
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        
        var resultBrep = result[0];

        // Act
        var newFaces = FaceTagger.FindNewFaces(originalBox, resultBrep, tolerance);

        // Assert
        // Should find some new faces (the cylindrical faces of the hole)
        Assert.NotEmpty(newFaces);
        
        // Clean up
        cylinderBrep.Dispose();
        resultBrep.Dispose();
    }

    [Fact]
    public void FindNewFaces_NullInputs_ReturnsEmptyList()
    {
        // Act
        var result1 = FaceTagger.FindNewFaces(null, CreateTestBox());
        var result2 = FaceTagger.FindNewFaces(CreateTestBox(), null);
        var result3 = FaceTagger.FindNewFaces(null, null);

        // Assert
        Assert.Empty(result1);
        Assert.Empty(result2);
        Assert.Empty(result3);
    }

    [Fact]
    public void TagFaces_InvalidObjectId_ReturnsFalse()
    {
        // Act
        var result = FaceTagger.TagFaces(_doc, Guid.NewGuid(), new[] { 0 }, 
            new Dictionary<string, string> { ["Type"] = "DRILL" });

        // Assert
        Assert.False(result);
    }

    /// <summary>
    /// Create a simple box Brep for testing.
    /// </summary>
    private static Brep CreateTestBox()
    {
        var box = new Box(new Plane(Point3d.Origin, Vector3d.ZAxis),
            new Interval(-5, 5), new Interval(-5, 5), new Interval(0, 10));
        return box.ToBrep();
    }
}#endif // RHINO_AVAILABLE
