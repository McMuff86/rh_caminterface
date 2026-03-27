using RhinoCNCExporter.Core.Models;
using Xunit;

namespace RhinoCNCExporter.Tests;

/// <summary>
/// Tests for MachiningStrategy: tool resolution, roughing pass logic, and defaults.
/// </summary>
public class MachiningStrategyTests
{
    #region CreateDefault — Basic

    [Fact]
    public void CreateDefault_RoutingMachining_HasFinishingTool()
    {
        var library = ToolLibrary.CreateDefault("xilog");
        var machining = CreateRouting(6.0);

        var strategy = MachiningStrategy.CreateDefault(machining, library);

        Assert.NotNull(strategy.FinishingTool);
        Assert.Equal(ToolKind.Router, strategy.FinishingTool.Kind);
    }

    [Fact]
    public void CreateDefault_DrillMachining_HasFinishingTool()
    {
        var library = ToolLibrary.CreateDefault("xilog");
        var machining = CreateDrillMachining(8.0);

        var strategy = MachiningStrategy.CreateDefault(machining, library);

        Assert.NotNull(strategy.FinishingTool);
        Assert.Equal(ToolKind.Drill, strategy.FinishingTool.Kind);
    }

    [Fact]
    public void CreateDefault_DrillMachining_NoRoughingPass()
    {
        var library = ToolLibrary.CreateDefault("xilog");
        var machining = CreateDrillMachining(8.0);

        var strategy = MachiningStrategy.CreateDefault(machining, library);

        Assert.Null(strategy.RoughingTool);
        Assert.False(strategy.HasRoughingPass);
    }

    #endregion

    #region Roughing Strategies

    [Fact]
    public void CreateDefault_RoutingWithRoughing_HasRoughingTool()
    {
        var library = ToolLibrary.CreateDefault("xilog");
        var machining = CreateRouting(6.0);
        var options = new ToolpathPlanningOptions { EnableRoughingStrategies = true };

        var strategy = MachiningStrategy.CreateDefault(machining, library, options);

        if (strategy.RoughingTool != null)
        {
            Assert.True(strategy.HasRoughingPass);
            Assert.True(strategy.StockToLeave > 0);
            Assert.True(strategy.RoughingTool.NominalDiameter > strategy.FinishingTool!.NominalDiameter);
        }
    }

    [Fact]
    public void CreateDefault_RoughingDisabled_NoRoughingTool()
    {
        var library = ToolLibrary.CreateDefault("xilog");
        var machining = CreateRouting(6.0);
        var options = new ToolpathPlanningOptions { EnableRoughingStrategies = false };

        var strategy = MachiningStrategy.CreateDefault(machining, library, options);

        Assert.Null(strategy.RoughingTool);
        Assert.False(strategy.HasRoughingPass);
    }

    #endregion

    #region HasRoughingPass Logic

    [Fact]
    public void HasRoughingPass_BothToolsAndStockToLeave_True()
    {
        var strategy = new MachiningStrategy
        {
            RoughingTool = CreateToolDef("r1", ToolKind.Router, 12.0),
            FinishingTool = CreateToolDef("r2", ToolKind.Router, 6.0),
            StockToLeave = 0.3
        };

        Assert.True(strategy.HasRoughingPass);
    }

    [Fact]
    public void HasRoughingPass_NoRoughingTool_False()
    {
        var strategy = new MachiningStrategy
        {
            RoughingTool = null,
            FinishingTool = CreateToolDef("r1", ToolKind.Router, 6.0),
            StockToLeave = 0.3
        };

        Assert.False(strategy.HasRoughingPass);
    }

    [Fact]
    public void HasRoughingPass_ZeroStockToLeave_False()
    {
        var strategy = new MachiningStrategy
        {
            RoughingTool = CreateToolDef("r1", ToolKind.Router, 12.0),
            FinishingTool = CreateToolDef("r2", ToolKind.Router, 6.0),
            StockToLeave = 0
        };

        Assert.False(strategy.HasRoughingPass);
    }

    #endregion

    #region Tool Override

    [Fact]
    public void CreateDefault_WithToolOverride_UsesOverriddenTool()
    {
        var library = ToolLibrary.CreateDefault("xilog");
        var machining = CreateRouting(6.0);
        var overrides = new MachiningToolOverride
        {
            OperationKey = "test",
            FinishingToolId = "scm_router_3"
        };
        var options = new ToolpathPlanningOptions
        {
            EnableRoughingStrategies = false,
            StrategyOverrides = new[] { overrides }
        };

        var strategy = MachiningStrategy.CreateDefault(machining, library, options, overrides);

        Assert.NotNull(strategy.FinishingTool);
        Assert.Equal("scm_router_3", strategy.FinishingTool.Id);
    }

    [Fact]
    public void CreateDefault_IncompatibleToolOverride_FallsBackToSuggested()
    {
        var library = ToolLibrary.CreateDefault("xilog");
        var machining = CreateDrillMachining(8.0);
        var overrides = new MachiningToolOverride
        {
            OperationKey = "test",
            FinishingToolId = "scm_router_12" // Router not compatible with drill
        };

        var strategy = MachiningStrategy.CreateDefault(machining, library, options: null, overrides);

        // Should fall back to suggested drill, not use the router override
        Assert.NotNull(strategy.FinishingTool);
        Assert.Equal(ToolKind.Drill, strategy.FinishingTool.Kind);
    }

    #endregion

    #region ToolpathPlanningOptions

    [Fact]
    public void ToolpathPlanningOptions_Defaults()
    {
        var options = new ToolpathPlanningOptions();

        Assert.True(options.IncludeRapidMoves);
        Assert.True(options.EnableRoughingStrategies);
        Assert.Equal(0.3, options.DefaultStockToLeave);
        Assert.Empty(options.StrategyOverrides);
    }

    [Fact]
    public void ToolpathPlanningOptions_FindOverride_CaseInsensitive()
    {
        var options = new ToolpathPlanningOptions
        {
            StrategyOverrides = new[]
            {
                new MachiningToolOverride { OperationKey = "TestOp", FinishingToolId = "t1" }
            }
        };

        Assert.NotNull(options.FindOverride("testop"));
        Assert.Null(options.FindOverride("nonexistent"));
    }

    #endregion

    #region MachiningToolOverride

    [Fact]
    public void MachiningToolOverride_HasOverride_WhenToolIdSet()
    {
        var o = new MachiningToolOverride
        {
            OperationKey = "test",
            FinishingToolId = "t1"
        };
        Assert.True(o.HasOverride);
    }

    [Fact]
    public void MachiningToolOverride_HasOverride_WhenEmpty_ReturnsFalse()
    {
        var o = new MachiningToolOverride { OperationKey = "test" };
        Assert.False(o.HasOverride);
    }

    #endregion

    #region Helpers

    private static RoutingMachining CreateRouting(double toolDiameter) => new()
    {
        Name = "Test Route",
        Points = new[] { (0.0, 0.0), (100.0, 0.0), (100.0, 100.0) },
        Depth = 19,
        ToolDiameter = toolDiameter,
        IsClosed = false
    };

    private static DrillMachining CreateDrillMachining(double diameter) => new()
    {
        Name = "Test Drill",
        X = 50, Y = 50, Depth = 13, Diameter = diameter
    };

    private static ToolDefinition CreateToolDef(string id, ToolKind kind, double diameter) => new()
    {
        Id = id,
        Name = $"Test {kind} {diameter}",
        Kind = kind,
        NominalDiameter = diameter
    };

    #endregion
}
