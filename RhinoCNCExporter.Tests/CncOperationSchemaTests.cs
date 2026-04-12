using RhinoCNCExporter.Core.Blocks;
using Xunit;

namespace RhinoCNCExporter.Tests;

public class CncOperationSchemaTests
{
    [Fact]
    public void ValidateOperation_ContourWithValidParameters_ReturnsValid()
    {
        // Arrange
        var parameters = new Dictionary<string, string>
        {
            [CncOperationSchema.CNC_TOOL] = "Fräser 8mm",
            [CncOperationSchema.CNC_DEPTH] = "10.0"
        };

        // Act
        var (isValid, error) = CncOperationSchema.ValidateOperation(CncOperationSchema.TYPE_CONTOUR, parameters);

        // Assert
        Assert.True(isValid);
        Assert.Null(error);
    }

    [Fact]
    public void ValidateOperation_ContourMissingTool_ReturnsInvalid()
    {
        // Arrange
        var parameters = new Dictionary<string, string>
        {
            [CncOperationSchema.CNC_DEPTH] = "10.0"
        };

        // Act
        var (isValid, error) = CncOperationSchema.ValidateOperation(CncOperationSchema.TYPE_CONTOUR, parameters);

        // Assert
        Assert.False(isValid);
        Assert.Equal("Contour operation requires CNC_Tool", error);
    }

    [Fact]
    public void ValidateOperation_DrillWithValidParameters_ReturnsValid()
    {
        // Arrange
        var parameters = new Dictionary<string, string>
        {
            [CncOperationSchema.CNC_DIAMETER] = "5.0",
            [CncOperationSchema.CNC_DEPTH] = "13.0"
        };

        // Act
        var (isValid, error) = CncOperationSchema.ValidateOperation(CncOperationSchema.TYPE_DRILL, parameters);

        // Assert
        Assert.True(isValid);
        Assert.Null(error);
    }

    [Fact]
    public void ValidateOperation_DrillMissingDiameter_ReturnsInvalid()
    {
        // Arrange
        var parameters = new Dictionary<string, string>
        {
            [CncOperationSchema.CNC_DEPTH] = "13.0"
        };

        // Act
        var (isValid, error) = CncOperationSchema.ValidateOperation(CncOperationSchema.TYPE_DRILL, parameters);

        // Assert
        Assert.False(isValid);
        Assert.Equal("Drill operation requires CNC_Diameter", error);
    }

    [Fact]
    public void ValidateOperation_PocketWithValidParameters_ReturnsValid()
    {
        // Arrange
        var parameters = new Dictionary<string, string>
        {
            [CncOperationSchema.CNC_TOOL] = "Fräser 6mm",
            [CncOperationSchema.CNC_DEPTH] = "8.0"
        };

        // Act
        var (isValid, error) = CncOperationSchema.ValidateOperation(CncOperationSchema.TYPE_POCKET, parameters);

        // Assert
        Assert.True(isValid);
        Assert.Null(error);
    }

    [Fact]
    public void ValidateOperation_GrooveWithValidParameters_ReturnsValid()
    {
        // Arrange
        var parameters = new Dictionary<string, string>
        {
            [CncOperationSchema.CNC_TOOL] = "Nutfräser 5mm",
            [CncOperationSchema.CNC_WIDTH] = "5.0",
            [CncOperationSchema.CNC_DEPTH] = "8.0"
        };

        // Act
        var (isValid, error) = CncOperationSchema.ValidateOperation(CncOperationSchema.TYPE_GROOVE, parameters);

        // Assert
        Assert.True(isValid);
        Assert.Null(error);
    }

    [Fact]
    public void ValidateOperation_UnknownType_ReturnsInvalid()
    {
        // Arrange
        var parameters = new Dictionary<string, string>();

        // Act
        var (isValid, error) = CncOperationSchema.ValidateOperation("INVALID_TYPE", parameters);

        // Assert
        Assert.False(isValid);
        Assert.Equal("Unknown operation type: INVALID_TYPE", error);
    }

    [Theory]
    [InlineData("contour")]
    [InlineData("POCKET")]
    [InlineData("Drill")]
    [InlineData("groove")]
    public void ValidateOperation_CaseInsensitive_HandlesCorrectly(string inputType)
    {
        // Arrange
        var parameters = new Dictionary<string, string>
        {
            [CncOperationSchema.CNC_TOOL] = "Test Tool",
            [CncOperationSchema.CNC_DEPTH] = "10.0",
            [CncOperationSchema.CNC_DIAMETER] = "5.0", // For drill
            [CncOperationSchema.CNC_WIDTH] = "5.0" // For groove
        };

        // Act
        var (isValid, error) = CncOperationSchema.ValidateOperation(inputType, parameters);

        // Assert
        Assert.True(isValid, $"Should be valid for type {inputType}");
        Assert.Null(error);
    }
}