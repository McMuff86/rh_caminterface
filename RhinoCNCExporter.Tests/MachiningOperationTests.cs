using RhinoCNCExporter.Core.Blocks;
using Xunit;

namespace RhinoCNCExporter.Tests;

public class MachiningOperationTests
{
    [Fact]
    public void MachiningOperation_ContourParameters_ParsedCorrectly()
    {
        // Arrange
        var parameters = new Dictionary<string, string>
        {
            [CncOperationSchema.CNC_TOOL] = "Fräser 8mm",
            [CncOperationSchema.CNC_DEPTH] = "10.5",
            [CncOperationSchema.CNC_STRATEGY] = CncOperationSchema.STRATEGY_BOTH,
            [CncOperationSchema.CNC_FEEDRATE] = "3000.0"
        };

        // Act
        var operation = new MachiningOperation(CncOperationSchema.TYPE_CONTOUR, parameters);

        // Assert
        Assert.Equal(CncOperationSchema.TYPE_CONTOUR, operation.Type);
        Assert.Equal("Fräser 8mm", operation.Tool);
        Assert.Equal(10.5, operation.Depth);
        Assert.Equal(CncOperationSchema.STRATEGY_BOTH, operation.Strategy);
        Assert.Equal(3000.0, operation.Feedrate);
    }

    [Fact]
    public void MachiningOperation_DrillParameters_ParsedCorrectly()
    {
        // Arrange
        var parameters = new Dictionary<string, string>
        {
            [CncOperationSchema.CNC_DIAMETER] = "5.0",
            [CncOperationSchema.CNC_DEPTH] = "13.0",
            [CncOperationSchema.CNC_PECK] = "true",
            [CncOperationSchema.CNC_PECK_DEPTH] = "3.0"
        };

        // Act
        var operation = new MachiningOperation(CncOperationSchema.TYPE_DRILL, parameters);

        // Assert
        Assert.Equal(CncOperationSchema.TYPE_DRILL, operation.Type);
        Assert.Equal(5.0, operation.Diameter);
        Assert.Equal(13.0, operation.Depth);
        Assert.True(operation.Peck);
        Assert.Equal(3.0, operation.PeckDepth);
    }

    [Fact]
    public void MachiningOperation_PocketParameters_ParsedCorrectly()
    {
        // Arrange
        var parameters = new Dictionary<string, string>
        {
            [CncOperationSchema.CNC_TOOL] = "Fräser 6mm",
            [CncOperationSchema.CNC_DEPTH] = "8.0",
            [CncOperationSchema.CNC_STEPOVER] = "60.0",
            [CncOperationSchema.CNC_STRATEGY] = CncOperationSchema.STRATEGY_ROUGH,
            [CncOperationSchema.CNC_RAMP_ENTRY] = CncOperationSchema.RAMP_SPIRAL
        };

        // Act
        var operation = new MachiningOperation(CncOperationSchema.TYPE_POCKET, parameters);

        // Assert
        Assert.Equal(CncOperationSchema.TYPE_POCKET, operation.Type);
        Assert.Equal("Fräser 6mm", operation.Tool);
        Assert.Equal(8.0, operation.Depth);
        Assert.Equal(60.0, operation.Stepover);
        Assert.Equal(CncOperationSchema.STRATEGY_ROUGH, operation.Strategy);
        Assert.Equal(CncOperationSchema.RAMP_SPIRAL, operation.RampEntry);
    }

    [Fact]
    public void MachiningOperation_GrooveParameters_ParsedCorrectly()
    {
        // Arrange
        var parameters = new Dictionary<string, string>
        {
            [CncOperationSchema.CNC_TOOL] = "Nutfräser 5mm",
            [CncOperationSchema.CNC_WIDTH] = "5.0",
            [CncOperationSchema.CNC_DEPTH] = "8.0"
        };

        // Act
        var operation = new MachiningOperation(CncOperationSchema.TYPE_GROOVE, parameters);

        // Assert
        Assert.Equal(CncOperationSchema.TYPE_GROOVE, operation.Type);
        Assert.Equal("Nutfräser 5mm", operation.Tool);
        Assert.Equal(5.0, operation.Width);
        Assert.Equal(8.0, operation.Depth);
    }

    [Fact]
    public void MachiningOperation_MissingParameters_ReturnsNull()
    {
        // Arrange
        var parameters = new Dictionary<string, string>
        {
            [CncOperationSchema.CNC_TOOL] = "Test Tool"
        };

        // Act
        var operation = new MachiningOperation(CncOperationSchema.TYPE_CONTOUR, parameters);

        // Assert
        Assert.Equal("Test Tool", operation.Tool);
        Assert.Null(operation.Depth);
        Assert.Null(operation.Diameter);
        Assert.Null(operation.Feedrate);
        Assert.Null(operation.Peck);
    }

    [Fact]
    public void MachiningOperation_InvalidNumbers_ReturnsNull()
    {
        // Arrange
        var parameters = new Dictionary<string, string>
        {
            [CncOperationSchema.CNC_DEPTH] = "invalid_number",
            [CncOperationSchema.CNC_DIAMETER] = "also_invalid"
        };

        // Act
        var operation = new MachiningOperation(CncOperationSchema.TYPE_DRILL, parameters);

        // Assert
        Assert.Null(operation.Depth);
        Assert.Null(operation.Diameter);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("True", true)]
    [InlineData("False", false)]
    [InlineData("invalid", null)]
    public void MachiningOperation_BooleanParsing_HandlesCorrectly(string value, bool? expected)
    {
        // Arrange
        var parameters = new Dictionary<string, string>
        {
            [CncOperationSchema.CNC_PECK] = value
        };

        // Act
        var operation = new MachiningOperation(CncOperationSchema.TYPE_DRILL, parameters);

        // Assert
        Assert.Equal(expected, operation.Peck);
    }
}