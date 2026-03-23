using RhinoCNCExporter.BlockScanning;
using RhinoCNCExporter.Core.Models;
using RhinoCNCExporter.Core.PlateDetection;
using Xunit;

namespace RhinoCNCExporter.Tests;

public class AssignmentResolverTests
{
    private readonly AssignmentResolver _resolver = new();

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

        var groups = _resolver.GroupByLayer(blocks);

        Assert.Equal(3, groups.Count);
        Assert.Equal(2, groups["Seite_links"].Count);
        Assert.Single(groups["Seite_rechts"]);
        Assert.Single(groups["Boden"]);
    }

    [Fact]
    public void Resolve_LayerPathPreferredOverName()
    {
        var plate = CreatePlate("Boden", @"Korpus_1::Boden", 0, 0, 0);
        var byPath = CreateBlock("Topfband_35", "DRILL", @"Korpus_1::Boden");
        var byName = CreateBlock("Lochreihe_32", "DRILLPATTERN", "Boden");

        var result = _resolver.Resolve(new[] { plate }, new[] { byPath, byName });

        Assert.Single(result);
        Assert.Single(result[0].Blocks);
        Assert.Same(byPath, result[0].Blocks[0]);
    }

    [Fact]
    public void Resolve_ExplicitCncPlate_AssignsWithoutLayerMatch()
    {
        var plate = CreatePlate("Seite_links", @"Korpus::Seite_links", 0, 0, 0);
        var block = CreateBlock(
            "Duebel_8x30",
            "DRILL",
            layerName: "Andere_Layer",
            attrs: new Dictionary<string, string> { ["CNC_Plate"] = "Seite_links" });

        var result = _resolver.Resolve(new[] { plate }, new[] { block });

        Assert.Single(result);
        Assert.Single(result[0].Blocks);
        Assert.Same(block, result[0].Blocks[0]);
    }

    [Fact]
    public void ResolveWithProximity_AssignsBlockToContainingPlate()
    {
        var left = CreatePlate("Seite_links", @"Korpus::Seite_links", 0, 0, 0);
        var right = CreatePlate("Seite_rechts", @"Korpus::Seite_rechts", 120, 0, 0);
        var block = CreateBlock("Topfband_35", "DRILL", layerName: null, x: 40, y: 50, z: 9.5);

        var result = _resolver.ResolveWithProximity(new[] { left, right }, new[] { block }, tolerance: 5);

        Assert.Single(result[0].Blocks);
        Assert.Empty(result[1].Blocks);
    }

    [Fact]
    public void ResolveWithProximity_BlockBetweenTwoPlates_AssignsToClosestFace()
    {
        var left = CreatePlate("Seite_links", @"Korpus::Seite_links", 0, 0, 0, lengthX: 100);
        var right = CreatePlate("Seite_rechts", @"Korpus::Seite_rechts", 108, 0, 0, lengthX: 100);
        var block = CreateBlock("Topfband_35", "DRILL", layerName: null, x: 103, y: 50, z: 9.5);

        var result = _resolver.ResolveWithProximity(new[] { left, right }, new[] { block }, tolerance: 5);

        Assert.Single(result[0].Blocks);
        Assert.Empty(result[1].Blocks);
    }

    [Fact]
    public void ResolveWithProximity_ExplicitPlateBeatsCloserNeighbor()
    {
        var left = CreatePlate("Seite_links", @"Korpus::Seite_links", 0, 0, 0);
        var right = CreatePlate("Seite_rechts", @"Korpus::Seite_rechts", 120, 0, 0);
        var block = CreateBlock(
            "CLAMEX_P14",
            "MACRO",
            layerName: null,
            x: 125,
            y: 40,
            z: 9.5,
            attrs: new Dictionary<string, string> { ["CNC_Plate"] = "Seite_links" });

        var result = _resolver.ResolveWithProximity(new[] { left, right }, new[] { block }, tolerance: 5);

        Assert.Single(result[0].Blocks);
        Assert.Empty(result[1].Blocks);
    }

    [Fact]
    public void ResolveWithProximity_BlockOutsideTolerance_RemainsUnassigned()
    {
        var plate = CreatePlate("Boden", @"Korpus::Boden", 0, 0, 0);
        var block = CreateBlock("Topfband_35", "DRILL", layerName: null, x: 500, y: 500, z: 50);

        var result = _resolver.ResolveWithProximity(new[] { plate }, new[] { block }, tolerance: 5);

        Assert.Single(result);
        Assert.Empty(result[0].Blocks);
    }

    private static Plate CreatePlate(
        string name,
        string layerPath,
        double originX,
        double originY,
        double originZ,
        double lengthX = 100,
        double widthY = 100,
        double thickness = 19)
    {
        return new Plate
        {
            Name = name,
            LayerPath = layerPath,
            LengthX = lengthX,
            WidthY = widthY,
            Thickness = thickness,
            Origin = CoordinateTransformer.CreateFlatOrigin(originX, originY, originZ),
            Source = PlateSource.SolidDetection
        };
    }

    private static FittingBlock CreateBlock(
        string name,
        string cncType,
        string? layerName,
        double x = 100,
        double y = 200,
        double z = 0,
        Dictionary<string, string>? attrs = null)
    {
        var cncAttributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CNC_Type"] = cncType
        };

        if (attrs != null)
        {
            foreach (var (key, value) in attrs)
            {
                cncAttributes[key] = value;
            }
        }

        return new FittingBlock
        {
            BlockName = name,
            CncType = cncType,
            InsertionPoint = (x, y, z),
            CncAttributes = cncAttributes,
            LayerName = layerName
        };
    }
}
