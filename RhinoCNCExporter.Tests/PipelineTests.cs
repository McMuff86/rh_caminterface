using RhinoCNCExporter.Core.Emitters;
using RhinoCNCExporter.Core.Models;
using RhinoCNCExporter.Core.Naming;
using RhinoCNCExporter.Core.Pipeline;
using RhinoCNCExporter.Core.Profiles;
using Xunit;

namespace RhinoCNCExporter.Tests;

public class MachiningBuilderTests
{
    private readonly MachiningBuilder _builder = new();

    [Fact]
    public void MergeAndDeduplicate_NoOverlap_ReturnsBoth()
    {
        var legacy = new Machining[]
        {
            new DrillMachining { Name = "D1", X = 10, Y = 10, Depth = 13, Diameter = 5 }
        };
        var blocks = new Machining[]
        {
            new DrillMachining { Name = "D2", X = 200, Y = 200, Depth = 13, Diameter = 35, Source = MachiningSource.BlockDetection }
        };

        var result = _builder.MergeAndDeduplicate(legacy, blocks);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void MergeAndDeduplicate_SamePosition_BlockWins()
    {
        var legacy = new Machining[]
        {
            new DrillMachining { Name = "Legacy_D1", X = 100, Y = 200, Depth = 13, Diameter = 5 }
        };
        var blocks = new Machining[]
        {
            new DrillMachining { Name = "Block_D1", X = 100.1, Y = 200.1, Depth = 13, Diameter = 35, Source = MachiningSource.BlockDetection }
        };

        var result = _builder.MergeAndDeduplicate(legacy, blocks, positionTolerance: 0.5);

        Assert.Single(result);
        Assert.Equal("Block_D1", result[0].Name); // Block wins
    }

    [Fact]
    public void MergeAndDeduplicate_DifferentTypes_NoDeduplicate()
    {
        // A drill and a drill pattern at same position should NOT deduplicate
        var legacy = new Machining[]
        {
            new DrillMachining { Name = "D1", X = 100, Y = 200, Depth = 13, Diameter = 5 }
        };
        var blocks = new Machining[]
        {
            new DrillPatternMachining { Name = "DP1", X = 100, Y = 200, Depth = 13, Diameter = 5,
                CountX = 1, CountY = 5, SpacingX = 0, SpacingY = 32, Source = MachiningSource.BlockDetection }
        };

        var result = _builder.MergeAndDeduplicate(legacy, blocks);
        Assert.Equal(2, result.Count); // Both kept
    }

    [Fact]
    public void MergeAndDeduplicate_EmptyInputs()
    {
        var result = _builder.MergeAndDeduplicate(
            Array.Empty<Machining>(), Array.Empty<Machining>());
        Assert.Empty(result);
    }

    [Fact]
    public void MergeAndDeduplicate_OnlyLegacy()
    {
        var legacy = new Machining[]
        {
            new DrillMachining { Name = "D1", X = 10, Y = 10, Depth = 5, Diameter = 5 },
            new DrillMachining { Name = "D2", X = 20, Y = 20, Depth = 5, Diameter = 5 }
        };

        var result = _builder.MergeAndDeduplicate(legacy, Array.Empty<Machining>());
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void MergeAndDeduplicate_OnlyBlocks()
    {
        var blocks = new Machining[]
        {
            new DrillMachining { Name = "D1", X = 10, Y = 10, Depth = 5, Diameter = 5, Source = MachiningSource.BlockDetection }
        };

        var result = _builder.MergeAndDeduplicate(Array.Empty<Machining>(), blocks);
        Assert.Single(result);
    }

    [Fact]
    public void MergeAndDeduplicate_RoutingNeverDeduplicates()
    {
        // Routing machinings don't have simple X/Y position → never deduplicate
        var legacy = new Machining[]
        {
            new RoutingMachining
            {
                Name = "Cut1",
                Points = new[] { (0.0, 0.0), (100.0, 0.0) },
                Depth = 19, ToolDiameter = 9.5
            }
        };
        var blocks = new Machining[]
        {
            new RoutingMachining
            {
                Name = "Cut2",
                Points = new[] { (0.0, 0.0), (100.0, 0.0) },
                Depth = 19, ToolDiameter = 9.5,
                Source = MachiningSource.BlockDetection
            }
        };

        var result = _builder.MergeAndDeduplicate(legacy, blocks);
        Assert.Equal(2, result.Count); // Both kept (no position-based dedup for routing)
    }
}

public class EmitterRouterTests
{
    private EmitterRouter CreateRouter()
    {
        var emitter = new XilogEmitter(new NameService());
        var nameService = new NameService();
        var profile = new MaestroCadTProfile();
        return new EmitterRouter(emitter, nameService, profile);
    }

    [Fact]
    public void GenerateProgram_EmptyPlate_HeaderAndFooter()
    {
        var router = CreateRouter();
        var plate = new Plate
        {
            Name = "TestPlate",
            LengthX = 800, WidthY = 400, Thickness = 19
        };

        var result = router.GenerateProgram(plate);

        Assert.Contains("TestPlate", result);
        Assert.Contains("800", result);
        Assert.Contains("400", result);
    }

    [Fact]
    public void GenerateProgram_WithDrill_ContainsDrillOutput()
    {
        var router = CreateRouter();
        var plate = new Plate
        {
            Name = "TestPlate",
            LengthX = 800, WidthY = 400, Thickness = 19,
            Machinings = new Machining[]
            {
                new DrillMachining { Name = "Topfband_35", X = 100, Y = 200, Depth = 13, Diameter = 35 }
            }
        };

        var result = router.GenerateProgram(plate);
        Assert.Contains("CreateDrill", result);
    }

    [Fact]
    public void GenerateProgram_WithDrillPattern_ContainsPatternOutput()
    {
        var router = CreateRouter();
        var plate = new Plate
        {
            Name = "TestPlate",
            LengthX = 800, WidthY = 400, Thickness = 19,
            Machinings = new Machining[]
            {
                new DrillPatternMachining
                {
                    Name = "Lochreihe_32",
                    X = 37, Y = 96, Depth = 13, Diameter = 5,
                    CountX = 1, CountY = 10, SpacingX = 0, SpacingY = 32
                }
            }
        };

        var result = router.GenerateProgram(plate);
        Assert.Contains("CreateDrill", result);
        Assert.Contains("CreatePattern", result);
    }

    [Fact]
    public void GenerateProgram_WithRouting_ContainsPolyline()
    {
        var router = CreateRouter();
        var plate = new Plate
        {
            Name = "TestPlate",
            LengthX = 800, WidthY = 400, Thickness = 19,
            Machinings = new Machining[]
            {
                new RoutingMachining
                {
                    Name = "Cut1",
                    Points = new[] { (0.0, 0.0), (100.0, 0.0), (100.0, 50.0), (0.0, 50.0), (0.0, 0.0) },
                    Depth = 19, ToolDiameter = 9.5, IsClosed = true
                }
            }
        };

        var result = router.GenerateProgram(plate);
        Assert.Contains("CreatePolyline", result);
    }

    [Fact]
    public void GenerateProgram_WithMacro_ContainsMacroComment()
    {
        var router = CreateRouter();
        var plate = new Plate
        {
            Name = "TestPlate",
            LengthX = 800, WidthY = 400, Thickness = 19,
            Machinings = new Machining[]
            {
                new MacroMachining
                {
                    Name = "CLAMEX_P14",
                    MacroName = "SawCut_Lamello",
                    Parameters = new[] { "9.5", "100", null, "0" }
                }
            }
        };

        var result = router.GenerateProgram(plate);
        Assert.Contains("MACRO: SawCut_Lamello", result);
        Assert.Contains("4 params", result);
    }

    [Fact]
    public void GenerateProgram_WithHorizontalDrill_ContainsWorkplane()
    {
        var router = CreateRouter();
        var plate = new Plate
        {
            Name = "TestPlate",
            LengthX = 800, WidthY = 400, Thickness = 19,
            Machinings = new Machining[]
            {
                new HorizontalDrillMachining
                {
                    Name = "HDrill_L",
                    X = 50, Y = 100, Depth = 30, Diameter = 8,
                    DrillSide = 'L', Side = MachiningSide.Left
                }
            }
        };

        var result = router.GenerateProgram(plate);
        Assert.Contains("CreateWorkplane", result);
        Assert.Contains("SelectWorkplane", result);
        Assert.Contains("CreateDrill", result);
    }

    [Fact]
    public void GenerateProgram_WithGrooveRnt_ContainsRnt()
    {
        var router = CreateRouter();
        var plate = new Plate
        {
            Name = "TestPlate",
            LengthX = 800, WidthY = 400, Thickness = 19,
            Machinings = new Machining[]
            {
                new GrooveRntMachining
                {
                    Name = "RNT_1",
                    Axis = Core.LayerParser.Axis.X,
                    XStart = 10, YStart = 200, Length = 780, Width = 5.5,
                    Depth = 8.3, RntCode = "066"
                }
            }
        };

        var result = router.GenerateProgram(plate);
        Assert.Contains("RNT", result);
    }

    [Fact]
    public void GenerateProgram_MultipleOps_OrderedCorrectly()
    {
        var router = CreateRouter();
        var plate = new Plate
        {
            Name = "TestPlate",
            LengthX = 800, WidthY = 400, Thickness = 19,
            Machinings = new Machining[]
            {
                // Intentionally out of order
                new MacroMachining { Name = "Macro1", MacroName = "Test", Parameters = Array.Empty<string?>() },
                new DrillMachining { Name = "Drill1", X = 50, Y = 100, Depth = 13, Diameter = 5 },
                new RoutingMachining
                {
                    Name = "Contour1",
                    Points = new[] { (0.0, 0.0), (100.0, 0.0), (100.0, 50.0), (0.0, 0.0) },
                    Depth = 19, ToolDiameter = 9.5, IsClosed = true
                }
            }
        };

        var result = router.GenerateProgram(plate);

        // Contour (routing closed) should come before drill, which comes before macro
        var contourIdx = result.IndexOf("CreatePolyline");
        var drillIdx = result.IndexOf("CreateDrill");
        var macroIdx = result.IndexOf("MACRO:");

        Assert.True(contourIdx < drillIdx, "Contour should come before drill");
        Assert.True(drillIdx < macroIdx, "Drill should come before macro");
    }

    [Fact]
    public void OrderMachinings_SortsCorrectly()
    {
        var machinings = new Machining[]
        {
            new MacroMachining { Name = "M1", MacroName = "X", Parameters = Array.Empty<string?>() },
            new DrillMachining { Name = "D1", X = 0, Y = 0, Depth = 5, Diameter = 5 },
            new RoutingMachining { Name = "R1", Points = new[] { (0.0, 0.0), (1.0, 1.0) }, Depth = 5, ToolDiameter = 5, IsClosed = true },
            new DrillPatternMachining { Name = "DP1", X = 0, Y = 0, Depth = 5, Diameter = 5, CountX = 1, CountY = 1, SpacingX = 0, SpacingY = 0 }
        };

        var ordered = EmitterRouter.OrderMachinings(machinings).ToList();
        Assert.IsType<RoutingMachining>(ordered[0]);
        Assert.IsType<DrillMachining>(ordered[1]);
        Assert.IsType<DrillPatternMachining>(ordered[2]);
        Assert.IsType<MacroMachining>(ordered[3]);
    }
}
