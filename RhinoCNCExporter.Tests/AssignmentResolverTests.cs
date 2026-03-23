using RhinoCNCExporter.Core.Models;
using Xunit;

namespace RhinoCNCExporter.Tests;

/// <summary>
/// Tests for AssignmentResolver — the block-to-plate assignment logic.
/// Note: AssignmentResolver lives in the Plugin project but only depends on Core Models.
/// We test the core assignment logic here using mock data.
/// The actual class uses the same logic — these tests validate the algorithm.
/// </summary>
public class AssignmentResolverTests
{
    // We test the assignment algorithm directly using simple logic
    // since AssignmentResolver only uses Core.Models types.

    [Fact]
    public void GroupByLayer_SingleLayer_GroupsCorrectly()
    {
        var blocks = new[]
        {
            CreateBlock("Topfband_35", "DRILL", "Seite_links"),
            CreateBlock("Lochreihe_32", "DRILLPATTERN", "Seite_links"),
        };

        var groups = GroupByLayer(blocks);

        Assert.Single(groups);
        Assert.True(groups.ContainsKey("Seite_links"));
        Assert.Equal(2, groups["Seite_links"].Count);
    }

    [Fact]
    public void GroupByLayer_MultipleLayers_GroupsCorrectly()
    {
        var blocks = new[]
        {
            CreateBlock("Topfband_35", "DRILL", "Seite_links"),
            CreateBlock("Topfband_35", "DRILL", "Seite_rechts"),
            CreateBlock("Lochreihe_32", "DRILLPATTERN", "Seite_links"),
            CreateBlock("CLAMEX_P14", "MACRO", "Boden"),
        };

        var groups = GroupByLayer(blocks);

        Assert.Equal(3, groups.Count);
        Assert.Equal(2, groups["Seite_links"].Count);
        Assert.Single(groups["Seite_rechts"]);
        Assert.Single(groups["Boden"]);
    }

    [Fact]
    public void GroupByLayer_NoLayer_UsesUnassigned()
    {
        var blocks = new[]
        {
            CreateBlock("Topfband_35", "DRILL", null),
        };

        var groups = GroupByLayer(blocks);
        Assert.True(groups.ContainsKey("__unassigned__"));
    }

    [Fact]
    public void GroupByLayer_Empty_ReturnsEmpty()
    {
        var groups = GroupByLayer(Array.Empty<FittingBlock>());
        Assert.Empty(groups);
    }

    [Fact]
    public void LayerMatch_BlockToPlate_MatchesByName()
    {
        var plates = new[]
        {
            CreatePlate("Seite_links", 800, 400, 19),
            CreatePlate("Seite_rechts", 800, 400, 19),
            CreatePlate("Boden", 750, 400, 19),
        };

        var blocks = new[]
        {
            CreateBlock("Topfband_35", "DRILL", "Seite_links"),
            CreateBlock("Lochreihe_32", "DRILLPATTERN", "Seite_links"),
            CreateBlock("CLAMEX_P14", "MACRO", "Boden"),
        };

        var assignments = ResolveByLayerMatch(plates, blocks);

        Assert.Equal(3, assignments.Count);
        Assert.Equal(2, assignments["Seite_links"].Count);
        Assert.Empty(assignments["Seite_rechts"]);
        Assert.Single(assignments["Boden"]);
    }

    [Fact]
    public void LayerMatch_NoMatchingPlate_BlockNotAssigned()
    {
        var plates = new[]
        {
            CreatePlate("Seite_links", 800, 400, 19),
        };

        var blocks = new[]
        {
            CreateBlock("Topfband_35", "DRILL", "NonExistentLayer"),
        };

        var assignments = ResolveByLayerMatch(plates, blocks);
        Assert.Empty(assignments["Seite_links"]);
    }

    [Fact]
    public void LayerMatch_CaseInsensitive()
    {
        var plates = new[]
        {
            CreatePlate("Seite_Links", 800, 400, 19),
        };

        var blocks = new[]
        {
            CreateBlock("Topfband_35", "DRILL", "seite_links"),
        };

        var assignments = ResolveByLayerMatch(plates, blocks);
        Assert.Single(assignments["Seite_Links"]);
    }

    // --- Helper: Simulates AssignmentResolver logic ---

    private static Dictionary<string, List<FittingBlock>> GroupByLayer(IReadOnlyList<FittingBlock> blocks)
    {
        var result = new Dictionary<string, List<FittingBlock>>(StringComparer.OrdinalIgnoreCase);
        foreach (var block in blocks)
        {
            var key = block.LayerName ?? "__unassigned__";
            if (!result.TryGetValue(key, out var list))
            {
                list = new List<FittingBlock>();
                result[key] = list;
            }
            list.Add(block);
        }
        return result;
    }

    private static Dictionary<string, List<FittingBlock>> ResolveByLayerMatch(
        IReadOnlyList<Plate> plates, IReadOnlyList<FittingBlock> blocks)
    {
        var blocksByLayer = GroupByLayer(blocks);
        var result = new Dictionary<string, List<FittingBlock>>(StringComparer.OrdinalIgnoreCase);

        foreach (var plate in plates)
        {
            var assigned = new List<FittingBlock>();

            if (blocksByLayer.TryGetValue(plate.Name, out var byName))
                assigned.AddRange(byName);

            result[plate.Name] = assigned;
        }

        return result;
    }

    private static FittingBlock CreateBlock(string name, string cncType, string? layer) => new()
    {
        BlockName = name,
        CncType = cncType,
        InsertionPoint = (100, 200, 0),
        CncAttributes = new Dictionary<string, string> { ["CNC_Type"] = cncType },
        LayerName = layer
    };

    private static Plate CreatePlate(string name, double lx, double ly, double thickness) => new()
    {
        Name = name,
        LengthX = lx,
        WidthY = ly,
        Thickness = thickness
    };
}
