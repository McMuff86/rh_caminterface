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
    public void ToolLibrary_RoundTripsJson()
    {
        var library = ToolLibrary.CreateDefault("xilog");

        var roundTripped = ToolLibrary.FromJson(library.ToJson());

        Assert.Equal(library.Name, roundTripped.Name);
        Assert.Equal(library.MachineKey, roundTripped.MachineKey);
        Assert.Equal(library.Tools.Count, roundTripped.Tools.Count);
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
}
