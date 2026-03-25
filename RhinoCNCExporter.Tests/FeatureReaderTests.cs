#if RHINO_AVAILABLE
using System;
using System.Collections.Generic;
using System.Linq;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using RhinoCNCExporter.Core.Models;
using RhinoCNCExporter.Services;
using Xunit;

namespace RhinoCNCExporter.Tests;

/// <summary>
/// Tests for FeatureReader service functionality.
/// </summary>
public class FeatureReaderTests : IDisposable
{
    private readonly RhinoDoc _doc;

    public FeatureReaderTests()
    {
        _doc = RhinoDoc.CreateHeadless("test");
    }

    public void Dispose()
    {
        _doc?.Dispose();
    }

    [Fact]
    public void ReadTaggedFeatures_DrillTags_CreatesCorrectMachining()
    {
        // Arrange
        var box = CreateTestBox();
        var objectId = _doc.Objects.Add(box);
        var obj = _doc.Objects.FindId(objectId);

        // Set up drill tags on face 0
        obj.Attributes.SetUserString("CNC_Face_0_Type", "DRILL");
        obj.Attributes.SetUserString("CNC_Face_0_Diameter", "8.0");
        obj.Attributes.SetUserString("CNC_Face_0_Depth", "15.0");
        obj.Attributes.SetUserString("CNC_Face_0_Side", "TOP");
        obj.Attributes.SetUserString("CNC_Face_0_TechCode", "E010");
        obj.Attributes.SetUserString("CNC_Face_0_Description", "TestDrill");
        obj.CommitChanges();

        // Act
        var machinings = FeatureReader.ReadTaggedFeatures(obj);

        // Assert
        Assert.Single(machinings);
        var drill = Assert.IsType<DrillMachining>(machinings[0]);
        Assert.Equal("TestDrill", drill.Name);
        Assert.Equal(MachiningSide.Top, drill.Side);
        Assert.Equal("E010", drill.TechCode);
        Assert.Equal(MachiningSource.FaceTag, drill.Source);
        Assert.Equal(8.0, drill.Diameter);
        Assert.Equal(15.0, drill.Depth);
        // X, Y should be calculated from face center (we can't easily test exact values)
        Assert.True(drill.X != 0 || drill.Y != 0); // Should have some position
    }

    [Fact]
    public void ReadTaggedFeatures_DrillPatternTags_CreatesCorrectMachining()
    {
        // Arrange
        var box = CreateTestBox();
        var objectId = _doc.Objects.Add(box);
        var obj = _doc.Objects.FindId(objectId);

        obj.Attributes.SetUserString("CNC_Face_1_Type", "DRILLPATTERN");
        obj.Attributes.SetUserString("CNC_Face_1_Diameter", "5.0");
        obj.Attributes.SetUserString("CNC_Face_1_Depth", "13.0");
        obj.Attributes.SetUserString("CNC_Face_1_PatternX", "1");
        obj.Attributes.SetUserString("CNC_Face_1_PatternY", "5");
        obj.Attributes.SetUserString("CNC_Face_1_SpacingX", "0");
        obj.Attributes.SetUserString("CNC_Face_1_SpacingY", "32");
        obj.Attributes.SetUserString("CNC_Face_1_Side", "LEFT");
        obj.CommitChanges();

        // Act
        var machinings = FeatureReader.ReadTaggedFeatures(obj);

        // Assert
        Assert.Single(machinings);
        var pattern = Assert.IsType<DrillPatternMachining>(machinings[0]);
        Assert.Equal(MachiningSide.Left, pattern.Side);
        Assert.Equal(MachiningSource.FaceTag, pattern.Source);
        Assert.Equal(5.0, pattern.Diameter);
        Assert.Equal(13.0, pattern.Depth);
        Assert.Equal(1, pattern.CountX);
        Assert.Equal(5, pattern.CountY);
        Assert.Equal(0.0, pattern.SpacingX);
        Assert.Equal(32.0, pattern.SpacingY);
    }

    [Fact]
    public void ReadTaggedFeatures_PocketTags_CreatesCorrectMachining()
    {
        // Arrange
        var box = CreateTestBox();
        var objectId = _doc.Objects.Add(box);
        var obj = _doc.Objects.FindId(objectId);

        obj.Attributes.SetUserString("CNC_Face_2_Type", "POCKET");
        obj.Attributes.SetUserString("CNC_Face_2_Depth", "12.0");
        obj.Attributes.SetUserString("CNC_Face_2_ToolDia", "10.0");
        obj.Attributes.SetUserString("CNC_Face_2_StepDown", "3.0");
        obj.Attributes.SetUserString("CNC_Face_2_Side", "BOTTOM");
        obj.CommitChanges();

        // Act
        var machinings = FeatureReader.ReadTaggedFeatures(obj);

        // Assert
        Assert.Single(machinings);
        var pocket = Assert.IsType<PocketMachining>(machinings[0]);
        Assert.Equal(MachiningSide.Bottom, pocket.Side);
        Assert.Equal(MachiningSource.FaceTag, pocket.Source);
        Assert.Equal(12.0, pocket.Depth);
        Assert.Equal(10.0, pocket.ToolDiameter);
        Assert.Equal(3.0, pocket.StepDown);
        Assert.NotEmpty(pocket.Loops); // Should have extracted some boundary
    }

    [Fact]
    public void ReadTaggedFeatures_GrooveTags_CreatesCorrectMachining()
    {
        // Arrange
        var box = CreateTestBox();
        var objectId = _doc.Objects.Add(box);
        var obj = _doc.Objects.FindId(objectId);

        obj.Attributes.SetUserString("CNC_Face_3_Type", "GROOVE");
        obj.Attributes.SetUserString("CNC_Face_3_Width", "8.0");
        obj.Attributes.SetUserString("CNC_Face_3_Depth", "6.0");
        obj.Attributes.SetUserString("CNC_Face_3_StepDown", "2.0");
        obj.CommitChanges();

        // Act
        var machinings = FeatureReader.ReadTaggedFeatures(obj);

        // Assert
        Assert.Single(machinings);
        var groove = Assert.IsType<RoutingMachining>(machinings[0]);
        Assert.Equal(MachiningSource.FaceTag, groove.Source);
        Assert.Equal(6.0, groove.Depth);
        Assert.Equal(8.0, groove.ToolDiameter); // Width maps to tool diameter
        Assert.Equal(2.0, groove.StepDown);
        Assert.False(groove.IsClosed); // Grooves are open paths
        Assert.NotEmpty(groove.Points); // Should have extracted some path
    }

    [Fact]
    public void ReadTaggedFeatures_MacroTags_CreatesCorrectMachining()
    {
        // Arrange
        var box = CreateTestBox();
        var objectId = _doc.Objects.Add(box);
        var obj = _doc.Objects.FindId(objectId);

        obj.Attributes.SetUserString("CNC_Face_4_Type", "MACRO");
        obj.Attributes.SetUserString("CNC_Face_4_MacroName", "SawCut_Lamello");
        obj.Attributes.SetUserString("CNC_Face_4_Orientation", "90");
        obj.Attributes.SetUserString("CNC_Face_4_MacroParams", "param1,param2,param3");
        obj.Attributes.SetUserString("CNC_Face_4_TechCode", "E015");
        obj.CommitChanges();

        // Act
        var machinings = FeatureReader.ReadTaggedFeatures(obj);

        // Assert
        Assert.Single(machinings);
        var macro = Assert.IsType<MacroMachining>(machinings[0]);
        Assert.Equal("SawCut_Lamello", macro.MacroName);
        Assert.Equal("E015", macro.TechCode);
        Assert.Equal(MachiningSource.FaceTag, macro.Source);
        Assert.Equal(4, macro.Parameters.Count);
        Assert.Equal("90", macro.Parameters[0]); // Orientation added first
        Assert.Equal("param1", macro.Parameters[1]);
        Assert.Equal("param2", macro.Parameters[2]);
        Assert.Equal("param3", macro.Parameters[3]);
    }

    [Fact]
    public void ReadTaggedFeatures_MultipleFaces_ReturnsAllMachinings()
    {
        // Arrange
        var box = CreateTestBox();
        var objectId = _doc.Objects.Add(box);
        var obj = _doc.Objects.FindId(objectId);

        // Set up multiple different features
        obj.Attributes.SetUserString("CNC_Face_0_Type", "DRILL");
        obj.Attributes.SetUserString("CNC_Face_0_Diameter", "5.0");
        obj.Attributes.SetUserString("CNC_Face_0_Depth", "10.0");

        obj.Attributes.SetUserString("CNC_Face_2_Type", "POCKET");
        obj.Attributes.SetUserString("CNC_Face_2_Depth", "8.0");
        obj.Attributes.SetUserString("CNC_Face_2_ToolDia", "6.0");

        obj.Attributes.SetUserString("CNC_Face_5_Type", "MACRO");
        obj.Attributes.SetUserString("CNC_Face_5_MacroName", "TestMacro");

        obj.CommitChanges();

        // Act
        var machinings = FeatureReader.ReadTaggedFeatures(obj);

        // Assert
        Assert.Equal(3, machinings.Count);
        Assert.Contains(machinings, m => m is DrillMachining);
        Assert.Contains(machinings, m => m is PocketMachining);
        Assert.Contains(machinings, m => m is MacroMachining);
        Assert.All(machinings, m => Assert.Equal(MachiningSource.FaceTag, m.Source));
    }

    [Fact]
    public void ReadTaggedFeatures_InvalidType_SkipsFace()
    {
        // Arrange
        var box = CreateTestBox();
        var objectId = _doc.Objects.Add(box);
        var obj = _doc.Objects.FindId(objectId);

        obj.Attributes.SetUserString("CNC_Face_0_Type", "INVALID_TYPE");
        obj.Attributes.SetUserString("CNC_Face_0_Diameter", "5.0");
        obj.CommitChanges();

        // Act
        var machinings = FeatureReader.ReadTaggedFeatures(obj);

        // Assert
        Assert.Empty(machinings); // Should skip the invalid type
    }

    [Fact]
    public void ReadTaggedFeatures_MissingType_SkipsFace()
    {
        // Arrange
        var box = CreateTestBox();
        var objectId = _doc.Objects.Add(box);
        var obj = _doc.Objects.FindId(objectId);

        // Set some CNC_ attributes but no Type
        obj.Attributes.SetUserString("CNC_Face_0_Diameter", "5.0");
        obj.Attributes.SetUserString("CNC_Face_0_Depth", "10.0");
        obj.CommitChanges();

        // Act
        var machinings = FeatureReader.ReadTaggedFeatures(obj);

        // Assert
        Assert.Empty(machinings); // Should skip faces without Type
    }

    [Fact]
    public void ReadTaggedFeatures_InvalidParameters_SkipsFace()
    {
        // Arrange
        var box = CreateTestBox();
        var objectId = _doc.Objects.Add(box);
        var obj = _doc.Objects.FindId(objectId);

        // DRILL without required diameter
        obj.Attributes.SetUserString("CNC_Face_0_Type", "DRILL");
        obj.Attributes.SetUserString("CNC_Face_0_Depth", "10.0");
        // Missing Diameter

        obj.CommitChanges();

        // Act
        var machinings = FeatureReader.ReadTaggedFeatures(obj);

        // Assert
        Assert.Empty(machinings); // Should skip invalid drill
    }

    [Fact]
    public void ReadTaggedFeatures_NotABrep_ReturnsEmpty()
    {
        // Arrange
        var curve = new LineCurve(Point3d.Origin, new Point3d(10, 10, 0));
        var objectId = _doc.Objects.AddCurve(curve);
        var obj = _doc.Objects.FindId(objectId);

        obj.Attributes.SetUserString("CNC_Face_0_Type", "DRILL");
        obj.Attributes.SetUserString("CNC_Face_0_Diameter", "5.0");
        obj.Attributes.SetUserString("CNC_Face_0_Depth", "10.0");
        obj.CommitChanges();

        // Act
        var machinings = FeatureReader.ReadTaggedFeatures(obj);

        // Assert
        Assert.Empty(machinings); // Not a Brep, so no face features
    }

    [Fact]
    public void ReadTaggedFeatures_InvalidFaceIndex_SkipsFace()
    {
        // Arrange
        var box = CreateTestBox(); // Typically has 6 faces (0-5)
        var objectId = _doc.Objects.Add(box);
        var obj = _doc.Objects.FindId(objectId);

        // Tag a non-existent face index
        obj.Attributes.SetUserString("CNC_Face_99_Type", "DRILL");
        obj.Attributes.SetUserString("CNC_Face_99_Diameter", "5.0");
        obj.Attributes.SetUserString("CNC_Face_99_Depth", "10.0");
        obj.CommitChanges();

        // Act
        var machinings = FeatureReader.ReadTaggedFeatures(obj);

        // Assert
        Assert.Empty(machinings); // Should skip invalid face indices
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
}
#endif // RHINO_AVAILABLE
