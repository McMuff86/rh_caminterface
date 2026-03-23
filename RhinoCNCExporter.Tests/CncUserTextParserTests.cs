using RhinoCNCExporter.Core.Blocks;
using RhinoCNCExporter.Core.Models;
using Xunit;

namespace RhinoCNCExporter.Tests;

public class CncUserTextParserTests
{
    [Fact]
    public void Parse_ValidDrill_ReturnsFittingBlock()
    {
        var userText = new Dictionary<string, string>
        {
            ["CNC_Type"] = "DRILL",
            ["CNC_Diameter"] = "35",
            ["CNC_Depth"] = "13",
            ["CNC_Side"] = "TOP",
            ["SomeOtherKey"] = "ignored"
        };

        var block = CncUserTextParser.Parse("Topfband_35", userText, (100, 200, 0), 0, "Seite_links", out var error);

        Assert.NotNull(block);
        Assert.Null(error);
        Assert.Equal("Topfband_35", block.BlockName);
        Assert.Equal("DRILL", block.CncType);
        Assert.Equal(100, block.InsertionPoint.X);
        Assert.Equal(200, block.InsertionPoint.Y);
        Assert.Equal("Seite_links", block.LayerName);
        // SomeOtherKey should be filtered out
        Assert.False(block.CncAttributes.ContainsKey("SomeOtherKey"));
        Assert.True(block.CncAttributes.ContainsKey("CNC_Type"));
    }

    [Fact]
    public void Parse_NoCncKeys_ReturnsNull()
    {
        var userText = new Dictionary<string, string> { ["Name"] = "Test" };
        var block = CncUserTextParser.Parse("Test", userText, (0, 0, 0), 0, null, out var error);
        Assert.Null(block);
        Assert.NotNull(error);
    }

    [Fact]
    public void Parse_InvalidCncType_ReturnsNull()
    {
        var userText = new Dictionary<string, string> { ["CNC_Type"] = "LASER" };
        var block = CncUserTextParser.Parse("Test", userText, (0, 0, 0), 0, null, out var error);
        Assert.Null(block);
        Assert.Contains("LASER", error);
    }

    [Fact]
    public void Parse_MacroWithoutMacroName_ReturnsNull()
    {
        var userText = new Dictionary<string, string> { ["CNC_Type"] = "MACRO" };
        var block = CncUserTextParser.Parse("Test", userText, (0, 0, 0), 0, null, out var error);
        Assert.Null(block);
        Assert.Contains("CNC_MacroName", error);
    }

    [Fact]
    public void Parse_ValidMacro_ReturnsFittingBlock()
    {
        var userText = new Dictionary<string, string>
        {
            ["CNC_Type"] = "MACRO",
            ["CNC_MacroName"] = "SawCut_Lamello",
            ["CNC_MacroParams"] = "{DZ}-9.5,{Y}"
        };

        var block = CncUserTextParser.Parse("CLAMEX_P14", userText, (50, 100, 0), 90, null, out var error);

        Assert.NotNull(block);
        Assert.Null(error);
        Assert.Equal("MACRO", block.CncType);
        Assert.Equal(90, block.Rotation);
        Assert.Equal("SawCut_Lamello", block.MacroName);
    }

    [Fact]
    public void Parse_CaseInsensitiveCncKeys()
    {
        var userText = new Dictionary<string, string>
        {
            ["cnc_type"] = "DRILL",
            ["cnc_diameter"] = "5"
        };

        var block = CncUserTextParser.Parse("Test", userText, (0, 0, 0), 0, null, out var error);

        Assert.NotNull(block);
        Assert.Null(error);
    }

    [Fact]
    public void Parse_DrillPattern_AllFields()
    {
        var userText = new Dictionary<string, string>
        {
            ["CNC_Type"] = "DRILLPATTERN",
            ["CNC_Diameter"] = "5",
            ["CNC_Depth"] = "13",
            ["CNC_PatternX"] = "1",
            ["CNC_PatternY"] = "10",
            ["CNC_SpacingX"] = "0",
            ["CNC_SpacingY"] = "32",
            ["CNC_TechCode"] = "E013"
        };

        var block = CncUserTextParser.Parse("Lochreihe_32", userText, (37, 96, 0), 0, null, out var error);

        Assert.NotNull(block);
        Assert.Equal("DRILLPATTERN", block.CncType);
        Assert.Equal(5.0, block.Diameter);
        Assert.Equal(13.0, block.Depth);
    }

    [Fact]
    public void Parse_EmptyDict_ReturnsNull()
    {
        var block = CncUserTextParser.Parse("Test", new Dictionary<string, string>(), (0, 0, 0), 0, null, out _);
        Assert.Null(block);
    }

    [Fact]
    public void Parse_ValidationError_NegativeDiameter_ReturnsNull()
    {
        var userText = new Dictionary<string, string>
        {
            ["CNC_Type"] = "DRILL",
            ["CNC_Diameter"] = "-5"
        };
        var block = CncUserTextParser.Parse("Test", userText, (0, 0, 0), 0, null, out var error);
        Assert.Null(block);
        Assert.Contains("CNC_Diameter", error);
    }
}
