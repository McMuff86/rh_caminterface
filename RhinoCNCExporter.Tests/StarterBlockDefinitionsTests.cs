using RhinoCNCExporter.Core.Blocks;
using RhinoCNCExporter.Core.Blocks.StarterBlocks;
using RhinoCNCExporter.Core.Models;
using Xunit;

namespace RhinoCNCExporter.Tests;

/// <summary>
/// Tests for StarterBlockDefinitions — validates all starter blocks
/// conform to the BlockUserTextSchema and produce valid Machinings.
/// </summary>
public class StarterBlockDefinitionsTests
{
    // --- Schema Validation ---

    [Fact]
    public void Topfband_35_IsValidSchema()
    {
        var (isValid, error) = BlockUserTextSchema.Validate(StarterBlockDefinitions.Topfband_35);
        Assert.True(isValid, error);
    }

    [Fact]
    public void Lochreihe_32_IsValidSchema()
    {
        var (isValid, error) = BlockUserTextSchema.Validate(StarterBlockDefinitions.Lochreihe_32);
        Assert.True(isValid, error);
    }

    [Fact]
    public void Duebel_8x30_IsValidSchema()
    {
        var (isValid, error) = BlockUserTextSchema.Validate(StarterBlockDefinitions.Duebel_8x30);
        Assert.True(isValid, error);
    }

    [Fact]
    public void Duebel_8x30_Stirn_IsValidSchema()
    {
        var (isValid, error) = BlockUserTextSchema.Validate(StarterBlockDefinitions.Duebel_8x30_Stirn);
        Assert.True(isValid, error);
    }

    [Fact]
    public void CLAMEX_P14_IsValidSchema()
    {
        var (isValid, error) = BlockUserTextSchema.Validate(StarterBlockDefinitions.CLAMEX_P14);
        Assert.True(isValid, error);
    }

    [Fact]
    public void All_Contains5Definitions()
    {
        Assert.Equal(5, StarterBlockDefinitions.All.Count);
    }

    [Fact]
    public void All_AllDefinitionsValidate()
    {
        foreach (var (name, def) in StarterBlockDefinitions.All)
        {
            var (isValid, error) = BlockUserTextSchema.Validate(def);
            Assert.True(isValid, $"Block '{name}' failed validation: {error}");
        }
    }

    // --- CncUserTextParser Integration ---

    [Theory]
    [InlineData("Topfband_35")]
    [InlineData("Lochreihe_32")]
    [InlineData("Duebel_8x30")]
    [InlineData("Duebel_8x30_Stirn")]
    [InlineData("CLAMEX_P14")]
    public void AllStarterBlocks_ParseSuccessfully(string blockName)
    {
        var def = GetDefinition(blockName);
        var block = CncUserTextParser.Parse(blockName, def, (100, 200, 0), 0, "TestLayer", out var error);

        Assert.NotNull(block);
        Assert.Null(error);
        Assert.Equal(blockName, block.BlockName);
    }

    // --- MachiningFactory Integration ---

    [Fact]
    public void Topfband_35_CreatesDrillMachining()
    {
        var block = ParseBlock("Topfband_35", StarterBlockDefinitions.Topfband_35);
        var machinings = MachiningFactory.CreateFromBlock(block!, 100, 200, 0, 19);

        Assert.Single(machinings);
        var drill = Assert.IsType<DrillMachining>(machinings[0]);
        Assert.Equal(35, drill.Diameter);
        Assert.Equal(13, drill.Depth);
        Assert.Equal(100, drill.X);
        Assert.Equal(200, drill.Y);
        Assert.Equal(MachiningSide.Top, drill.Side);
        Assert.Equal("E009", drill.TechCode);
    }

    [Fact]
    public void Lochreihe_32_CreatesDrillPatternMachining()
    {
        var block = ParseBlock("Lochreihe_32", StarterBlockDefinitions.Lochreihe_32);
        var machinings = MachiningFactory.CreateFromBlock(block!, 37, 96, 0, 19);

        Assert.Single(machinings);
        var dp = Assert.IsType<DrillPatternMachining>(machinings[0]);
        Assert.Equal(5, dp.Diameter);
        Assert.Equal(13, dp.Depth);
        Assert.Equal(1, dp.CountX);
        Assert.Equal(10, dp.CountY);
        Assert.Equal(0, dp.SpacingX);
        Assert.Equal(32, dp.SpacingY);
        Assert.Equal("E013", dp.TechCode);
    }

    [Fact]
    public void Duebel_8x30_CreatesDrillMachining()
    {
        var block = ParseBlock("Duebel_8x30", StarterBlockDefinitions.Duebel_8x30);
        var machinings = MachiningFactory.CreateFromBlock(block!, 150, 75, 0, 19);

        Assert.Single(machinings);
        var drill = Assert.IsType<DrillMachining>(machinings[0]);
        Assert.Equal(8, drill.Diameter);
        Assert.Equal(10, drill.Depth);
        Assert.Equal(MachiningSide.Top, drill.Side);
    }

    [Fact]
    public void Duebel_8x30_Stirn_CreatesHorizontalDrillMachining()
    {
        var block = ParseBlock("Duebel_8x30_Stirn", StarterBlockDefinitions.Duebel_8x30_Stirn);
        var machinings = MachiningFactory.CreateFromBlock(block!, 150, 75, 0, 19);

        Assert.Single(machinings);
        var hdrill = Assert.IsType<HorizontalDrillMachining>(machinings[0]);
        Assert.Equal(8, hdrill.Diameter);
        Assert.Equal(30, hdrill.Depth);
        Assert.Equal('L', hdrill.DrillSide);
    }

    [Fact]
    public void CLAMEX_P14_CreatesMacroMachining()
    {
        var block = ParseBlock("CLAMEX_P14", StarterBlockDefinitions.CLAMEX_P14);
        var machinings = MachiningFactory.CreateFromBlock(block!, 100, 200, 0, 19);

        Assert.Single(machinings);
        var macro = Assert.IsType<MacroMachining>(machinings[0]);
        Assert.Equal("SawCut_Lamello", macro.MacroName);
        Assert.True(macro.Parameters.Count > 10, "CLAMEX should have many parameters");
        // First param should be {DZ}-9.5 = 19-9.5 = 9.5
        Assert.Equal("9.5", macro.Parameters[0]);
    }

    [Fact]
    public void CLAMEX_P14_MacroParams_ContainExpandedDZ()
    {
        var block = ParseBlock("CLAMEX_P14", StarterBlockDefinitions.CLAMEX_P14);
        var machinings = MachiningFactory.CreateFromBlock(block!, 50, 300, 0, 22);

        var macro = Assert.IsType<MacroMachining>(machinings[0]);
        // DZ=22: {DZ}-9.5 = 12.5
        Assert.Equal("12.5", macro.Parameters[0]);
        // {Y} = 300
        Assert.Equal("300", macro.Parameters[1]);
    }

    // --- Emitter Router Integration ---

    [Fact]
    public void Topfband_35_EmitsValidCncCode()
    {
        var block = ParseBlock("Topfband_35", StarterBlockDefinitions.Topfband_35);
        var machinings = MachiningFactory.CreateFromBlock(block!, 100, 200, 0, 19);

        var plate = new Plate
        {
            Name = "TestPlate", LengthX = 800, WidthY = 400, Thickness = 19,
            Machinings = machinings
        };

        var router = CreateRouter();
        var program = router.GenerateProgram(plate);

        Assert.Contains("CreateDrill", program);
        Assert.Contains("100", program);
        Assert.Contains("200", program);
    }

    [Fact]
    public void Lochreihe_32_EmitsValidCncCode()
    {
        var block = ParseBlock("Lochreihe_32", StarterBlockDefinitions.Lochreihe_32);
        var machinings = MachiningFactory.CreateFromBlock(block!, 37, 96, 0, 19);

        var plate = new Plate
        {
            Name = "TestPlate", LengthX = 800, WidthY = 400, Thickness = 19,
            Machinings = machinings
        };

        var router = CreateRouter();
        var program = router.GenerateProgram(plate);

        Assert.Contains("CreateDrill", program);
        Assert.Contains("CreatePattern", program);
    }

    // --- Helper Methods ---

    private static FittingBlock? ParseBlock(string name, IReadOnlyDictionary<string, string> def)
    {
        return CncUserTextParser.Parse(name, def, (100, 200, 0), 0, "TestLayer", out _);
    }

    private static IReadOnlyDictionary<string, string> GetDefinition(string name)
    {
        return name switch
        {
            "Topfband_35" => StarterBlockDefinitions.Topfband_35,
            "Lochreihe_32" => StarterBlockDefinitions.Lochreihe_32,
            "Duebel_8x30" => StarterBlockDefinitions.Duebel_8x30,
            "Duebel_8x30_Stirn" => StarterBlockDefinitions.Duebel_8x30_Stirn,
            "CLAMEX_P14" => StarterBlockDefinitions.CLAMEX_P14,
            _ => throw new ArgumentException($"Unknown block: {name}")
        };
    }

    private static Core.Pipeline.EmitterRouter CreateRouter()
    {
        var nameService = new Core.Naming.NameService();
        var emitter = new Core.Emitters.XilogEmitter(nameService);
        var profile = new Core.Profiles.MaestroCadTProfile();
        return new Core.Pipeline.EmitterRouter(emitter, nameService, profile);
    }
}
