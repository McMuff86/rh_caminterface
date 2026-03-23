using RhinoCNCExporter.Core.Blocks;
using RhinoCNCExporter.Core.Models;
using Xunit;

namespace RhinoCNCExporter.Tests;

public class MachiningFactoryTests
{
    private static FittingBlock CreateBlock(string cncType, Dictionary<string, string>? extraAttrs = null)
    {
        var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["CNC_Type"] = cncType };
        if (extraAttrs != null)
            foreach (var kv in extraAttrs)
                attrs[kv.Key] = kv.Value;

        return new FittingBlock
        {
            BlockName = $"Test_{cncType}",
            CncType = cncType,
            InsertionPoint = (0, 0, 0),
            CncAttributes = attrs
        };
    }

    // --- DRILL ---

    [Fact]
    public void CreateFromBlock_Drill_ReturnsDrillMachining()
    {
        var block = CreateBlock("DRILL", new() { ["CNC_Diameter"] = "35", ["CNC_Depth"] = "13" });
        var result = MachiningFactory.CreateFromBlock(block, 100, 200, 0, 19);

        Assert.Single(result);
        var drill = Assert.IsType<DrillMachining>(result[0]);
        Assert.Equal(100, drill.X);
        Assert.Equal(200, drill.Y);
        Assert.Equal(13, drill.Depth);
        Assert.Equal(35, drill.Diameter);
        Assert.Equal(MachiningSource.BlockDetection, drill.Source);
    }

    [Fact]
    public void CreateFromBlock_Drill_ThroughHole()
    {
        var block = CreateBlock("DRILL", new()
        {
            ["CNC_Diameter"] = "15",
            ["CNC_Through"] = "true"
        });
        var result = MachiningFactory.CreateFromBlock(block, 50, 50, 0, 19);

        var drill = Assert.IsType<DrillMachining>(result[0]);
        Assert.Equal(19, drill.Depth); // plate thickness
    }

    [Fact]
    public void CreateFromBlock_Drill_DepthPlaceholder()
    {
        var block = CreateBlock("DRILL", new() { ["CNC_Depth"] = "{DZ}-2" });
        var result = MachiningFactory.CreateFromBlock(block, 50, 50, 0, 19);

        var drill = Assert.IsType<DrillMachining>(result[0]);
        Assert.Equal(17, drill.Depth); // 19 - 2
    }

    [Fact]
    public void CreateFromBlock_Drill_DefaultDiameter()
    {
        var block = CreateBlock("DRILL");
        var result = MachiningFactory.CreateFromBlock(block, 50, 50, 0, 19);

        var drill = Assert.IsType<DrillMachining>(result[0]);
        Assert.Equal(5.0, drill.Diameter); // default
    }

    [Fact]
    public void CreateFromBlock_Drill_WithTechCode()
    {
        var block = CreateBlock("DRILL", new() { ["CNC_TechCode"] = "E009" });
        var result = MachiningFactory.CreateFromBlock(block, 50, 50, 0, 19);

        var drill = Assert.IsType<DrillMachining>(result[0]);
        Assert.Equal("E009", drill.TechCode);
    }

    [Fact]
    public void CreateFromBlock_Drill_WithSide()
    {
        var block = CreateBlock("DRILL", new() { ["CNC_Side"] = "BOTTOM" });
        var result = MachiningFactory.CreateFromBlock(block, 50, 50, 0, 19);

        var drill = Assert.IsType<DrillMachining>(result[0]);
        Assert.Equal(MachiningSide.Bottom, drill.Side);
    }

    // --- DRILLPATTERN ---

    [Fact]
    public void CreateFromBlock_DrillPattern_ReturnsDrillPatternMachining()
    {
        var block = CreateBlock("DRILLPATTERN", new()
        {
            ["CNC_Diameter"] = "5",
            ["CNC_Depth"] = "13",
            ["CNC_PatternX"] = "1",
            ["CNC_PatternY"] = "10",
            ["CNC_SpacingX"] = "0",
            ["CNC_SpacingY"] = "32"
        });

        var result = MachiningFactory.CreateFromBlock(block, 37, 96, 0, 19);

        Assert.Single(result);
        var dp = Assert.IsType<DrillPatternMachining>(result[0]);
        Assert.Equal(37, dp.X);
        Assert.Equal(96, dp.Y);
        Assert.Equal(1, dp.CountX);
        Assert.Equal(10, dp.CountY);
        Assert.Equal(0, dp.SpacingX);
        Assert.Equal(32, dp.SpacingY);
    }

    [Fact]
    public void CreateFromBlock_DrillPattern_Rotated90_SwapsAxes()
    {
        var attrs = new Dictionary<string, string>
        {
            ["CNC_Type"] = "DRILLPATTERN",
            ["CNC_PatternX"] = "1",
            ["CNC_PatternY"] = "10",
            ["CNC_SpacingX"] = "0",
            ["CNC_SpacingY"] = "32"
        };
        var block = new FittingBlock
        {
            BlockName = "Lochreihe_32",
            CncType = "DRILLPATTERN",
            InsertionPoint = (0, 0, 0),
            Rotation = 90,
            CncAttributes = attrs
        };

        var result = MachiningFactory.CreateFromBlock(block, 37, 96, 0, 19);
        var dp = Assert.IsType<DrillPatternMachining>(result[0]);

        // Rotated: X/Y swapped
        Assert.Equal(10, dp.CountX);
        Assert.Equal(1, dp.CountY);
        Assert.Equal(32, dp.SpacingX);
        Assert.Equal(0, dp.SpacingY);
    }

    // --- MACRO ---

    [Fact]
    public void CreateFromBlock_Macro_ReturnsMacroMachining()
    {
        var block = CreateBlock("MACRO", new()
        {
            ["CNC_MacroName"] = "SawCut_Lamello",
            ["CNC_MacroParams"] = "{DZ}-9.5,100,null,0"
        });

        var result = MachiningFactory.CreateFromBlock(block, 50, 100, 0, 19);

        Assert.Single(result);
        var macro = Assert.IsType<MacroMachining>(result[0]);
        Assert.Equal("SawCut_Lamello", macro.MacroName);
        Assert.Equal(4, macro.Parameters.Count);
        Assert.Equal("9.5", macro.Parameters[0]); // 19-9.5 = 9.5
        Assert.Equal("100", macro.Parameters[1]);
        Assert.Null(macro.Parameters[2]);
        Assert.Equal("0", macro.Parameters[3]);
    }

    [Fact]
    public void CreateFromBlock_Macro_WithoutMacroName_ReturnsEmpty()
    {
        var block = CreateBlock("MACRO");
        var result = MachiningFactory.CreateFromBlock(block, 0, 0, 0, 19);
        Assert.Empty(result);
    }

    // --- HDRILL ---

    [Fact]
    public void CreateFromBlock_HDrill_ReturnsHorizontalDrillMachining()
    {
        var block = CreateBlock("HDRILL", new()
        {
            ["CNC_Diameter"] = "8",
            ["CNC_Depth"] = "30",
            ["CNC_Side"] = "LEFT"
        });

        var result = MachiningFactory.CreateFromBlock(block, 50, 100, 0, 19);

        Assert.Single(result);
        var hdrill = Assert.IsType<HorizontalDrillMachining>(result[0]);
        Assert.Equal(8, hdrill.Diameter);
        Assert.Equal(30, hdrill.Depth);
        Assert.Equal('L', hdrill.DrillSide);
    }

    [Theory]
    [InlineData("LEFT", 'L')]
    [InlineData("RIGHT", 'R')]
    [InlineData("FRONT", 'V')]
    [InlineData("BACK", 'H')]
    public void CreateFromBlock_HDrill_SideMapping(string side, char expected)
    {
        var block = CreateBlock("HDRILL", new() { ["CNC_Side"] = side });
        var result = MachiningFactory.CreateFromBlock(block, 0, 0, 0, 19);

        var hdrill = Assert.IsType<HorizontalDrillMachining>(result[0]);
        Assert.Equal(expected, hdrill.DrillSide);
    }

    // --- CUT, POCKET, GROOVE (Phase 3 stubs) ---

    [Fact]
    public void CreateFromBlock_Cut_ReturnsEmpty()
    {
        var block = CreateBlock("CUT");
        var result = MachiningFactory.CreateFromBlock(block, 0, 0, 0, 19);
        Assert.Empty(result);
    }

    [Fact]
    public void CreateFromBlock_Pocket_ReturnsEmpty()
    {
        var block = CreateBlock("POCKET");
        var result = MachiningFactory.CreateFromBlock(block, 0, 0, 0, 19);
        Assert.Empty(result);
    }

    [Fact]
    public void CreateFromBlock_Groove_ReturnsEmpty()
    {
        var block = CreateBlock("GROOVE");
        var result = MachiningFactory.CreateFromBlock(block, 0, 0, 0, 19);
        Assert.Empty(result);
    }

    // --- Unknown type ---

    [Fact]
    public void CreateFromBlock_UnknownType_ReturnsEmpty()
    {
        var block = CreateBlock("UNKNOWN");
        var result = MachiningFactory.CreateFromBlock(block, 0, 0, 0, 19);
        Assert.Empty(result);
    }

    // --- Template expansion ---

    [Fact]
    public void ExpandTemplateParams_DZ_Replacement()
    {
        var result = MachiningFactory.ExpandTemplateParams("{DZ}", 19, 0, 0);
        Assert.Equal("19", result);
    }

    [Fact]
    public void ExpandTemplateParams_DZ_Minus()
    {
        var result = MachiningFactory.ExpandTemplateParams("{DZ}-9.5", 19, 0, 0);
        Assert.Equal("9.5", result);
    }

    [Fact]
    public void ExpandTemplateParams_DZ_Plus()
    {
        var result = MachiningFactory.ExpandTemplateParams("{DZ}+1", 19, 0, 0);
        Assert.Equal("20", result);
    }

    [Fact]
    public void ExpandTemplateParams_XY_Replacement()
    {
        var result = MachiningFactory.ExpandTemplateParams("{X},{Y}", 19, 50.5, 100);
        Assert.Equal("50.5,100", result);
    }

    [Fact]
    public void ExpandTemplateParams_Mixed()
    {
        var result = MachiningFactory.ExpandTemplateParams("{DZ}-9.5,{Y},{DZ}-9.5,{Y}", 19, 0, 100);
        Assert.Equal("9.5,100,9.5,100", result);
    }

    [Fact]
    public void ExpandTemplateParams_EmptyString()
    {
        var result = MachiningFactory.ExpandTemplateParams("", 19, 0, 0);
        Assert.Equal("", result);
    }

    [Fact]
    public void ExpandTemplateParams_NoPlaceholders()
    {
        var result = MachiningFactory.ExpandTemplateParams("42,0,1", 19, 0, 0);
        Assert.Equal("42,0,1", result);
    }
}
