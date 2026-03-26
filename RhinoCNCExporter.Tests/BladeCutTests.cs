using System.Collections.Generic;
using RhinoCNCExporter.Core.Blocks;
using RhinoCNCExporter.Core.Emitters;
using RhinoCNCExporter.Core.Models;
using RhinoCNCExporter.Core.Naming;
using RhinoCNCExporter.Core.Pipeline;
using RhinoCNCExporter.Core.Profiles;
using Xunit;

namespace RhinoCNCExporter.Tests;

public class BladeCutTests
{
    [Fact]
    public void BladeCutMachining_Creation_Success()
    {
        var segments = new BladeCutSegment[]
        {
            new("Cut segment_1", 10, 20, 30, 20),
            new("Cut segment_2", 30, 20, 30, 40)
        };

        var bladeCut = new BladeCutMachining
        {
            Name = "Test BladeCut",
            Angle = 45.0,
            Segments = segments,
            Depth = 15.0,
            TechCode = "E015",
            Side = MachiningSide.Top,
            Source = MachiningSource.BlockDetection
        };

        Assert.Equal("Test BladeCut", bladeCut.Name);
        Assert.Equal(45.0, bladeCut.Angle);
        Assert.Equal(15.0, bladeCut.Depth);
        Assert.Equal(2, bladeCut.Segments.Count);
        Assert.Equal("E015", bladeCut.TechCode);
    }

    [Fact]
    public void MachiningFactory_CreateBladeCut_WithValidAttributes()
    {
        var block = new FittingBlock
        {
            BlockName = "TestBladeCut",
            CncType = "BLADECUT",
            InsertionPoint = (100, 200, 0),
            Rotation = 0,
            CncAttributes = new Dictionary<string, string>
            {
                { BlockUserTextSchema.CNC_TYPE, "BLADECUT" },
                { BlockUserTextSchema.CNC_ANGLE, "45.0" },
                { BlockUserTextSchema.CNC_DEPTH, "15" },
                { BlockUserTextSchema.CNC_TECHCODE, "E015" },
                { BlockUserTextSchema.CNC_SEGMENTS, "seg1,10,20,30,20;seg2,30,20,30,40" }
            }
        };

        var machinings = MachiningFactory.CreateFromBlock(block, 100, 200, 0, 19);

        Assert.Single(machinings);
        var bladeCut = Assert.IsType<BladeCutMachining>(machinings[0]);
        Assert.Equal("TestBladeCut", bladeCut.Name);
        Assert.Equal(45.0, bladeCut.Angle);
        Assert.Equal(15.0, bladeCut.Depth);
        Assert.Equal("E015", bladeCut.TechCode);
        Assert.Equal(2, bladeCut.Segments.Count);

        var seg1 = bladeCut.Segments[0];
        Assert.Equal("seg1", seg1.Name);
        Assert.Equal(10.0, seg1.StartX);
        Assert.Equal(20.0, seg1.StartY);
        Assert.Equal(30.0, seg1.EndX);
        Assert.Equal(20.0, seg1.EndY);
    }

    [Fact]
    public void MachiningFactory_CreateBladeCut_WithDefaultSegments()
    {
        var block = new FittingBlock
        {
            BlockName = "TestBladeCut",
            CncType = "BLADECUT",
            InsertionPoint = (50, 100, 0),
            Rotation = 0,
            CncAttributes = new Dictionary<string, string>
            {
                { BlockUserTextSchema.CNC_TYPE, "BLADECUT" }
            }
        };

        var machinings = MachiningFactory.CreateFromBlock(block, 50, 100, 0, 19);

        Assert.Single(machinings);
        var bladeCut = Assert.IsType<BladeCutMachining>(machinings[0]);
        Assert.Equal(45.0, bladeCut.Angle); // default
        Assert.Equal(15.0, bladeCut.Depth); // default
        Assert.Equal("E015", bladeCut.TechCode); // default
        Assert.Equal(4, bladeCut.Segments.Count); // default cross pattern

        // Check default cross pattern around center (50, 100)
        var seg1 = bladeCut.Segments[0];
        Assert.Equal("Cut segment_1", seg1.Name);
        Assert.Equal(40.0, seg1.StartX); // 50 - 10
        Assert.Equal(90.0, seg1.StartY); // 100 - 10
    }

    [Fact]
    public void XilogEmitter_EmitBladeCut_ProductionFormat()
    {
        var nameService = new NameService();
        var emitter = new XilogEmitter(nameService);

        var segments = new BladeCutSegment[]
        {
            new("Cut segment_1", 19, 354, 19, -187.5),
            new("Cut segment_2", 628, -187.5, 628, 354)
        };

        var strategy = new SectioningStrategy(5, 0, 0);

        var result = emitter.EmitBladeCut("Geneigter Schnitt", 45.0, segments, "E015", 15, strategy);

        var lines = result.Split('\n');
        Assert.Contains("SelectWorkplane(\"Top\");", lines);
        Assert.Contains("CreateSectioningMillingStrategy(5,0,0);", lines);
        Assert.Contains("SetApproachStrategy(true,true,0);", lines);
        Assert.Contains("SetRetractStrategy(true,true,0,0);", lines);
        Assert.Contains("CreateSegment(\"Cut segment_1\",19.000,354.000,19.000,-187.500);", lines);
        Assert.Contains("CreateSegment(\"Cut segment_2\",628.000,-187.500,628.000,354.000);", lines);
        Assert.Contains("CreateBladeCut(\"Geneigter Schnitt\",\"Blade Cut\",TypeOfProcess.GeneralRouting,\"E015\",\"-1\",45.00,2,-1,-1,-1,2,true,true,0,15);", lines);
        Assert.Contains("ResetApproachStrategy();", lines);
        Assert.Contains("ResetRetractStrategy();", lines);
    }

    [Fact]
    public void XilogEmitter_EmitHelicMillingStrategy()
    {
        var nameService = new NameService();
        var emitter = new XilogEmitter(nameService);

        var result = emitter.EmitHelicMillingStrategy(8.5, true, 17);

        Assert.Equal("CreateHelicMillingStrategy(8.5,true,17);\n", result);
    }

    [Fact]
    public void BiesseEmitter_EmitBladeCut_ConvertToRouting()
    {
        var nameService = new NameService();
        var emitter = new BiesseEmitter(nameService);

        var segments = new BladeCutSegment[]
        {
            new("Cut segment_1", 10, 20, 30, 20),
            new("Cut segment_2", 30, 20, 30, 40)
        };

        var strategy = new SectioningStrategy();

        var result = emitter.EmitBladeCut("Test BladeCut", 45.0, segments, "E015", 15, strategy);

        Assert.Contains("NAME=ROUTG", result);
        Assert.Contains("ANG,VALUE=45.0", result);
        Assert.Contains("DP,VALUE=15.0", result);
        Assert.Contains("START_POINT,X=10.00000,Y=20.00000", result);
        Assert.Contains("LINE_EP,X=30.00000,Y=20.00000", result);
        Assert.Contains("LINE_EP,X=30.00000,Y=40.00000", result);
        Assert.Contains("ENDPATH", result);
    }

    [Fact]
    public void EmitterRouter_RoutesBladeCutMachining()
    {
        var nameService = new NameService();
        var emitter = new XilogEmitter(nameService);
        var profile = new ScmProfile();
        var router = new EmitterRouter(emitter, nameService, profile);

        var segments = new BladeCutSegment[]
        {
            new("Cut segment_1", 10, 20, 30, 20)
        };

        var bladeCut = new BladeCutMachining
        {
            Name = "Test BladeCut",
            Angle = 45.0,
            Segments = segments,
            Depth = 15.0,
            TechCode = "E015",
            Side = MachiningSide.Top,
            Source = MachiningSource.BlockDetection
        };

        var plate = new Plate
        {
            Name = "TestPlate",
            LengthX = 100,
            WidthY = 50,
            Thickness = 19,
            Machinings = new[] { bladeCut }
        };

        var result = router.GenerateProgram(plate);

        Assert.Contains("CreateSectioningMillingStrategy", result);
        Assert.Contains("CreateSegment", result);
        Assert.Contains("CreateBladeCut", result);
        Assert.Contains("E015", result);
    }

    [Fact]
    public void BlockUserTextSchema_ValidatesBladeCutType()
    {
        var attributes = new Dictionary<string, string>
        {
            { BlockUserTextSchema.CNC_TYPE, "BLADECUT" },
            { BlockUserTextSchema.CNC_ANGLE, "45.0" },
            { BlockUserTextSchema.CNC_DEPTH, "15" }
        };

        var (isValid, error) = BlockUserTextSchema.Validate(attributes);

        Assert.True(isValid);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("seg1,10,20,30,40", 1)] // single segment
    [InlineData("seg1,10,20,30,40;seg2,40,30,50,60", 2)] // two segments
    [InlineData("", 0)] // empty
    [InlineData("invalid", 0)] // invalid format
    public void ParseBladeCutSegments_HandlesVariousFormats(string segmentStr, int expectedCount)
    {
        var block = new FittingBlock
        {
            BlockName = "Test",
            CncType = "BLADECUT",
            InsertionPoint = (0, 0, 0),
            Rotation = 0,
            CncAttributes = new Dictionary<string, string>
            {
                { BlockUserTextSchema.CNC_TYPE, "BLADECUT" },
                { BlockUserTextSchema.CNC_SEGMENTS, segmentStr }
            }
        };

        var machinings = MachiningFactory.CreateFromBlock(block, 0, 0, 0, 19);
        var bladeCut = Assert.IsType<BladeCutMachining>(machinings[0]);

        if (expectedCount == 0 && string.IsNullOrEmpty(segmentStr))
        {
            // Should get default segments
            Assert.Equal(4, bladeCut.Segments.Count);
        }
        else if (expectedCount == 0)
        {
            // Invalid format should result in default segments  
            Assert.Equal(4, bladeCut.Segments.Count);
        }
        else
        {
            Assert.Equal(expectedCount, bladeCut.Segments.Count);
        }
    }

    [Fact]
    public void BladeCutMachining_DefaultStrategy()
    {
        var segments = new BladeCutSegment[]
        {
            new("Cut segment_1", 10, 20, 30, 20)
        };

        var bladeCut = new BladeCutMachining
        {
            Name = "Test BladeCut",
            Angle = 45.0,
            Segments = segments,
            Depth = 15.0,
            TechCode = "E015"
        };

        Assert.Equal(5, bladeCut.Strategy.StrategyType);
        Assert.Equal(0, bladeCut.Strategy.OffsetX);
        Assert.Equal(0, bladeCut.Strategy.OffsetY);
    }
}