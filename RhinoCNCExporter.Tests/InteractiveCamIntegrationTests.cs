using RhinoCNCExporter.Core.Models;
using RhinoCNCExporter.Core.Pipeline;
using Xunit;

namespace RhinoCNCExporter.Tests;

/// <summary>
/// Tests for integration of interactive CAM commands with the existing pipeline.
/// </summary>
public class InteractiveCamIntegrationTests
{
    [Fact]
    public void MergeAllSources_UserTextTakesPriorityOverBlocks()
    {
        // Arrange
        var machiningBuilder = new MachiningBuilder();
        
        var legacyMachinings = new List<Machining>
        {
            new DrillMachining
            {
                Name = "Legacy_Drill",
                X = 10, Y = 10,
                Diameter = 5, Depth = 10,
                Source = MachiningSource.LegacyLayer
            }
        };

        var blockMachinings = new List<Machining>
        {
            new DrillMachining
            {
                Name = "Block_Drill",
                X = 10.1, Y = 10.1, // Very close to legacy
                Diameter = 5, Depth = 10,
                Source = MachiningSource.BlockDetection
            }
        };

        var userTextMachinings = new List<Machining>
        {
            new DrillMachining
            {
                Name = "UserText_Drill",
                X = 10.05, Y = 10.05, // Close to both
                Diameter = 5, Depth = 10,
                Source = MachiningSource.Manual
            }
        };

        // Act
        var result = machiningBuilder.MergeAllSources(
            legacyMachinings, blockMachinings, userTextMachinings, positionTolerance: 0.5);

        // Assert
        Assert.Single(result);
        Assert.Equal("UserText_Drill", result[0].Name);
        Assert.Equal(MachiningSource.Manual, result[0].Source);
    }

    [Fact]
    public void MergeAllSources_BlocksTakePriorityOverLegacy()
    {
        // Arrange
        var machiningBuilder = new MachiningBuilder();
        
        var legacyMachinings = new List<Machining>
        {
            new DrillMachining
            {
                Name = "Legacy_Drill",
                X = 20, Y = 20,
                Diameter = 5, Depth = 10,
                Source = MachiningSource.LegacyLayer
            }
        };

        var blockMachinings = new List<Machining>
        {
            new DrillMachining
            {
                Name = "Block_Drill",
                X = 20.1, Y = 20.1, // Close to legacy
                Diameter = 5, Depth = 10,
                Source = MachiningSource.BlockDetection
            }
        };

        var userTextMachinings = new List<Machining>(); // No UserText operations

        // Act
        var result = machiningBuilder.MergeAllSources(
            legacyMachinings, blockMachinings, userTextMachinings, positionTolerance: 0.5);

        // Assert
        Assert.Single(result);
        Assert.Equal("Block_Drill", result[0].Name);
        Assert.Equal(MachiningSource.BlockDetection, result[0].Source);
    }

    [Fact]
    public void MergeAllSources_NonConflictingOperationsAllIncluded()
    {
        // Arrange
        var machiningBuilder = new MachiningBuilder();
        
        var legacyMachinings = new List<Machining>
        {
            new DrillMachining
            {
                Name = "Legacy_Drill",
                X = 10, Y = 10,
                Diameter = 5, Depth = 10,
                Source = MachiningSource.LegacyLayer
            }
        };

        var blockMachinings = new List<Machining>
        {
            new DrillMachining
            {
                Name = "Block_Drill",
                X = 20, Y = 20, // Far from legacy
                Diameter = 5, Depth = 10,
                Source = MachiningSource.BlockDetection
            }
        };

        var userTextMachinings = new List<Machining>
        {
            new RoutingMachining
            {
                Name = "UserText_Contour",
                Points = new[] { (30.0, 30.0), (40.0, 30.0), (40.0, 40.0) },
                Depth = 10,
                ToolDiameter = 6,
                IsClosed = false,
                Source = MachiningSource.Manual
            }
        };

        // Act
        var result = machiningBuilder.MergeAllSources(
            legacyMachinings, blockMachinings, userTextMachinings, positionTolerance: 0.5);

        // Assert
        Assert.Equal(3, result.Count);
        
        // UserText should be first (highest priority)
        Assert.Equal("UserText_Contour", result[0].Name);
        Assert.Equal(MachiningSource.Manual, result[0].Source);
        
        // Block should be second
        Assert.Equal("Block_Drill", result[1].Name);
        Assert.Equal(MachiningSource.BlockDetection, result[1].Source);
        
        // Legacy should be last
        Assert.Equal("Legacy_Drill", result[2].Name);
        Assert.Equal(MachiningSource.LegacyLayer, result[2].Source);
    }

    [Fact]
    public void MergeAllSources_DifferentOperationTypes_NoConflict()
    {
        // Arrange
        var machiningBuilder = new MachiningBuilder();
        
        var legacyMachinings = new List<Machining>
        {
            new DrillMachining
            {
                Name = "Legacy_Drill",
                X = 10, Y = 10,
                Diameter = 5, Depth = 10,
                Source = MachiningSource.LegacyLayer
            }
        };

        var userTextMachinings = new List<Machining>
        {
            new PocketMachining
            {
                Name = "UserText_Pocket",
                Loops = new[] { new[] { (9.0, 9.0), (11.0, 9.0), (11.0, 11.0), (9.0, 11.0) } },
                Depth = 5,
                ToolDiameter = 6,
                Source = MachiningSource.Manual
            }
        };

        // Act
        var result = machiningBuilder.MergeAllSources(
            legacyMachinings, Array.Empty<Machining>(), userTextMachinings, positionTolerance: 0.5);

        // Assert - Different operation types at same location should not conflict
        Assert.Equal(2, result.Count);
        Assert.Contains(result, m => m.Name == "UserText_Pocket");
        Assert.Contains(result, m => m.Name == "Legacy_Drill");
    }

    [Fact]
    public void MergeAllSources_EmptyInputs_ReturnsEmpty()
    {
        // Arrange
        var machiningBuilder = new MachiningBuilder();
        var emptyList = new List<Machining>();

        // Act
        var result = machiningBuilder.MergeAllSources(emptyList, emptyList, emptyList);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void MergeAllSources_ToleranceTest_RespectsTolerance()
    {
        // Arrange
        var machiningBuilder = new MachiningBuilder();
        
        var legacyMachinings = new List<Machining>
        {
            new DrillMachining
            {
                Name = "Legacy_Drill",
                X = 10, Y = 10,
                Diameter = 5, Depth = 10,
                Source = MachiningSource.LegacyLayer
            }
        };

        var userTextMachinings = new List<Machining>
        {
            new DrillMachining
            {
                Name = "UserText_Drill",
                X = 10.6, Y = 10.6, // Just outside default tolerance of 0.5
                Diameter = 5, Depth = 10,
                Source = MachiningSource.Manual
            }
        };

        // Act with default tolerance (0.5)
        var resultWithDefault = machiningBuilder.MergeAllSources(
            legacyMachinings, Array.Empty<Machining>(), userTextMachinings);

        // Act with larger tolerance (1.0)
        var resultWithLarge = machiningBuilder.MergeAllSources(
            legacyMachinings, Array.Empty<Machining>(), userTextMachinings, positionTolerance: 1.0);

        // Assert
        Assert.Equal(2, resultWithDefault.Count); // Both should be included (outside tolerance)
        Assert.Single(resultWithLarge); // Only UserText should be included (within tolerance)
        Assert.Equal("UserText_Drill", resultWithLarge[0].Name);
    }
}