using RhinoCNCExporter.Core.Blocks;
using RhinoCNCExporter.Core.Blocks.StarterBlocks;
using RhinoCNCExporter.Core.Emitters;
using RhinoCNCExporter.Core.Models;
using RhinoCNCExporter.Core.Naming;
using RhinoCNCExporter.Core.Pipeline;
using RhinoCNCExporter.Core.Profiles;
using Xunit;

namespace RhinoCNCExporter.Tests;

/// <summary>
/// Integration tests: Starter Block → Parse → Factory → Builder → EmitterRouter → CNC output.
/// Full pipeline without RhinoCommon.
/// </summary>
public class BlockIntegrationTests
{
    // --- Full Pipeline: Block → CNC Code ---

    [Fact]
    public void FullPipeline_Topfband_ProducesValidXCS()
    {
        var block = ParseStarterBlock("Topfband_35", StarterBlockDefinitions.Topfband_35, 150, 250);
        var machinings = MachiningFactory.CreateFromBlock(block!, 150, 250, 0, 19);

        var plate = CreatePlateWithMachinings("Seite_links", 800, 400, 19, machinings);
        var program = GenerateProgram(plate);

        Assert.Contains("Seite_links", program);
        Assert.Contains("CreateDrill", program);
        Assert.Contains("800", program);
        Assert.Contains("400", program);
    }

    [Fact]
    public void FullPipeline_Lochreihe_ProducesPatternAndDrill()
    {
        var block = ParseStarterBlock("Lochreihe_32", StarterBlockDefinitions.Lochreihe_32, 37, 96);
        var machinings = MachiningFactory.CreateFromBlock(block!, 37, 96, 0, 19);

        var plate = CreatePlateWithMachinings("Seite_links", 800, 400, 19, machinings);
        var program = GenerateProgram(plate);

        Assert.Contains("CreateDrill", program);
        Assert.Contains("CreatePattern", program);
    }

    [Fact]
    public void FullPipeline_MultipleDifferentBlocks_AllEmitted()
    {
        var allMachinings = new List<Machining>();

        // Topfband
        var topfband = ParseStarterBlock("Topfband_35", StarterBlockDefinitions.Topfband_35, 100, 200);
        allMachinings.AddRange(MachiningFactory.CreateFromBlock(topfband!, 100, 200, 0, 19));

        // Lochreihe
        var lochreihe = ParseStarterBlock("Lochreihe_32", StarterBlockDefinitions.Lochreihe_32, 37, 96);
        allMachinings.AddRange(MachiningFactory.CreateFromBlock(lochreihe!, 37, 96, 0, 19));

        // Dübel
        var duebel = ParseStarterBlock("Duebel_8x30", StarterBlockDefinitions.Duebel_8x30, 200, 300);
        allMachinings.AddRange(MachiningFactory.CreateFromBlock(duebel!, 200, 300, 0, 19));

        var plate = CreatePlateWithMachinings("TestPlate", 800, 400, 19, allMachinings);
        var program = GenerateProgram(plate);

        // Should have: 2 drills (Topfband + Dübel) + 1 pattern (Lochreihe)
        var drillCount = CountOccurrences(program, "CreateDrill");
        var patternCount = CountOccurrences(program, "CreatePattern");

        Assert.True(drillCount >= 2, $"Expected at least 2 CreateDrill calls, got {drillCount}");
        Assert.True(patternCount >= 1, $"Expected at least 1 CreatePattern call, got {patternCount}");
    }

    // --- MachiningBuilder: Block + Legacy Merge ---

    [Fact]
    public void MergeAndDeduplicate_BlockAndLegacy_BlockWinsAtSamePosition()
    {
        var builder = new MachiningBuilder();

        var legacyDrill = new DrillMachining
        {
            Name = "Legacy_D5", X = 100, Y = 200, Depth = 13, Diameter = 5,
            Source = MachiningSource.LegacyLayer
        };

        var blockDrill = new DrillMachining
        {
            Name = "Topfband_35", X = 100.1, Y = 200.1, Depth = 13, Diameter = 35,
            Source = MachiningSource.BlockDetection
        };

        var result = builder.MergeAndDeduplicate(
            new[] { legacyDrill },
            new[] { blockDrill });

        // Block should win, legacy dropped (within tolerance)
        Assert.Single(result);
        Assert.Equal("Topfband_35", result[0].Name);
    }

    [Fact]
    public void MergeAndDeduplicate_LegacyCutPlusBlockDrill_BothKept()
    {
        var builder = new MachiningBuilder();

        var legacyCut = new RoutingMachining
        {
            Name = "Aussenkontur", Points = new[] { (0.0, 0.0), (800.0, 0.0), (800.0, 400.0), (0.0, 400.0), (0.0, 0.0) },
            Depth = 19, ToolDiameter = 9.5, IsClosed = true,
            Source = MachiningSource.LegacyLayer
        };

        var blockDrill = new DrillMachining
        {
            Name = "Topfband_35", X = 100, Y = 200, Depth = 13, Diameter = 35,
            Source = MachiningSource.BlockDetection
        };

        var result = builder.MergeAndDeduplicate(
            new Machining[] { legacyCut },
            new Machining[] { blockDrill });

        // Both kept: different types, routing doesn't have position-based dedup
        Assert.Equal(2, result.Count);
    }

    // --- EmitterRouter: Ordering with mixed sources ---

    [Fact]
    public void EmitterRouter_MixedSources_OrderedCorrectly()
    {
        var machinings = new Machining[]
        {
            new MacroMachining { Name = "CLAMEX", MacroName = "SawCut_Lamello", Parameters = new[] { "9.5" }, Source = MachiningSource.BlockDetection },
            new DrillMachining { Name = "Topfband", X = 100, Y = 200, Depth = 13, Diameter = 35, Source = MachiningSource.BlockDetection },
            new DrillMachining { Name = "Legacy_D5", X = 50, Y = 50, Depth = 13, Diameter = 5, Source = MachiningSource.LegacyLayer },
            new DrillPatternMachining { Name = "Lochreihe", X = 37, Y = 96, Depth = 13, Diameter = 5,
                CountX = 1, CountY = 10, SpacingX = 0, SpacingY = 32, Source = MachiningSource.BlockDetection },
        };

        var ordered = EmitterRouter.OrderMachinings(machinings).ToList();

        // Drills first (2), then DrillPattern (1), then Macro (1)
        Assert.IsType<DrillMachining>(ordered[0]);
        Assert.IsType<DrillMachining>(ordered[1]);
        Assert.IsType<DrillPatternMachining>(ordered[2]);
        Assert.IsType<MacroMachining>(ordered[3]);
    }

    // --- Edge Cases ---

    [Fact]
    public void EmptyBlockList_ProducesEmptyMachinings()
    {
        var blocks = Array.Empty<FittingBlock>();
        var allMachinings = new List<Machining>();

        foreach (var block in blocks)
        {
            allMachinings.AddRange(MachiningFactory.CreateFromBlock(block, 0, 0, 0, 19));
        }

        Assert.Empty(allMachinings);
    }

    [Fact]
    public void InvalidBlock_ProducesEmptyMachinings()
    {
        // Block with unknown CNC_Type
        var block = CncUserTextParser.Parse("Invalid_Block",
            new Dictionary<string, string> { ["CNC_Type"] = "INVALID" },
            (0, 0, 0), 0, "Test", out _);

        // Parser should reject unknown types
        Assert.Null(block);
    }

    [Fact]
    public void MultipleBlocksSamePosition_AllCreated()
    {
        // Two different blocks at the same position (e.g., Topfband + Lochreihe on same spot)
        var topfband = ParseStarterBlock("Topfband_35", StarterBlockDefinitions.Topfband_35, 100, 200);
        var lochreihe = ParseStarterBlock("Lochreihe_32", StarterBlockDefinitions.Lochreihe_32, 100, 200);

        var m1 = MachiningFactory.CreateFromBlock(topfband!, 100, 200, 0, 19);
        var m2 = MachiningFactory.CreateFromBlock(lochreihe!, 100, 200, 0, 19);

        // Each creates its own machining
        Assert.Single(m1);
        Assert.Single(m2);
        Assert.IsType<DrillMachining>(m1[0]);
        Assert.IsType<DrillPatternMachining>(m2[0]);
    }

    // --- Helpers ---

    private static FittingBlock? ParseStarterBlock(string name, IReadOnlyDictionary<string, string> def, double x, double y)
        => CncUserTextParser.Parse(name, def, (x, y, 0), 0, "TestLayer", out _);

    private static Plate CreatePlateWithMachinings(string name, double lx, double ly, double t, IReadOnlyList<Machining> machinings)
        => new() { Name = name, LengthX = lx, WidthY = ly, Thickness = t, Machinings = machinings };

    private static string GenerateProgram(Plate plate)
    {
        var nameService = new NameService();
        var emitter = new XilogEmitter(nameService);
        var profile = new MaestroCadTProfile();
        var router = new EmitterRouter(emitter, nameService, profile);
        return router.GenerateProgram(plate);
    }

    private static int CountOccurrences(string text, string search)
    {
        int count = 0, index = 0;
        while ((index = text.IndexOf(search, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += search.Length;
        }
        return count;
    }
}
