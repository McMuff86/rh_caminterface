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
            CreatePreview("Seite_links", 2, 5),
            CreatePreview("Boden", 1, 3),
            CreatePreview("Rueckwand", 0, 1)
        };

        var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Seite_links",
            "Rueckwand"
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
            CreatePreview("Seite_links", 2, 6),
            CreatePreview("Seite_rechts", 2, 6),
            CreatePreview("Boden", 1, 4),
            CreatePreview("Deckel", 1, 4)
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
        var previews = new[] { CreatePreview("Boden", 3, 7) };
        var plan = BatchExportPlanner.BuildPlan(previews, @"C:\temp\out", "cix");

        var report = BatchExportPlanner.BuildReport(
            ExportMode.MultiPlate3D,
            plan,
            new[] { @"C:\temp\out\Boden.cix" });

        Assert.Equal("1 Platten, 7 Bearbeitungen exportiert", report.SummaryLine);
        Assert.Equal(3, report.TotalBlocks);
        Assert.Single(report.ExportedFiles);
    }

    private static PlatePreview CreatePreview(string name, int blockCount, int machiningCount)
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
                Origin = CoordinateTransformer.CreateFlatOrigin(0, 0, 0),
                Source = PlateSource.SolidDetection
            },
            Blocks = blocks,
            MachiningCount = machiningCount
        };
    }
}
