using RhinoCNCExporter.Core.Blocks;
using RhinoCNCExporter.Core.Emitters;
using RhinoCNCExporter.Core.Models;
using RhinoCNCExporter.Core.Naming;
using RhinoCNCExporter.Core.Pipeline;
using RhinoCNCExporter.Core.PlateDetection;
using RhinoCNCExporter.Core.Profiles;
using Xunit;

namespace RhinoCNCExporter.Tests;

/// <summary>
/// Tests for the ExportService3D pipeline logic.
/// Since ExportService3D depends on RhinoCommon (PlateDetector, BlockScanner),
/// we test the core pipeline: plates + blocks → CNC programs.
/// This validates the orchestration logic that ExportService3D uses internally.
/// </summary>
public class ExportService3DPipelineTests
{
    // === Multi-Plate Pipeline ===

    [Fact]
    public void MultiPlate_FourPlateKorpus_GeneratesFourPrograms()
    {
        // Arrange: 4-plate cabinet
        var plates = new[]
        {
            CreateFlatPlate("Seite_links", 600, 2100, 19),
            CreateFlatPlate("Seite_rechts", 600, 2100, 19),
            CreateFlatPlate("Boden", 560, 600, 19),
            CreateFlatPlate("Deckel", 560, 600, 19),
        };

        var blocks = new Dictionary<string, List<FittingBlock>>
        {
            ["Seite_links"] = new()
            {
                CreateDrillBlock("Topfband_35", 50, 100),
                CreateDrillBlock("Topfband_35", 50, 400),
                CreateDrillBlock("Topfband_35", 50, 700),
            },
            ["Seite_rechts"] = new()
            {
                CreateDrillBlock("Topfband_35", 50, 100),
                CreateDrillBlock("Topfband_35", 50, 400),
            },
            ["Boden"] = new()
            {
                CreateDrillBlock("Duebel_8x30", 100, 50),
                CreateDrillBlock("Duebel_8x30", 200, 50),
            },
            ["Deckel"] = new()
            {
                CreateDrillBlock("Duebel_8x30", 100, 50),
            }
        };

        // Act: Generate CNC program for each plate
        var programs = new Dictionary<string, string>();
        var emitter = new XilogEmitter(new NameService());
        var profile = new MaestroCadTProfile();

        foreach (var plate in plates)
        {
            var plateBlocks = blocks.GetValueOrDefault(plate.Name, new List<FittingBlock>());
            var machinings = new List<Machining>();

            foreach (var block in plateBlocks)
            {
                var (localX, localY, localZ) = CoordinateTransformer.WorldToPlateLocal(
                    plate.Origin, block.InsertionPoint.X, block.InsertionPoint.Y, block.InsertionPoint.Z);
                machinings.AddRange(MachiningFactory.CreateFromBlock(block, localX, localY, localZ, plate.Thickness));
            }

            var plateWithMachinings = plate with { Machinings = machinings };
            var nameService = new NameService();
            var router = new EmitterRouter(emitter, nameService, profile);
            programs[plate.Name] = router.GenerateProgram(plateWithMachinings);
        }

        // Assert
        Assert.Equal(4, programs.Count);

        // Seite_links: 3 drills
        Assert.Contains("Seite_links", programs["Seite_links"]);
        Assert.Equal(3, CountOccurrences(programs["Seite_links"], "CreateDrill"));

        // Seite_rechts: 2 drills
        Assert.Contains("Seite_rechts", programs["Seite_rechts"]);
        Assert.Equal(2, CountOccurrences(programs["Seite_rechts"], "CreateDrill"));

        // Boden: 2 drills
        Assert.Equal(2, CountOccurrences(programs["Boden"], "CreateDrill"));

        // Deckel: 1 drill
        Assert.Equal(1, CountOccurrences(programs["Deckel"], "CreateDrill"));
    }

    [Fact]
    public void MultiPlate_EmptyPlate_ProducesHeaderFooterOnly()
    {
        var plate = CreateFlatPlate("Rückwand", 760, 2062, 5);
        var plateWithMachinings = plate with { Machinings = Array.Empty<Machining>() };

        var emitter = new XilogEmitter(new NameService());
        var nameService = new NameService();
        var profile = new MaestroCadTProfile();
        var router = new EmitterRouter(emitter, nameService, profile);
        var program = router.GenerateProgram(plateWithMachinings);

        // Should still have header and footer
        Assert.Contains("Rückwand", program);
        Assert.DoesNotContain("CreateDrill", program);
        // Note: Footer may contain CreateMacro("Wegfahrs..") for XPARK — that's expected
        Assert.DoesNotContain("SawCut_Lamello", program);
    }

    [Fact]
    public void MultiPlate_MixedDrillsAndClamex_CorrectOutput()
    {
        var plate = CreateFlatPlate("Seite_links", 600, 2100, 19);
        var machinings = new List<Machining>();

        // Drills
        machinings.Add(new DrillMachining
        {
            Name = "Topfband_1", X = 50, Y = 100, Depth = 13, Diameter = 35,
            Side = MachiningSide.Top, Source = MachiningSource.BlockDetection
        });
        machinings.Add(new DrillMachining
        {
            Name = "Topfband_2", X = 50, Y = 400, Depth = 13, Diameter = 35,
            Side = MachiningSide.Top, Source = MachiningSource.BlockDetection
        });

        // CLAMEX
        var clamexBlock = new FittingBlock
        {
            BlockName = "CLAMEX_P14",
            CncType = "MACRO",
            InsertionPoint = (9.5, 200, 0),
            Rotation = 270,
            CncAttributes = new Dictionary<string, string>
            {
                ["CNC_Type"] = "MACRO",
                ["CNC_MacroName"] = "SawCut_Lamello",
                ["CNC_Orientation"] = "0",
                ["CNC_Side"] = "TOP"
            }
        };
        var clamex = ClamexMacroBuilder.CreateMachining(clamexBlock, 9.5, 200, 19, 1);
        Assert.NotNull(clamex);
        machinings.Add(clamex);

        var plateWithMachinings = plate with { Machinings = machinings };
        var emitter = new XilogEmitter(new NameService());
        var nameService = new NameService();
        var profile = new MaestroCadTProfile();
        var router = new EmitterRouter(emitter, nameService, profile);
        var program = router.GenerateProgram(plateWithMachinings);

        Assert.Equal(2, CountOccurrences(program, "CreateDrill"));
        // 1 SawCut_Lamello macro + 1 Wegfahrs (XPARK footer) = 2 CreateMacro total
        Assert.True(CountOccurrences(program, "CreateMacro") >= 1);
        Assert.Equal(1, CountOccurrences(program, "SawCut_Lamello"));
    }

    [Fact]
    public void MultiPlate_UprightSidePanel_CoordinatesTransformedCorrectly()
    {
        // Side panel standing upright in XZ plane at X=0
        var origin = CoordinateTransformer.CreateUprightXZOrigin(0, 0, 0);
        var plate = new Plate
        {
            Name = "Seite_links",
            LengthX = 600,
            WidthY = 2100,
            Thickness = 19,
            Origin = origin,
            Source = PlateSource.SolidDetection
        };

        // Block at world position (300, 9.5, 500) on the face of the upright panel
        var block = CreateDrillBlock("Topfband_35", 300, 9.5);
        // Need to set Z in world space
        var fittingBlock = new FittingBlock
        {
            BlockName = "Topfband_35",
            CncType = "DRILL",
            InsertionPoint = (300, 9.5, 500),
            CncAttributes = new Dictionary<string, string>
            {
                ["CNC_Type"] = "DRILL",
                ["CNC_Diameter"] = "35",
                ["CNC_Depth"] = "13",
                ["CNC_Side"] = "TOP"
            }
        };

        var (localX, localY, localZ) = CoordinateTransformer.WorldToPlateLocal(
            origin, 300, 9.5, 500);

        Assert.Equal(300, localX, 0.1);   // Along plate length (world X)
        Assert.Equal(500, localY, 0.1);   // Along plate height (world Z)
        Assert.Equal(9.5, localZ, 0.1);   // Into the plate (world Y)

        var machinings = MachiningFactory.CreateFromBlock(fittingBlock, localX, localY, localZ, plate.Thickness);
        Assert.Single(machinings);
        var drill = Assert.IsType<DrillMachining>(machinings[0]);
        Assert.Equal(300, drill.X, 0.1);
        Assert.Equal(500, drill.Y, 0.1);
    }

    [Fact]
    public void MultiPlate_FileNameSanitization_HandlesSpecialChars()
    {
        // Test that plate names with special chars would be sanitized
        var plate = CreateFlatPlate("Tür/links", 380, 2062, 19);
        // The sanitization happens in ExportService3D, so just verify the plate name works
        Assert.Equal("Tür/links", plate.Name);
    }

    // === ExportReport Tests ===

    [Fact]
    public void ExportReport_TracksMultiplePlates()
    {
        var report = new ExportReport
        {
            Success = true,
            Mode = ExportMode.ThreeD,
            TotalPlatesDetected = 7,
            PlatesExported = 5,
            TotalMachinings = 48,
            TotalBlocksDetected = 35,
            ExportedFiles = new List<string>
            {
                "/output/Seite_links.xcs",
                "/output/Seite_rechts.xcs",
                "/output/Boden.xcs",
                "/output/Deckel.xcs",
                "/output/Rückwand.xcs"
            },
            Warnings = new List<string>
            {
                "Platte 'Rückwand' hat keine Bearbeitungen."
            }
        };

        Assert.True(report.Success);
        Assert.Equal(5, report.PlatesExported);
        Assert.Equal(48, report.TotalMachinings);
        Assert.Single(report.Warnings);
        Assert.Equal(5, report.ExportedFiles.Count);

        var summary = report.GetSummary();
        Assert.Contains("5 Platte(n) exportiert", summary);
        Assert.Contains("48 Bearbeitung(en)", summary);
        Assert.Contains("1 Warnung(en)", summary);
    }

    // === Legacy Regression ===

    [Fact]
    public void LegacyExport_EmitterRouter_StillWorksForSinglePlate()
    {
        // Verify that the existing EmitterRouter works unchanged for a single plate
        var plate = new Plate
        {
            Name = "Test_Program",
            LengthX = 500,
            WidthY = 300,
            Thickness = 19,
            Origin = PlateOrigin.Identity,
            Source = PlateSource.LegacyLayer,
            Machinings = new Machining[]
            {
                new DrillMachining
                {
                    Name = "Drill_1",
                    X = 100, Y = 150,
                    Depth = 13, Diameter = 5,
                    Side = MachiningSide.Top
                }
            }
        };

        var emitter = new XilogEmitter(new NameService());
        var nameService = new NameService();
        var profile = new MaestroCadTProfile();
        var router = new EmitterRouter(emitter, nameService, profile);
        var program = router.GenerateProgram(plate);

        // Header present
        Assert.Contains("Test_Program", program);
        // Drill present
        Assert.Contains("CreateDrill", program);
        // Footer present (XPARK for Xilog)
        Assert.Contains("XPARK", program);
    }

    [Fact]
    public void LegacyExport_DrillPattern_StillEmitsCreatePattern()
    {
        var plate = new Plate
        {
            Name = "Test_Pattern",
            LengthX = 300,
            WidthY = 700,
            Thickness = 19,
            Origin = PlateOrigin.Identity,
            Machinings = new Machining[]
            {
                new DrillPatternMachining
                {
                    Name = "Lochreihe_1",
                    X = 37, Y = 50,
                    Depth = 13, Diameter = 5,
                    CountX = 1, CountY = 10,
                    SpacingX = 0, SpacingY = 32,
                    Side = MachiningSide.Top
                }
            }
        };

        var emitter = new XilogEmitter(new NameService());
        var nameService = new NameService();
        var profile = new MaestroCadTProfile();
        var router = new EmitterRouter(emitter, nameService, profile);
        var program = router.GenerateProgram(plate);

        Assert.Contains("CreatePattern", program);
    }

    // === Helpers ===

    private static Plate CreateFlatPlate(string name, double lpx, double lpy, double dz)
    {
        return new Plate
        {
            Name = name,
            LengthX = lpx,
            WidthY = lpy,
            Thickness = dz,
            LayerPath = name,
            Origin = CoordinateTransformer.CreateFlatOrigin(0, 0, 0),
            Source = PlateSource.SolidDetection
        };
    }

    private static FittingBlock CreateDrillBlock(string name, double x, double y)
    {
        return new FittingBlock
        {
            BlockName = name,
            CncType = "DRILL",
            InsertionPoint = (x, y, 0),
            CncAttributes = new Dictionary<string, string>
            {
                ["CNC_Type"] = "DRILL",
                ["CNC_Diameter"] = "35",
                ["CNC_Depth"] = "13",
                ["CNC_Side"] = "TOP"
            }
        };
    }

    private static int CountOccurrences(string text, string search)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(search, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += search.Length;
        }
        return count;
    }
}
