using RhinoCNCExporter.Core.Models;
using RhinoCNCExporter.Core.Pipeline;
using RhinoCNCExporter.Core.PlateDetection;
using Xunit;

namespace RhinoCNCExporter.Tests;

public class BatchExportPlannerTests
{
    [Fact]
    public void BuildPlan_FiltersSelectedPlates_AndKeepsTotals()
    {
        var previews = new[]
        {
            CreatePreview("Seite_links", 2, 5, @"Korpus::Seite_links"),
            CreatePreview("Boden", 1, 3, @"Korpus::Boden"),
            CreatePreview("Rueckwand", 0, 1, @"Korpus::Rueckwand")
        };

        var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            @"Korpus::Seite_links",
            @"Korpus::Rueckwand"
        };

        var plan = BatchExportPlanner.BuildPlan(previews, @"C:\temp\out", ".xcs", selected);

        Assert.Equal(2, plan.PlateCount);
        Assert.Equal(2, plan.TotalBlocks);
        Assert.Equal(6, plan.TotalMachinings);
        Assert.Contains(plan.Plates, p => p.FileName == "Seite_links.xcs");
        Assert.Contains(plan.Plates, p => p.FileName == "Rueckwand.xcs");
    }

    [Fact]
    public void BuildPlan_FourPlateCorpus_ProducesFourFiles()
    {
        var previews = new[]
        {
            CreatePreview("Seite_links", 2, 6, @"Korpus::Seite_links"),
            CreatePreview("Seite_rechts", 2, 6, @"Korpus::Seite_rechts"),
            CreatePreview("Boden", 1, 4, @"Korpus::Boden"),
            CreatePreview("Deckel", 1, 4, @"Korpus::Deckel")
        };

        var plan = BatchExportPlanner.BuildPlan(previews, @"C:\temp\korpus", ".xcs");

        Assert.Equal(4, plan.PlateCount);
        Assert.Equal(20, plan.TotalMachinings);
        Assert.Equal(6, plan.TotalBlocks);
        Assert.Contains(plan.Plates, p => p.FileName == "Seite_links.xcs");
        Assert.Contains(plan.Plates, p => p.FileName == "Deckel.xcs");
    }

    [Fact]
    public void SanitizeFileName_ReplacesInvalidCharacters()
    {
        var sanitized = BatchExportPlanner.SanitizeFileName(@"Seite:links/oben");

        Assert.DoesNotContain(':', sanitized);
        Assert.DoesNotContain('/', sanitized);
        Assert.Equal("Seite_links_oben", sanitized);
    }

    [Fact]
    public void BuildReport_UsesPlanTotals()
    {
        var previews = new[] { CreatePreview("Boden", 3, 7, @"Korpus::Boden") };
        var plan = BatchExportPlanner.BuildPlan(previews, @"C:\temp\out", "cix");

        var report = BatchExportPlanner.BuildReport(
            ExportMode.MultiPlate3D,
            plan,
            new[] { @"C:\temp\out\Boden.cix" });

        Assert.Equal("1 Platten, 7 Bearbeitungen exportiert", report.SummaryLine);
        Assert.Equal(3, report.TotalBlocks);
        Assert.Single(report.ExportedFiles);
    }

    [Fact]
    public void BuildPlan_DuplicateProductionNames_UsesUniqueFileNames()
    {
        var previews = new[]
        {
            CreatePreview("Schubladen_Doppel", 2, 8, @"Korpus::Schubkasten_01"),
            CreatePreview("Schubladen_Doppel", 2, 8, @"Korpus::Schubkasten_02"),
            CreatePreview("Revisionsture", 1, 4, @"Korpus::Revision_01"),
            CreatePreview("Revisionsture", 1, 4, @"Korpus::Revision_02")
        };

        var plan = BatchExportPlanner.BuildPlan(previews, @"C:\temp\out", ".xcs");

        Assert.Equal(4, plan.PlateCount);
        Assert.Equal(new[]
        {
            "Schubladen_Doppel.xcs",
            "Schubladen_Doppel_2.xcs",
            "Revisionsture.xcs",
            "Revisionsture_2.xcs"
        }, plan.Plates.Select(p => p.FileName).ToArray());
    }

    [Fact]
    public void BuildPlan_DuplicateNames_CanFilterByLayerPathSelectionKey()
    {
        var first = CreatePreview("Boden", 1, 4, @"Korpus_A::Boden");
        var second = CreatePreview("Boden", 2, 6, @"Korpus_B::Boden");

        var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            BatchExportPlanner.GetSelectionKey(second.Plate)
        };

        var plan = BatchExportPlanner.BuildPlan(new[] { first, second }, @"C:\temp\out", ".xcs", selected);

        Assert.Single(plan.Plates);
        Assert.Equal(@"Korpus_B::Boden", plan.Plates[0].Preview.Plate.LayerPath);
        Assert.Equal("Boden.xcs", plan.Plates[0].FileName);
    }

    [Fact]
    public void BuildPlan_SanitizedNameCollision_UsesUniqueFileNames()
    {
        var previews = new[]
        {
            CreatePreview("Seite:links", 1, 4, @"Korpus::Seite_A"),
            CreatePreview("Seite/links", 1, 4, @"Korpus::Seite_B")
        };

        var plan = BatchExportPlanner.BuildPlan(previews, @"C:\temp\out", ".xcs");

        Assert.Equal(new[] { "Seite_links.xcs", "Seite_links_2.xcs" }, plan.Plates.Select(p => p.FileName).ToArray());
    }

    [Fact]
    public void BuildPlan_TwentyFourPlateCorpus_ProducesUniqueFiles_AndStableTotals()
    {
        var previews = Enumerable.Range(1, 24)
            .Select(i => CreatePreview(
                (i % 3) switch
                {
                    0 => "Boden",
                    1 => "Schubladen_Doppel",
                    _ => "Revisionsture"
                },
                blockCount: 1,
                machiningCount: 3,
                layerPath: $@"Korpus::{i:00}"))
            .ToArray();

        var plan = BatchExportPlanner.BuildPlan(previews, @"C:\temp\out", ".xcs");

        Assert.Equal(24, plan.PlateCount);
        Assert.Equal(24, plan.TotalBlocks);
        Assert.Equal(72, plan.TotalMachinings);
        Assert.Equal(24, plan.Plates.Select(p => p.FileName).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    private static PlatePreview CreatePreview(string name, int blockCount, int machiningCount, string layerPath)
    {
        var blocks = Enumerable.Range(0, blockCount)
            .Select(i => new FittingBlock
            {
                BlockName = $"Block_{i}",
                CncType = "DRILL",
                InsertionPoint = (i, i, 0),
                CncAttributes = new Dictionary<string, string>
                {
                    ["CNC_Type"] = "DRILL"
                }
            })
            .ToList();

        return new PlatePreview
        {
            Plate = new Plate
            {
                Name = name,
                LengthX = 800,
                WidthY = 400,
                Thickness = 19,
                LayerPath = layerPath,
                Origin = CoordinateTransformer.CreateFlatOrigin(0, 0, 0),
                Source = PlateSource.SolidDetection
            },
            Blocks = blocks,
            MachiningCount = machiningCount
        };
    }
}
