using RhinoCNCExporter.Core.Blocks;
using Xunit;

namespace RhinoCNCExporter.Tests;

public class BlockUserTextSchemaTests
{
    [Fact]
    public void Validate_ValidDrill_Succeeds()
    {
        var attrs = new Dictionary<string, string>
        {
            ["CNC_Type"] = "DRILL",
            ["CNC_Diameter"] = "35",
            ["CNC_Depth"] = "13",
            ["CNC_Side"] = "TOP"
        };
        var (isValid, error) = BlockUserTextSchema.Validate(attrs);
        Assert.True(isValid);
        Assert.Null(error);
    }

    [Fact]
    public void Validate_MinimalDrill_Succeeds()
    {
        var attrs = new Dictionary<string, string> { ["CNC_Type"] = "DRILL" };
        var (isValid, _) = BlockUserTextSchema.Validate(attrs);
        Assert.True(isValid);
    }

    [Fact]
    public void Validate_MissingCncType_Fails()
    {
        var attrs = new Dictionary<string, string> { ["CNC_Diameter"] = "5" };
        var (isValid, error) = BlockUserTextSchema.Validate(attrs);
        Assert.False(isValid);
        Assert.Contains("CNC_Type", error);
    }

    [Fact]
    public void Validate_EmptyCncType_Fails()
    {
        var attrs = new Dictionary<string, string> { ["CNC_Type"] = "" };
        var (isValid, error) = BlockUserTextSchema.Validate(attrs);
        Assert.False(isValid);
        Assert.Contains("CNC_Type", error);
    }

    [Fact]
    public void Validate_UnknownCncType_Fails()
    {
        var attrs = new Dictionary<string, string> { ["CNC_Type"] = "LASER" };
        var (isValid, error) = BlockUserTextSchema.Validate(attrs);
        Assert.False(isValid);
        Assert.Contains("LASER", error);
    }

    [Theory]
    [InlineData("DRILL")]
    [InlineData("drill")]
    [InlineData("DRILLPATTERN")]
    [InlineData("MACRO")]
    [InlineData("CUT")]
    [InlineData("POCKET")]
    [InlineData("GROOVE")]
    [InlineData("HDRILL")]
    public void Validate_AllValidTypes_Succeed(string cncType)
    {
        var attrs = new Dictionary<string, string> { ["CNC_Type"] = cncType };
        // MACRO needs MacroName
        if (cncType.Equals("MACRO", StringComparison.OrdinalIgnoreCase))
            attrs["CNC_MacroName"] = "TestMacro";

        var (isValid, _) = BlockUserTextSchema.Validate(attrs);
        Assert.True(isValid);
    }

    [Fact]
    public void Validate_CaseInsensitive()
    {
        var attrs = new Dictionary<string, string> { ["CNC_Type"] = "drill" };
        var (isValid, _) = BlockUserTextSchema.Validate(attrs);
        Assert.True(isValid);
    }

    [Fact]
    public void Validate_MacroWithoutMacroName_Fails()
    {
        var attrs = new Dictionary<string, string> { ["CNC_Type"] = "MACRO" };
        var (isValid, error) = BlockUserTextSchema.Validate(attrs);
        Assert.False(isValid);
        Assert.Contains("CNC_MacroName", error);
    }

    [Fact]
    public void Validate_MacroWithMacroName_Succeeds()
    {
        var attrs = new Dictionary<string, string>
        {
            ["CNC_Type"] = "MACRO",
            ["CNC_MacroName"] = "SawCut_Lamello"
        };
        var (isValid, _) = BlockUserTextSchema.Validate(attrs);
        Assert.True(isValid);
    }

    [Fact]
    public void Validate_InvalidSide_Fails()
    {
        var attrs = new Dictionary<string, string>
        {
            ["CNC_Type"] = "DRILL",
            ["CNC_Side"] = "DIAGONAL"
        };
        var (isValid, error) = BlockUserTextSchema.Validate(attrs);
        Assert.False(isValid);
        Assert.Contains("CNC_Side", error);
    }

    [Theory]
    [InlineData("TOP")]
    [InlineData("BOTTOM")]
    [InlineData("LEFT")]
    [InlineData("RIGHT")]
    [InlineData("FRONT")]
    [InlineData("BACK")]
    [InlineData("top")]
    public void Validate_ValidSides_Succeed(string side)
    {
        var attrs = new Dictionary<string, string>
        {
            ["CNC_Type"] = "DRILL",
            ["CNC_Side"] = side
        };
        var (isValid, _) = BlockUserTextSchema.Validate(attrs);
        Assert.True(isValid);
    }

    [Fact]
    public void Validate_InvalidOrientation_Fails()
    {
        var attrs = new Dictionary<string, string>
        {
            ["CNC_Type"] = "DRILL",
            ["CNC_Orientation"] = "45"
        };
        var (isValid, error) = BlockUserTextSchema.Validate(attrs);
        Assert.False(isValid);
        Assert.Contains("CNC_Orientation", error);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("90")]
    [InlineData("180")]
    [InlineData("270")]
    public void Validate_ValidOrientations_Succeed(string orient)
    {
        var attrs = new Dictionary<string, string>
        {
            ["CNC_Type"] = "DRILL",
            ["CNC_Orientation"] = orient
        };
        var (isValid, _) = BlockUserTextSchema.Validate(attrs);
        Assert.True(isValid);
    }

    [Fact]
    public void Validate_NegativeDiameter_Fails()
    {
        var attrs = new Dictionary<string, string>
        {
            ["CNC_Type"] = "DRILL",
            ["CNC_Diameter"] = "-5"
        };
        var (isValid, error) = BlockUserTextSchema.Validate(attrs);
        Assert.False(isValid);
        Assert.Contains("CNC_Diameter", error);
    }

    [Fact]
    public void Validate_ZeroDiameter_Fails()
    {
        var attrs = new Dictionary<string, string>
        {
            ["CNC_Type"] = "DRILL",
            ["CNC_Diameter"] = "0"
        };
        var (isValid, error) = BlockUserTextSchema.Validate(attrs);
        Assert.False(isValid);
        Assert.Contains("CNC_Diameter", error);
    }

    [Fact]
    public void Validate_DepthWithPlaceholder_Succeeds()
    {
        var attrs = new Dictionary<string, string>
        {
            ["CNC_Type"] = "DRILL",
            ["CNC_Depth"] = "{DZ}-2"
        };
        var (isValid, _) = BlockUserTextSchema.Validate(attrs);
        Assert.True(isValid);
    }

    [Fact]
    public void Validate_NegativeSpacing_Fails()
    {
        var attrs = new Dictionary<string, string>
        {
            ["CNC_Type"] = "DRILLPATTERN",
            ["CNC_SpacingX"] = "-5"
        };
        var (isValid, error) = BlockUserTextSchema.Validate(attrs);
        Assert.False(isValid);
        Assert.Contains("CNC_SpacingX", error);
    }

    [Fact]
    public void Validate_ZeroPatternCount_Fails()
    {
        var attrs = new Dictionary<string, string>
        {
            ["CNC_Type"] = "DRILLPATTERN",
            ["CNC_PatternX"] = "0"
        };
        var (isValid, error) = BlockUserTextSchema.Validate(attrs);
        Assert.False(isValid);
        Assert.Contains("CNC_PatternX", error);
    }

    [Fact]
    public void Constants_AreCorrect()
    {
        Assert.Equal("CNC_Type", BlockUserTextSchema.CNC_TYPE);
        Assert.Equal("CNC_Diameter", BlockUserTextSchema.CNC_DIAMETER);
        Assert.Equal("CNC_MacroName", BlockUserTextSchema.CNC_MACRO_NAME);
        Assert.Equal("CNC_Through", BlockUserTextSchema.CNC_THROUGH);
    }

    [Fact]
    public void ValidTypes_Contains7Types()
    {
        Assert.Equal(7, BlockUserTextSchema.ValidTypes.Count);
    }
}
