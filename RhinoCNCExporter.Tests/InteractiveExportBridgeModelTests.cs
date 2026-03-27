using RhinoCNCExporter.Core.Models;
using Xunit;

namespace RhinoCNCExporter.Tests;

/// <summary>
/// Tests for InteractiveExportBridge models and statistics.
/// Validates pure logic that doesn't require RhinoCommon runtime.
/// </summary>
public class InteractiveExportBridgeTests
{
    #region OperationStatistics

    [Fact]
    public void OperationStatistics_DefaultValues()
    {
        var stats = new OperationStatistics();

        Assert.Equal(0, stats.TotalOperations);
        Assert.Equal(0, stats.ContourCount);
        Assert.Equal(0, stats.PocketCount);
        Assert.Equal(0, stats.DrillCount);
        Assert.Equal(0, stats.GrooveCount);
        Assert.Equal(0, stats.ToolChanges);
        Assert.Equal(0, stats.MaxDepth);
        Assert.Equal(0, stats.EstimatedTimeMinutes);
    }

    [Fact]
    public void OperationStatistics_FormatSummary_EmptyStats()
    {
        var stats = new OperationStatistics();
        var summary = stats.FormatSummary();

        Assert.Contains("0 Op.", summary);
        Assert.Contains("0 Werkzeugwechsel", summary);
    }

    [Fact]
    public void OperationStatistics_FormatSummary_WithContours()
    {
        var stats = new OperationStatistics
        {
            TotalOperations = 3,
            ContourCount = 3,
            ToolChanges = 0,
            MaxDepth = 19.0,
            EstimatedTimeMinutes = 2.5
        };

        var summary = stats.FormatSummary();

        Assert.Contains("3 Op.", summary);
        Assert.Contains("3× Contour", summary);
        Assert.Contains("Max. Tiefe: 19.0mm", summary);
        Assert.Contains("~2.5 min", summary);
    }

    [Fact]
    public void OperationStatistics_FormatSummary_MixedTypes()
    {
        var stats = new OperationStatistics
        {
            TotalOperations = 5,
            ContourCount = 2,
            PocketCount = 1,
            DrillCount = 1,
            GrooveCount = 1,
            ToolChanges = 3,
            MaxDepth = 19.0,
            EstimatedTimeMinutes = 5.3
        };

        var summary = stats.FormatSummary();

        Assert.Contains("5 Op.", summary);
        Assert.Contains("2× Contour", summary);
        Assert.Contains("1× Pocket", summary);
        Assert.Contains("1× Drill", summary);
        Assert.Contains("1× Groove", summary);
        Assert.Contains("3 Werkzeugwechsel", summary);
    }

    [Fact]
    public void OperationStatistics_FormatSummary_ShortTime_ShowsSeconds()
    {
        var stats = new OperationStatistics
        {
            TotalOperations = 1,
            DrillCount = 1,
            EstimatedTimeMinutes = 0.25 // 15 seconds
        };

        var summary = stats.FormatSummary();

        Assert.Contains("~15 sec", summary);
    }

    [Fact]
    public void OperationStatistics_FormatSummary_ZeroTime_ShowsDash()
    {
        var stats = new OperationStatistics
        {
            TotalOperations = 1,
            ContourCount = 1,
            EstimatedTimeMinutes = 0
        };

        var summary = stats.FormatSummary();

        Assert.Contains("Zeit: —", summary);
    }

    [Fact]
    public void OperationStatistics_FormatSummary_OnlyContourAndDrill()
    {
        var stats = new OperationStatistics
        {
            TotalOperations = 3,
            ContourCount = 2,
            DrillCount = 1,
            PocketCount = 0,
            GrooveCount = 0
        };

        var summary = stats.FormatSummary();

        Assert.Contains("2× Contour", summary);
        Assert.Contains("1× Drill", summary);
        Assert.DoesNotContain("Pocket", summary);
        Assert.DoesNotContain("Groove", summary);
    }

    #endregion

    #region OperationStatistics Properties

    [Fact]
    public void OperationStatistics_AllPropertiesSettable()
    {
        var stats = new OperationStatistics
        {
            TotalOperations = 10,
            ContourCount = 3,
            PocketCount = 2,
            DrillCount = 4,
            GrooveCount = 1,
            ToolChanges = 3,
            MaxDepth = 25.5,
            EstimatedTimeMinutes = 12.7
        };

        Assert.Equal(10, stats.TotalOperations);
        Assert.Equal(3, stats.ContourCount);
        Assert.Equal(2, stats.PocketCount);
        Assert.Equal(4, stats.DrillCount);
        Assert.Equal(1, stats.GrooveCount);
        Assert.Equal(3, stats.ToolChanges);
        Assert.Equal(25.5, stats.MaxDepth);
        Assert.Equal(12.7, stats.EstimatedTimeMinutes);
    }

    #endregion
}
