using System.Collections.Generic;
using RhinoCNCExporter.Core.Blocks;
using RhinoCNCExporter.Core.Emitters;
using RhinoCNCExporter.Core.Models;
using RhinoCNCExporter.Core.Naming;
using RhinoCNCExporter.Core.Pipeline;
using RhinoCNCExporter.Core.Profiles;
using Xunit;

namespace RhinoCNCExporter.Tests;

/// <summary>
/// Integration tests for the complete BladeCut pipeline:
/// Block → MachiningFactory → EmitterRouter → XCS output
/// </summary>
public class BladeCutIntegrationTests
{
    [Fact]
    public void BladeCut_FullPipeline_BlockToXCS()
    {
        // 1. Create a FittingBlock with BladeCut attributes
        var block = new FittingBlock
        {
            BlockName = "SchubladenFase_1",
            CncType = "BLADECUT",
            InsertionPoint = (100, 200, 0),
            Rotation = 0,
            CncAttributes = new Dictionary<string, string>
            {
                { BlockUserTextSchema.CNC_TYPE, "BLADECUT" },
                { BlockUserTextSchema.CNC_ANGLE, "45.0" },
                { BlockUserTextSchema.CNC_DEPTH, "15" },
                { BlockUserTextSchema.CNC_TECHCODE, "E015" },
                { BlockUserTextSchema.CNC_SEGMENTS, "seg1,19,354,19,-187.5;seg2,628,-187.5,628,354" }
            }
        };

        // 2. Convert Block to Machining via Factory
        var machinings = MachiningFactory.CreateFromBlock(block, 100, 200, 0, 19);
        Assert.Single(machinings);
        
        var bladeCut = Assert.IsType<BladeCutMachining>(machinings[0]);
        Assert.Equal("SchubladenFase_1", bladeCut.Name);
        Assert.Equal(45.0, bladeCut.Angle);
        Assert.Equal(2, bladeCut.Segments.Count);

        // 3. Create Plate with the Machining
        var plate = new Plate
        {
            Name = "Schublade_Test",
            LengthX = 700,
            WidthY = 400,
            Thickness = 19,
            Machinings = new[] { bladeCut }
        };

        // 4. Generate XCS via EmitterRouter
        var nameService = new NameService(31);
        var emitter = new XilogEmitter(nameService);
        var profile = new ScmProfile();
        var router = new EmitterRouter(emitter, nameService, profile);

        var xcsOutput = router.GenerateProgram(plate);

        // 5. Validate complete XCS structure
        Assert.Contains("CreateFinishedWorkpieceBox(\"Schublade_Test\", 700, 400, 19);", xcsOutput);
        Assert.Contains("CreateSectioningMillingStrategy(5,0,0);", xcsOutput);
        Assert.Contains("SetApproachStrategy(true,true,0);", xcsOutput);
        Assert.Contains("SetRetractStrategy(true,true,0,0);", xcsOutput);
        Assert.Contains("CreateSegment(\"seg1\",19.000,354.000,19.000,-187.500);", xcsOutput);
        Assert.Contains("CreateSegment(\"seg2\",628.000,-187.500,628.000,354.000);", xcsOutput);
        Assert.Contains("CreateBladeCut(\"SchubladenFase_1_0\",\"Blade Cut\",TypeOfProcess.GeneralRouting,\"E015\",\"-1\",45.00,2,-1,-1,-1,2,true,true,0,15);", xcsOutput);
        Assert.Contains("ResetApproachStrategy();", xcsOutput);
        Assert.Contains("ResetRetractStrategy();", xcsOutput);
        Assert.Contains("CreateMacro(\"Wegfahrschritt\",\"XPARK\");", xcsOutput);
    }

    [Fact]
    public void BladeCut_WithHelicMillingStrategy_CombinedOutput()
    {
        // Test HelicMillingStrategy + BladeCut workflow (like Rectangle macros)
        var nameService = new NameService(31);
        var emitter = new XilogEmitter(nameService);

        // First emit HelicMillingStrategy
        var helicStrategy = emitter.EmitHelicMillingStrategy(8.5, true, 17);

        // Then emit BladeCut
        var segments = new BladeCutSegment[]
        {
            new("Cut segment_1", 10, 20, 30, 40)
        };
        var sectioningStrategy = new SectioningStrategy(5, 0, 0);
        var bladeCutOutput = emitter.EmitBladeCut("TestBladeCut", 45.0, segments, "E015", 15, sectioningStrategy);

        // Combine both
        var combined = helicStrategy + bladeCutOutput;

        Assert.Contains("CreateHelicMillingStrategy(8.5,true,17);", combined);
        Assert.Contains("CreateSectioningMillingStrategy(5,0,0);", combined);
        Assert.Contains("CreateBladeCut(\"TestBladeCut\"", combined);
    }

    [Fact]
    public void BladeCut_BiesseCIX_ConversionPipeline()
    {
        // Test BladeCut conversion to Biesse CIX format
        var block = new FittingBlock
        {
            BlockName = "BiesseBladeCut",
            CncType = "BLADECUT", 
            InsertionPoint = (50, 100, 0),
            Rotation = 0,
            CncAttributes = new Dictionary<string, string>
            {
                { BlockUserTextSchema.CNC_TYPE, "BLADECUT" },
                { BlockUserTextSchema.CNC_ANGLE, "30.0" },
                { BlockUserTextSchema.CNC_SEGMENTS, "seg1,10,20,30,40;seg2,30,40,50,60" }
            }
        };

        var machinings = MachiningFactory.CreateFromBlock(block, 50, 100, 0, 18);
        var bladeCut = Assert.IsType<BladeCutMachining>(machinings[0]);

        var plate = new Plate
        {
            Name = "BiesseTest",
            LengthX = 100,
            WidthY = 100,
            Thickness = 18,
            Machinings = new[] { bladeCut }
        };

        var nameService = new NameService(63);
        var emitter = new BiesseEmitter(nameService);
        var profile = new BiesseProfile();
        var router = new EmitterRouter(emitter, nameService, profile);

        var cixOutput = router.GenerateProgram(plate);

        // Verify Biesse CIX structure
        Assert.Contains("BEGIN MAINDATA", cixOutput);
        Assert.Contains("LPX=100.00000", cixOutput);
        Assert.Contains("LPY=100.00000", cixOutput);
        Assert.Contains("LPZ=18.00000", cixOutput);
        Assert.Contains("NAME=ROUTG", cixOutput);
        Assert.Contains("ANG,VALUE=30.0", cixOutput);
        Assert.Contains("START_POINT,X=10.00000,Y=20.00000", cixOutput);
        Assert.Contains("LINE_EP,X=30.00000,Y=40.00000", cixOutput);
        Assert.Contains("LINE_EP,X=50.00000,Y=60.00000", cixOutput);
        Assert.Contains("ENDPATH", cixOutput);
    }

    [Fact]
    public void BladeCut_Mixed_WithDrillsAndPockets_OrderingTest()
    {
        // Test machining ordering with BladeCut mixed with other operations
        var drill = new DrillMachining
        {
            Name = "TestDrill",
            X = 25,
            Y = 25,
            Depth = 13,
            Diameter = 5
        };

        var bladeCut = new BladeCutMachining
        {
            Name = "TestBladeCut",
            Angle = 45.0,
            Segments = new BladeCutSegment[] { new("seg1", 10, 10, 50, 50) },
            Depth = 15.0,
            TechCode = "E015"
        };

        var pocket = new PocketMachining
        {
            Name = "TestPocket",
            Loops = new[] { new List<(double, double)> { (0, 0), (10, 0), (10, 10), (0, 10), (0, 0) } },
            Depth = 10,
            ToolDiameter = 10
        };

        var plate = new Plate
        {
            Name = "MixedOperations",
            LengthX = 100,
            WidthY = 100,
            Thickness = 19,
            Machinings = new Machining[] { drill, pocket, bladeCut },
            PreserveMachiningOrder = false // Use standard ordering
        };

        var nameService = new NameService(31);
        var emitter = new XilogEmitter(nameService);
        var profile = new ScmProfile();
        var router = new EmitterRouter(emitter, nameService, profile);

        var output = router.GenerateProgram(plate);

        // Verify ordering: BladeCut first (contour=0), then Drill (1), then Pocket (4)
        var bladeCutIdx = output.IndexOf("CreateBladeCut");
        var drillIdx = output.IndexOf("CreateDrill");
        var pocketIdx = output.IndexOf("CreatePolyline(\"TestPocket");

        Assert.True(bladeCutIdx < drillIdx, "BladeCut should come before Drill");
        Assert.True(drillIdx < pocketIdx, "Drill should come before Pocket");
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid_format")]
    [InlineData("seg1,10,20")]  // incomplete
    public void BladeCut_InvalidSegments_FallsBackToDefault(string invalidSegments)
    {
        var block = new FittingBlock
        {
            BlockName = "InvalidSegmentTest",
            CncType = "BLADECUT",
            InsertionPoint = (0, 0, 0),
            Rotation = 0,
            CncAttributes = new Dictionary<string, string>
            {
                { BlockUserTextSchema.CNC_TYPE, "BLADECUT" },
                { BlockUserTextSchema.CNC_SEGMENTS, invalidSegments }
            }
        };

        var machinings = MachiningFactory.CreateFromBlock(block, 0, 0, 0, 19);
        var bladeCut = Assert.IsType<BladeCutMachining>(machinings[0]);

        // Should fall back to 4-segment default cross pattern
        Assert.Equal(4, bladeCut.Segments.Count);
        Assert.All(bladeCut.Segments, seg => Assert.StartsWith("Cut segment_", seg.Name));
    }

    [Fact]
    public void BladeCut_NameService_TruncatesLongNames()
    {
        var nameService = new NameService(31); // XCS limit: 31 chars
        var emitter = new XilogEmitter(nameService);

        var segments = new BladeCutSegment[]
        {
            new("Very_Long_Segment_Name_That_Exceeds_Limits", 0, 0, 10, 10)
        };

        var strategy = new SectioningStrategy();
        var output = emitter.EmitBladeCut("Very_Long_BladeCut_Operation_Name_That_Should_Be_Truncated", 45.0, segments, "E015", 15, strategy);

        // Verify names are truncated to 31 chars max
        var lines = output.Split('\n');
        var bladeCutLine = Array.Find(lines, l => l.Contains("CreateBladeCut"));
        Assert.NotNull(bladeCutLine);

        // Extract name from CreateBladeCut("name",...)
        var startQuote = bladeCutLine.IndexOf('"') + 1;
        var endQuote = bladeCutLine.IndexOf('"', startQuote);
        var extractedName = bladeCutLine[startQuote..endQuote];
        
        Assert.True(extractedName.Length <= 31, $"BladeCut name '{extractedName}' exceeds 31 chars");
    }
}