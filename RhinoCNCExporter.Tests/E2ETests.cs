using System;
using System.IO;
using System.Linq;
using RhinoCNCExporter.Core.Emitters;
using RhinoCNCExporter.Core.Models;
using RhinoCNCExporter.Core.Naming;
using Xunit;

namespace RhinoCNCExporter.Tests;

/// <summary>
/// End-to-end tests comparing C# emitter output against reference XCS files.
/// Validates that our C# implementation matches production format.
/// </summary>
public class E2ETests
{
    private readonly string _testDataPath = Path.Combine(Environment.CurrentDirectory, "..", "..", "..", "..", "tests");

    [Fact]
    public void XilogEmitter_Header_Matches_Production_Format()
    {
        var nameService = new NameService(31);
        var emitter = new XilogEmitter(nameService);
        
        var header = emitter.EmitHeader("Seite_rechts", 300, 280, 19);
        
        // Production format checks
        Assert.Contains("// *** Programm created by RhinoCNCExporter ***", header);
        Assert.Contains("//**********************************************************", header);
        Assert.Contains("// *** Programmparameter setzen ***", header);
        Assert.Contains("SetMachiningParameters(\"IJ\",1,10,196608,false);", header);
        Assert.Contains("// *** Bauteil erstellen ***", header);
        Assert.Contains("CreateFinishedWorkpieceBox(\"Seite_rechts\", 300, 280, 19);", header);
        Assert.Contains("// *** Bauteil Infos ***", header);
        Assert.Contains("//CreateMessage(\"Projekt\"", header);
        Assert.Contains("//CreateMessage(\"Datei\",\"Seite_rechts.xcs\"", header);
        Assert.Contains("double DZ = 19;", header);
        Assert.Contains("// *** Bauteil Offsets ***", header);
        Assert.Contains("SetWorkpieceSetupPosition(2.5,2.5,0,0);", header);
    }

    [Fact]
    public void XilogEmitter_Footer_Matches_Production_Format()
    {
        var nameService = new NameService(31);
        var emitter = new XilogEmitter(nameService);
        
        var footer = emitter.EmitFooter();
        
        Assert.Contains("// Macro RNT", footer);
        Assert.Contains("CreateMacro(\"Wegfahrschritt\",\"XPARK\");", footer);
        Assert.Contains("// *** Programm Ende ***", footer);
        Assert.Contains("//**********************************************************", footer);
    }

    [Fact]
    public void XilogEmitter_Drill_Pattern_Matches_Production()
    {
        var nameService = new NameService(31);
        var emitter = new XilogEmitter(nameService);
        
        var drill = emitter.EmitDrill("DRILLROW_1_1", 150.0, 57.0, 13.0, 5.0, "Top", "P");
        
        var expectedPattern = "CreateDrill(\"DRILLROW_1_1\",150.000,57.000,13.000,5.000,\"\",TypeOfProcess.Drilling,\"-1\",\"-1\",1,-1,-1,\"P\");";
        Assert.Contains(expectedPattern, drill);
        Assert.Contains("SelectWorkplane(\"Top\");", drill);
        Assert.Contains("ResetPattern();", drill);
    }

    [Fact]
    public void XilogEmitter_DrillPattern_Matches_Production()
    {
        var nameService = new NameService(31);
        var emitter = new XilogEmitter(nameService);
        
        // From production (Staub / Mittelseite): CreatePattern → CreateDrill → ResetPattern
        var result = emitter.EmitDrillPattern("Vertikale Bohrung_1", 24, 75, 14, 15,
            xCount: 1, yCount: 4, xSpacing: 0, ySpacing: 64);
        
        Assert.Contains("CreateDrill(\"Vertikale Bohrung_1\",24.000,75.000,14.000,15.000", result);
        Assert.Contains("CreatePattern(1,4,0,64,0,90);", result);
        Assert.Contains("ResetPattern();", result);
    }

    [Fact]
    public void XilogEmitter_RNT_Matches_Pattern()
    {
        var nameService = new NameService(31);
        var emitter = new XilogEmitter(nameService);
        
        var rnt = emitter.EmitRntX("RBNUT_RNT_1", -5.0, 280.0, 6.0, 8.0, 8.0, "066");
        
        Assert.Contains("SelectWorkplane(\"Top\");", rnt);
        Assert.Contains("CreateMacro(\"RBNUT_RNT_1\",\"RNT\"", rnt);
        Assert.Contains("-5.000", rnt);
        Assert.Contains("280.000", rnt);
        Assert.Contains("6.000", rnt);
        Assert.Contains("8.000", rnt);
        Assert.Contains("\"066\"", rnt);
    }

    [Fact]
    public void XilogEmitter_Cut_Polyline_Structure_Matches_Reference()
    {
        var nameService = new NameService(31);
        var emitter = new XilogEmitter(nameService);
        
        var pts = new[]
        {
            (1120.0, 300.0),
            (0.0, 300.0),
            (0.0, 0.0),
            (2240.0, 0.0),
            (2240.0, 300.0),
            (1120.0, 300.0)
        };
        
        var polyPass = emitter.EmitPolylinePass("CUT_1", "CUT_1_OP", pts, "E010", 19.0, 9.5);
        
        Assert.Contains("CreatePolyline(\"CUT_1\", 1120.000,300.000);", polyPass);
        Assert.Contains("AddSegmentToPolyline(0.000,300.000);", polyPass);
        Assert.Contains("AddSegmentToPolyline(0.000,0.000);", polyPass);
        Assert.Contains("AddSegmentToPolyline(2240.000,0.000);", polyPass);
        Assert.Contains("AddSegmentToPolyline(2240.000,300.000);", polyPass);
        Assert.Contains("AddSegmentToPolyline(1120.000,300.000);", polyPass);
        Assert.Contains("CreateRoughFinish(\"CUT_1_OP\",19.000,\"\", TypeOfProcess.GeneralRouting ,\"E010\"", polyPass);
    }

    [Fact]
    public void XilogEmitter_PolylineWithArcs_Reference()
    {
        var nameService = new NameService(31);
        var emitter = new XilogEmitter(nameService);
        
        // From production: 2_16_1_Revsionsdeckel.xcs
        var segments = new PolySegment[]
        {
            new(883.5, 0),
            new(903.5, 20, IsArc: true, CenterX: 883.5, CenterY: 20, Clockwise: false),
            new(903.5, 280),
            new(883.5, 300, IsArc: true, CenterX: 883.5, CenterY: 280, Clockwise: false),
        };
        
        var result = emitter.EmitPolylinePassWithArcs("Polylinie_2", "Polylinie_2_OP",
            451.75, 0, segments, "E010", 19, 9.5);
        
        Assert.Contains("CreatePolyline(\"Polylinie_2\", 451.750,0.000);", result);
        Assert.Contains("AddSegmentToPolyline(883.500,0.000);", result);
        Assert.Contains("AddArc2PointCenterToPolyline(903.500,20.000,883.500,20.000,false);", result);
        Assert.Contains("AddSegmentToPolyline(903.500,280.000);", result);
        Assert.Contains("AddArc2PointCenterToPolyline(883.500,300.000,883.500,280.000,false);", result);
    }

    [Fact]
    public void XilogEmitter_Workplane_For_HorizontalDrill()
    {
        var nameService = new NameService(31);
        var emitter = new XilogEmitter(nameService);
        
        // From production: CreateWorkplane("Freie Ebene_803",0,43,-9.5+DZ,-90.000,90);
        var wp = emitter.EmitWorkplane("Freie Ebene_803", 0, 43, 9.5, -90, 90);
        
        Assert.Contains("CreateWorkplane(\"Freie Ebene_803\",0,43,9.5,-90,90);", wp);
    }

    [Fact]
    public void XilogEmitter_HorizontalDrill_Uses_Production_Signature()
    {
        var nameService = new NameService(31);
        var emitter = new XilogEmitter(nameService);

        var drill = emitter.EmitHorizontalDrill("Horizontal freie Bohrung_1_L", 30, 8, "Freie_Ebene_803");

        Assert.Contains("SelectWorkplane(\"Freie_Ebene_803\");", drill);
        Assert.Contains("CreateDrill(\"Horizontal freie Bohrung_1_L\",0.000,0.000,30.000,8.000,\"\",TypeOfProcess.Drilling,\"\",\"-1\",1,-1,-1,\"P\",0,0);", drill);
    }

    [Theory]
    [InlineData("test_01.xcs")]
    [InlineData("test_02.xcs")]
    public void Reference_Files_Exist_And_Are_Readable(string filename)
    {
        var filePath = Path.Combine(_testDataPath, filename);
        
        Assert.True(File.Exists(filePath), $"Reference file {filename} should exist at {filePath}");
        
        var content = File.ReadAllText(filePath);
        Assert.False(string.IsNullOrEmpty(content), $"Reference file {filename} should not be empty");
        Assert.Contains("CreateFinishedWorkpieceBox", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BiesseEmitter_Produces_Valid_CIX_Structure()
    {
        var nameService = new NameService(63);
        var emitter = new BiesseEmitter(nameService);
        
        var header = emitter.EmitHeader("test_biesse", 800.0, 320.0, 18.0);
        var drill = emitter.EmitDrill("Drill_1", 100.0, 50.0, 10.0, 5.0);
        var footer = emitter.EmitFooter();
        
        Assert.Contains("BEGIN ID CID3", header);
        Assert.Contains("BEGIN MAINDATA", header);
        Assert.Contains("LPX=800.00000", header);
        Assert.Contains("END MAINDATA", header);
        Assert.Contains("\r\n", header);
        
        Assert.Contains("NAME=BG", drill);
        Assert.Contains("PARAM,NAME=X,VALUE=100.0", drill);
        Assert.Contains("\r\n", drill);
        
        Assert.Equal("", footer);
    }

    [Fact]
    public void Production_Header_Structure_Matches_RealFiles()
    {
        // Compare our header structure against real production file structure
        var refPath = Path.Combine(_testDataPath, "references", "1_1_1_Seite_rechts.xcs");
        if (!File.Exists(refPath)) return; // Skip if reference not available

        var refContent = File.ReadAllText(refPath);
        
        var nameService = new NameService(31);
        var emitter = new XilogEmitter(nameService);
        var ourHeader = emitter.EmitHeader("Seite_rechts", 300, 280, 19);
        
        // Both should have the same structural elements
        Assert.Contains("SetMachiningParameters(\"IJ\",1,10,196608,false);", refContent);
        Assert.Contains("SetMachiningParameters(\"IJ\",1,10,196608,false);", ourHeader);
        
        Assert.Contains("double DZ = 19;", refContent);
        Assert.Contains("double DZ = 19;", ourHeader);
        
        Assert.Contains("SetWorkpieceSetupPosition(2.5,2.5,0,0);", refContent);
        Assert.Contains("SetWorkpieceSetupPosition(2.5,2.5,0,0);", ourHeader);
        
        Assert.Contains("//**********************************************************", refContent);
        Assert.Contains("//**********************************************************", ourHeader);
    }

    [Fact]
    public void BladeCut_E2E_MatchesProductionReference()
    {
        var nameService = new NameService(31);
        var emitter = new XilogEmitter(nameService);
        
        var segments = new BladeCutSegment[]
        {
            new("Cut segment_1", 19, 354, 19, -187.5),
            new("Cut segment_2", 628, -187.5, 628, 354)
        };

        var strategy = new SectioningStrategy(5, 0, 0);

        var bladeCutOutput = emitter.EmitBladeCut("Geneigter Schnitt in X/Y_1", 45.0, segments, "E015", 15, strategy);

        // Verify all required components are present in correct order
        var lines = bladeCutOutput.Split('\n');
        
        var selectIdx = Array.FindIndex(lines, l => l.Contains("SelectWorkplane"));
        var strategyIdx = Array.FindIndex(lines, l => l.Contains("CreateSectioningMillingStrategy"));
        var approachIdx = Array.FindIndex(lines, l => l.Contains("SetApproachStrategy"));
        var retractIdx = Array.FindIndex(lines, l => l.Contains("SetRetractStrategy"));
        var segment1Idx = Array.FindIndex(lines, l => l.Contains("CreateSegment(\"Cut segment_1\""));
        var segment2Idx = Array.FindIndex(lines, l => l.Contains("CreateSegment(\"Cut segment_2\""));
        var bladeCutIdx = Array.FindIndex(lines, l => l.Contains("CreateBladeCut"));
        var resetApproachIdx = Array.FindIndex(lines, l => l.Contains("ResetApproachStrategy"));
        var resetRetractIdx = Array.FindIndex(lines, l => l.Contains("ResetRetractStrategy"));

        // Verify order: Select → Strategy → Approach → Retract → Segments → BladeCut → Resets
        Assert.True(selectIdx < strategyIdx);
        Assert.True(strategyIdx < approachIdx);
        Assert.True(approachIdx < retractIdx);
        Assert.True(retractIdx < segment1Idx);
        Assert.True(segment1Idx < segment2Idx);
        Assert.True(segment2Idx < bladeCutIdx);
        Assert.True(bladeCutIdx < resetApproachIdx);
        Assert.True(resetApproachIdx < resetRetractIdx);

        // Verify exact format matches production
        Assert.Contains("SelectWorkplane(\"Top\");", bladeCutOutput);
        Assert.Contains("CreateSectioningMillingStrategy(5,0,0);", bladeCutOutput);
        Assert.Contains("SetApproachStrategy(true,true,0);", bladeCutOutput);
        Assert.Contains("SetRetractStrategy(true,true,0,0);", bladeCutOutput);
        Assert.Contains("CreateSegment(\"Cut segment_1\",19.000,354.000,19.000,-187.500);", bladeCutOutput);
        Assert.Contains("CreateSegment(\"Cut segment_2\",628.000,-187.500,628.000,354.000);", bladeCutOutput);
        Assert.Contains("CreateBladeCut(\"Geneigter Schnitt in X/Y_1\",\"Blade Cut\",TypeOfProcess.GeneralRouting,\"E015\",\"-1\",45.00,2,-1,-1,-1,2,true,true,0,15);", bladeCutOutput);
        Assert.Contains("ResetApproachStrategy();", bladeCutOutput);
        Assert.Contains("ResetRetractStrategy();", bladeCutOutput);
    }

    [Fact]  
    public void HelicMillingStrategy_E2E_BasicFormat()
    {
        var nameService = new NameService(31);
        var emitter = new XilogEmitter(nameService);

        var helicOutput = emitter.EmitHelicMillingStrategy(8.5, true, 17);

        Assert.Equal("CreateHelicMillingStrategy(8.5,true,17);\n", helicOutput);
    }
}
