using System.Reflection;
using RhinoCNCExporter.Core.Blocks;
using Xunit;

namespace RhinoCNCExporter.Tests;

/// <summary>
/// Tests for CncOperationSchema constants and key naming conventions.
/// Ensures all UserText keys follow the CNC_ prefix convention.
/// </summary>
public class CncOperationSchemaKeyTests
{
    #region Key Constants Are Defined

    [Fact]
    public void AllUserTextKeys_AreDefined()
    {
        // Core operation keys
        Assert.Equal("CNC_Type", CncOperationSchema.CNC_TYPE);
        Assert.Equal("CNC_Tool", CncOperationSchema.CNC_TOOL);
        Assert.Equal("CNC_Depth", CncOperationSchema.CNC_DEPTH);
        Assert.Equal("CNC_Diameter", CncOperationSchema.CNC_DIAMETER);
        Assert.Equal("CNC_Width", CncOperationSchema.CNC_WIDTH);
        Assert.Equal("CNC_Strategy", CncOperationSchema.CNC_STRATEGY);
        Assert.Equal("CNC_Feedrate", CncOperationSchema.CNC_FEEDRATE);
        Assert.Equal("CNC_Stepover", CncOperationSchema.CNC_STEPOVER);
        Assert.Equal("CNC_Peck", CncOperationSchema.CNC_PECK);
        Assert.Equal("CNC_PeckDepth", CncOperationSchema.CNC_PECK_DEPTH);
        Assert.Equal("CNC_RampEntry", CncOperationSchema.CNC_RAMP_ENTRY);
        Assert.Equal("CNC_GroupIndex", CncOperationSchema.CNC_GROUP_INDEX);

        // Edge extraction keys
        Assert.Equal("CNC_SourceBrep", CncOperationSchema.CNC_SOURCE_BREP);
        Assert.Equal("CNC_SourceEdgeIndex", CncOperationSchema.CNC_SOURCE_EDGE_INDEX);
    }

    [Fact]
    public void AllKeyConstants_StartWithCNC()
    {
        // Use reflection to get all string constants from CncOperationSchema
        var fields = typeof(CncOperationSchema)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            .Where(f => f.Name.StartsWith("CNC_")) // Only key constants (not TYPE_, STRATEGY_, RAMP_)
            .ToList();

        Assert.True(fields.Count >= 14, $"Expected at least 14 CNC_ key constants, found {fields.Count}");

        foreach (var field in fields)
        {
            var value = (string)field.GetValue(null)!;
            Assert.True(value.StartsWith("CNC_"),
                $"Constant {field.Name} has value '{value}' which doesn't start with 'CNC_'");
        }
    }

    [Fact]
    public void AllKeyConstants_AreNonEmpty()
    {
        var fields = typeof(CncOperationSchema)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            .ToList();

        foreach (var field in fields)
        {
            var value = (string)field.GetValue(null)!;
            Assert.False(string.IsNullOrWhiteSpace(value),
                $"Constant {field.Name} should not be empty or whitespace");
        }
    }

    #endregion

    #region Key Naming Convention

    [Fact]
    public void KeyValues_UsePascalCase()
    {
        // All key values should follow CNC_PascalCase convention
        var keyFields = typeof(CncOperationSchema)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            .Where(f => f.Name.StartsWith("CNC_"))
            .ToList();

        foreach (var field in keyFields)
        {
            var value = (string)field.GetValue(null)!;
            // Should start with CNC_ followed by uppercase letter
            Assert.Matches(@"^CNC_[A-Z]", value);
        }
    }

    [Fact]
    public void KeyFieldNames_MatchValues()
    {
        // Verify naming consistency: field name and value should correspond
        // e.g., CNC_TYPE = "CNC_Type", CNC_TOOL = "CNC_Tool"
        Assert.Equal("CNC_Type", CncOperationSchema.CNC_TYPE);
        Assert.Equal("CNC_Tool", CncOperationSchema.CNC_TOOL);
        Assert.Equal("CNC_Depth", CncOperationSchema.CNC_DEPTH);
        Assert.Equal("CNC_GroupIndex", CncOperationSchema.CNC_GROUP_INDEX);
    }

    #endregion

    #region Operation Type Constants

    [Fact]
    public void OperationTypes_AreDefined()
    {
        Assert.Equal("Contour", CncOperationSchema.TYPE_CONTOUR);
        Assert.Equal("Pocket", CncOperationSchema.TYPE_POCKET);
        Assert.Equal("Drill", CncOperationSchema.TYPE_DRILL);
        Assert.Equal("Groove", CncOperationSchema.TYPE_GROOVE);
    }

    [Fact]
    public void OperationTypes_ArePascalCase()
    {
        Assert.Matches("^[A-Z][a-z]+$", CncOperationSchema.TYPE_CONTOUR);
        Assert.Matches("^[A-Z][a-z]+$", CncOperationSchema.TYPE_POCKET);
        Assert.Matches("^[A-Z][a-z]+$", CncOperationSchema.TYPE_DRILL);
        Assert.Matches("^[A-Z][a-z]+$", CncOperationSchema.TYPE_GROOVE);
    }

    #endregion

    #region Strategy Constants

    [Fact]
    public void Strategies_AreDefined()
    {
        Assert.Equal("Rough", CncOperationSchema.STRATEGY_ROUGH);
        Assert.Equal("Finish", CncOperationSchema.STRATEGY_FINISH);
        Assert.Equal("Both", CncOperationSchema.STRATEGY_BOTH);
    }

    #endregion

    #region Ramp Entry Constants

    [Fact]
    public void RampEntryTypes_AreDefined()
    {
        Assert.Equal("Straight", CncOperationSchema.RAMP_STRAIGHT);
        Assert.Equal("Spiral", CncOperationSchema.RAMP_SPIRAL);
        Assert.Equal("Profile", CncOperationSchema.RAMP_PROFILE);
    }

    #endregion
}
