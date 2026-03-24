using RhinoCNCExporter.Core.Models;
using RhinoCNCExporter.Core.Pipeline;
using RhinoCNCExporter.Core.Profiles;
using Xunit;

namespace RhinoCNCExporter.Tests;

public class ToolLibraryTests
{
    [Fact]
    public void CreateDefault_Xilog_ContainsExpectedRouterAndDrillTools()
    {
        var library = ToolLibrary.CreateDefault("xilog");

        Assert.Contains(library.Tools, tool => tool.TechCode == "E010" && tool.Kind == ToolKind.Router);
        Assert.Contains(library.Tools, tool => tool.TechCode == "D5" && tool.Kind == ToolKind.Drill);
        Assert.Contains(library.Holders, holder => holder.Id == "scm_er32_collet");
        Assert.Contains(library.Tools, tool => tool.HolderId == "scm_er32_collet");
        Assert.Contains(library.Holders, holder => holder.Name.Contains("HSK63F", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(library.Holders, holder => holder.Id == "scm_vertical_drill_bank" && holder.Name.Contains("Bohraggregat", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(library.Tools, tool =>
            tool.TechCode == "D5"
            && tool.MotionProfile == ToolMotionProfile.PointOnly
            && tool.IsFixedAggregate);
        Assert.Contains(library.Tools, tool =>
            tool.TechCode == "RNT066"
            && tool.Name.Contains("Rueckwandnuter", StringComparison.OrdinalIgnoreCase)
            && tool.MotionProfile == ToolMotionProfile.LinearXyOnly
            && tool.IsFixedAggregate);
    }

    [Fact]
    public void SuggestTool_RoutingPrefersMatchingTechCode()
    {
        var library = ToolLibrary.CreateDefault("xilog");
        var machining = new RoutingMachining
        {
            Name = "Contour",
            Points = new[] { (0.0, 0.0), (100.0, 0.0), (0.0, 0.0) },
            Depth = 19,
            ToolDiameter = 9.5,
            TechCode = "E010",
            IsClosed = true
        };

        var tool = library.SuggestTool(machining, new MaestroCadTProfile());

        Assert.NotNull(tool);
        Assert.Equal("E010", tool!.TechCode);
        Assert.Equal(9.5, tool.NominalDiameter, 3);
    }

    [Fact]
    public void SuggestTool_GrooveRntPrefersRueckwandnuterSaw()
    {
        var library = ToolLibrary.CreateDefault("xilog");
        var machining = new GrooveRntMachining
        {
            Name = "Rueckwandnut",
            Axis = RhinoCNCExporter.Core.LayerParser.Axis.X,
            XStart = 10,
            YStart = 60,
            Length = 500,
            Width = 5.5,
            Depth = 8.3,
            RntCode = "066"
        };

        var tool = library.SuggestTool(machining, new MaestroCadTProfile());

        Assert.NotNull(tool);
        Assert.Equal(ToolKind.Saw, tool!.Kind);
        Assert.Equal("RNT066", tool.TechCode);
        Assert.True(tool.IsFixedAggregate);
    }

    [Fact]
    public void ToolLibrary_RoundTripsJson()
    {
        var library = ToolLibrary.CreateDefault("xilog").AddOrUpdate(new ToolDefinition
        {
            Id = "radius_tool",
            Name = "Radius Tool",
            Kind = ToolKind.Router,
            HolderId = "scm_er32_collet",
            TechCode = "ER_TEST",
            NominalDiameter = 12.0,
            CornerRadius = 1.5,
            Material = ToolMaterial.Carbide
        });

        var roundTripped = ToolLibrary.FromJson(library.ToJson());

        Assert.Equal(library.Name, roundTripped.Name);
        Assert.Equal(library.MachineKey, roundTripped.MachineKey);
        Assert.Equal(library.Holders.Count, roundTripped.Holders.Count);
        Assert.Equal(library.Tools.Count, roundTripped.Tools.Count);
        Assert.All(roundTripped.Tools, tool => Assert.Equal(ToolMaterial.Carbide, tool.Material));
        Assert.Contains(roundTripped.Tools, tool => tool.Id == "radius_tool" && tool.CornerRadius == 1.5);
    }

    [Fact]
    public void RemoveHolder_ClearsAssignmentsOnReferencedTools()
    {
        var library = ToolLibrary.CreateDefault("xilog");

        var updated = library.RemoveHolder("scm_er32_collet");

        Assert.DoesNotContain(updated.Holders, holder => holder.Id == "scm_er32_collet");
        Assert.DoesNotContain(updated.Tools, tool => tool.HolderId == "scm_er32_collet");
    }

    [Fact]
    public void FromJson_LegacyPayloadWithoutHolders_RemainsCompatible()
    {
        const string json = """
            {
              "name": "Legacy Tools",
              "machineKey": "xilog",
              "tools": [
                {
                  "id": "legacy_router",
                  "name": "Legacy Router",
                  "kind": "Router",
                  "nominalDiameter": 8.0,
                  "techCode": "E008"
                }
              ]
            }
            """;

        var library = ToolLibrary.FromJson(json);
        var tool = Assert.Single(library.Tools);

        Assert.Empty(library.Holders);
        Assert.Equal("legacy_router", tool.Id);
        Assert.Null(tool.HolderId);
        Assert.Equal(ToolMaterial.Carbide, tool.Material);
    }

    [Fact]
    public void MergeDefaults_BackfillsLegacyLibrariesWithHoldersAndAssignments()
    {
        var defaults = ToolLibrary.CreateDefault("xilog");
        var legacy = new ToolLibrary
        {
            Name = "Legacy Tools",
            MachineKey = "xilog",
            Holders = new[]
            {
                new ToolHolderDefinition
                {
                    Id = "scm_er32_collet",
                    Name = "SCM ER32 Spannzange",
                    Kind = HolderKind.ColletChuck,
                    GaugeLength = 120,
                    GaugeDiameter = 48,
                    ProjectionLength = 65
                }
            },
            Tools = defaults.Tools
                .Select(tool =>
                {
                    var legacyTool = tool with
                    {
                        HolderId = null,
                        ShankDiameter = null,
                        FluteCount = null,
                        DefaultStepOver = null,
                        PlungeFeedRate = null,
                        MotionProfile = ToolMotionProfile.Freeform2D,
                        IsFixedAggregate = false
                    };

                    return tool.Id == "scm_saw_5_5"
                        ? legacyTool with { Name = "SCM Saw 5.5mm" }
                        : legacyTool;
                })
                .ToArray()
        };

        var merged = legacy.MergeDefaults(defaults);

        Assert.NotEmpty(merged.Holders);
        Assert.All(merged.Tools, tool => Assert.False(string.IsNullOrWhiteSpace(tool.HolderId)));
        Assert.All(merged.Tools.Where(tool => tool.Kind == ToolKind.Router), tool => Assert.True(tool.DefaultStepOver > 0));
        Assert.Contains(merged.Holders, holder => holder.Id == "scm_er32_collet" && holder.Name.Contains("HSK63F", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(merged.Tools, tool =>
            tool.Id == "scm_saw_5_5"
            && tool.Name.Contains("Rueckwandnuter", StringComparison.OrdinalIgnoreCase)
            && tool.MotionProfile == ToolMotionProfile.LinearXyOnly
            && tool.IsFixedAggregate);
    }
}

public class ToolpathPlannerTests
{
    [Fact]
    public void MachiningStrategy_CreateDefault_RoutingGetsRoughingAndFinishingTools()
    {
        var library = ToolLibrary.CreateDefault("xilog");
        var machining = new RoutingMachining
        {
            Name = "Contour",
            Points = new[] { (0.0, 0.0), (100.0, 0.0), (0.0, 0.0) },
            Depth = 19,
            ToolDiameter = 9.5,
            TechCode = "E010",
            IsClosed = true
        };

        var strategy = MachiningStrategy.CreateDefault(machining, library);

        Assert.True(strategy.HasRoughingPass);
        Assert.Equal("E010", strategy.FinishingTool?.TechCode);
        Assert.Equal("E013", strategy.RoughingTool?.TechCode);
    }

    [Fact]
    public void MachiningStrategy_CreateDefault_GrooveRntUsesSawWithoutRoughingPass()
    {
        var library = ToolLibrary.CreateDefault("xilog");
        var machining = new GrooveRntMachining
        {
            Name = "Rueckwandnut",
            Axis = RhinoCNCExporter.Core.LayerParser.Axis.Y,
            XStart = 50,
            YStart = 20,
            Length = 700,
            Width = 5.5,
            Depth = 8,
            RntCode = "066"
        };

        var strategy = MachiningStrategy.CreateDefault(machining, library);

        Assert.False(strategy.HasRoughingPass);
        Assert.Equal("RNT066", strategy.FinishingTool?.TechCode);
        Assert.Null(strategy.RoughingTool);
    }

    [Fact]
    public void PlanPlate_CreatesRapidAndRoughFinishOperations()
    {
        var library = ToolLibrary.CreateDefault("xilog");
        var plate = new Plate
        {
            Name = "PlanPlate",
            LengthX = 800,
            WidthY = 400,
            Thickness = 19,
            Machinings = new Machining[]
            {
                new RoutingMachining
                {
                    Name = "Contour",
                    Points = new[] { (0.0, 0.0), (100.0, 0.0), (100.0, 50.0), (0.0, 0.0) },
                    Depth = 19,
                    ToolDiameter = 9.5,
                    TechCode = "E010",
                    IsClosed = true
                },
                new DrillMachining
                {
                    Name = "Drill",
                    X = 250,
                    Y = 120,
                    Depth = 13,
                    Diameter = 5
                }
            }
        };

        var plan = ToolpathPlanner.PlanPlate(plate, library);

        Assert.Contains(plan.Operations, operation => operation.PassType == ToolpathPassType.Roughing);
        Assert.Contains(plan.Operations, operation => operation.PassType == ToolpathPassType.Finishing);
        Assert.Contains(plan.Operations, operation => operation.PassType == ToolpathPassType.Rapid);
        Assert.Contains(plan.Operations, operation => operation.PassType == ToolpathPassType.Drill);
    }

    [Fact]
    public void PlanPlate_DrillPatternProducesOneCirclePerHole()
    {
        var library = ToolLibrary.CreateDefault("xilog");
        var plate = new Plate
        {
            Name = "PatternPlate",
            LengthX = 400,
            WidthY = 200,
            Thickness = 19,
            Machinings = new Machining[]
            {
                new DrillPatternMachining
                {
                    Name = "Row",
                    X = 37,
                    Y = 50,
                    Depth = 12,
                    Diameter = 5,
                    CountX = 2,
                    CountY = 3,
                    SpacingX = 32,
                    SpacingY = 32
                }
            }
        };

        var plan = ToolpathPlanner.PlanPlate(plate, library);
        var operation = Assert.Single(plan.Operations);

        Assert.Equal(ToolpathPassType.Drill, operation.PassType);
        Assert.Equal(6, operation.Primitives.OfType<ToolpathCirclePrimitive>().Count());
    }

    [Fact]
    public void PlanPlate_MacroUsesApproximatePreviewGeometry()
    {
        var library = ToolLibrary.CreateDefault("xilog");
        var plate = new Plate
        {
            Name = "MacroPlate",
            LengthX = 600,
            WidthY = 300,
            Thickness = 19,
            Machinings = new Machining[]
            {
                new MacroMachining
                {
                    Name = "Clamex",
                    MacroName = "SawCut_Lamello",
                    Parameters = new[] { "100", "120", "100", "120", "90", null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, "270" }
                }
            }
        };

        var plan = ToolpathPlanner.PlanPlate(plate, library);
        var operation = Assert.Single(plan.Operations);

        Assert.Equal(ToolpathPassType.Macro, operation.PassType);
        Assert.Contains(operation.Primitives, primitive => primitive is ToolpathCirclePrimitive);
        Assert.Contains(operation.Primitives, primitive => primitive is ToolpathLinePrimitive);
    }

    [Fact]
    public void PlanPlate_GrooveRntUsesSawToolAndSingleFeedPass()
    {
        var library = ToolLibrary.CreateDefault("xilog");
        var plate = new Plate
        {
            Name = "GroovePlate",
            LengthX = 800,
            WidthY = 400,
            Thickness = 19,
            Machinings = new Machining[]
            {
                new GrooveRntMachining
                {
                    Name = "Rueckwandnut",
                    Axis = RhinoCNCExporter.Core.LayerParser.Axis.X,
                    XStart = 15,
                    YStart = 45,
                    Length = 600,
                    Width = 5.5,
                    Depth = 8.3,
                    RntCode = "066"
                }
            }
        };

        var plan = ToolpathPlanner.PlanPlate(plate, library);
        var operation = Assert.Single(plan.Operations);

        Assert.Equal(ToolpathPassType.Feed, operation.PassType);
        Assert.Equal("RNT066", operation.Tool?.TechCode);
        Assert.IsType<ToolpathLinePrimitive>(Assert.Single(operation.Primitives));
    }
}
