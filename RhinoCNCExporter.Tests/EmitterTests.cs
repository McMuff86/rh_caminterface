using System.Collections.Generic;
using System.Globalization;
using RhinoCNCExporter.Core.Emitters;
using RhinoCNCExporter.Core.LayerParser;
using RhinoCNCExporter.Core.Naming;
using RhinoCNCExporter.Core.Profiles;
using Xunit;

namespace RhinoCNCExporter.Tests;

public class EmitterTests
{
    private XilogEmitter CreateEmitter(out NameService names)
    {
        names = new NameService(31);
        return new XilogEmitter(names);
    }

    #region Header / Footer — Production Format

    [Fact]
    public void Header_Has_Production_CommentBlocks()
    {
        var emitter = CreateEmitter(out _);
        var header = emitter.EmitHeader("Boden", 813.5, 380, 19);

        Assert.Contains("// *** Programm created by RhinoCNCExporter ***", header);
        Assert.Contains("//**********************************************************", header);
        Assert.Contains("// *** Programmparameter setzen ***", header);
        Assert.Contains("SetMachiningParameters(\"IJ\",1,10,196608,false);", header);
        Assert.Contains("// *** Bauteil erstellen ***", header);
        Assert.Contains("// *** Bauteil Infos ***", header);
        Assert.Contains("// *** Bauteil Offsets ***", header);
    }

    [Fact]
    public void Header_DZ_WholeNumber_NoDecimals()
    {
        var emitter = CreateEmitter(out _);
        var header = emitter.EmitHeader("test", 800, 300, 19);

        // Production format: "double DZ = 19;" not "double DZ = 19.000;"
        Assert.Contains("double DZ = 19;", header);
        Assert.DoesNotContain("double DZ = 19.000;", header);
    }

    [Fact]
    public void Header_DZ_Fractional_ShowsDecimals()
    {
        var emitter = CreateEmitter(out _);
        var header = emitter.EmitHeader("test", 800, 300, 18.5);

        Assert.Contains("double DZ = 18.5;", header);
    }

    [Fact]
    public void Header_WorkpieceBox_CompactFormat()
    {
        var emitter = CreateEmitter(out _);
        var header = emitter.EmitHeader("Seite_rechts", 300, 280, 19);

        // Production: integer dimensions without .000
        Assert.Contains("CreateFinishedWorkpieceBox(\"Seite_rechts\", 300, 280, 19);", header);
    }

    [Fact]
    public void Header_WorkpieceBox_FractionalDimensions()
    {
        var emitter = CreateEmitter(out _);
        var header = emitter.EmitHeader("Boden", 813.5, 380, 19);

        Assert.Contains("CreateFinishedWorkpieceBox(\"Boden\", 813.5, 380, 19);", header);
    }

    [Fact]
    public void Header_SetupOffset_DefaultValues()
    {
        var emitter = CreateEmitter(out _);
        var header = emitter.EmitHeader("test", 800, 300, 19);

        // Default: 2.5, 2.5, 0, 0 — compact format
        Assert.Contains("SetWorkpieceSetupPosition(2.5,2.5,0,0);", header);
    }

    [Fact]
    public void Header_SetupOffset_CustomValues()
    {
        var emitter = CreateEmitter(out _);
        var header = emitter.EmitHeader("test", 800, 300, 19,
            setupOffsetX: 5.0, setupOffsetY: 3.5, setupOffsetZ: 0, setupOffsetRot: 0);

        Assert.Contains("SetWorkpieceSetupPosition(5,3.5,0,0);", header);
    }

    [Fact]
    public void Header_Contains_CommentedProjectInfo()
    {
        var emitter = CreateEmitter(out _);
        var header = emitter.EmitHeader("Seite_rechts", 300, 280, 19);

        Assert.Contains("//CreateMessage(\"Projekt\",\"projekt_name\",false,false);", header);
        Assert.Contains("//CreateMessage(\"Datei\",\"Seite_rechts.xcs\",false,false);", header);
        Assert.Contains("//CreateMessage(\"Bemerkung\",\" \",false,false);", header);
    }

    [Fact]
    public void Footer_Has_Production_Format()
    {
        var emitter = CreateEmitter(out _);
        var footer = emitter.EmitFooter();

        Assert.Contains("CreateMacro(\"Wegfahrschritt\",\"XPARK\");", footer);
        Assert.Contains("//**********************************************************", footer);
        Assert.Contains("// *** Programm Ende ***", footer);
        Assert.Contains("// Macro RNT", footer);
    }

    #endregion

    #region EmitDrill — unchanged format

    [Fact]
    public void EmitDrill_Matches_Reference_Format()
    {
        var emitter = CreateEmitter(out _);
        var drill = emitter.EmitDrill("DRILLROW_1_1", 150.0, 57.0, 13.0, 5.0, "Top", "P");

        Assert.Contains("SelectWorkplane(\"Top\");", drill);
        Assert.Contains("CreateDrill(\"DRILLROW_1_1\",150.000,57.000,13.000,5.000,\"\",TypeOfProcess.Drilling,\"-1\",\"-1\",1,-1,-1,\"P\");", drill);
        Assert.Contains("ResetPattern();", drill);
    }

    #endregion

    #region EmitDrillPattern — NEW

    [Fact]
    public void EmitDrillPattern_Produces_CreatePattern()
    {
        var emitter = CreateEmitter(out _);
        var result = emitter.EmitDrillPattern("Vertikale Bohrung_1", 24, 75, 14, 15,
            xCount: 1, yCount: 4, xSpacing: 0, ySpacing: 64);

        Assert.Contains("SelectWorkplane(\"Top\");", result);
        Assert.Contains("CreateDrill(\"Vertikale Bohrung_1\",24.000,75.000,14.000,15.000,\"\",TypeOfProcess.Drilling,\"-1\",\"-1\",1,-1,-1,\"P\");", result);
        Assert.Contains("CreatePattern(1,4,0,64,0,90);", result);
        Assert.Contains("ResetPattern();", result);
    }

    [Fact]
    public void EmitDrillPattern_FractionalSpacing()
    {
        var emitter = CreateEmitter(out _);
        var result = emitter.EmitDrillPattern("Pat_1", 10, 20, 13, 5,
            xCount: 3, yCount: 2, xSpacing: 32.5, ySpacing: 64);

        Assert.Contains("CreatePattern(3,2,32.5,64,0,90);", result);
    }

    [Fact]
    public void EmitDrillPattern_Static_Emit_Class()
    {
        var emitter = CreateEmitter(out var names);
        var spec = new DrillPatternSpec(5.0, 13.0, 'P', 1, 4, 0, 64);
        var result = EmitDrillPattern.Emit(emitter, names, "DRILLPAT_1", 24, 75, spec);

        Assert.Contains("CreateDrill(", result);
        Assert.Contains("CreatePattern(1,4,0,64,0,90);", result);
        Assert.Contains("ResetPattern();", result);
    }

    #endregion

    #region EmitPolylinePass — unchanged format

    [Fact]
    public void EmitPolylinePass_Matches_Reference_Format()
    {
        var emitter = CreateEmitter(out _);
        var pts = new List<(double, double)>
        {
            (1120.0, 300.0),
            (0.0, 300.0),
            (0.0, 0.0),
            (2240.0, 0.0),
            (2240.0, 300.0),
            (1120.0, 300.0),
        };

        var pass = emitter.EmitPolylinePass("CUT_1", "CUT_1_OP", pts, "E010", 19.0, 9.5);

        Assert.Contains("SelectWorkplane(\"Top\");", pass);
        Assert.Contains("CreatePolyline(\"CUT_1\", 1120.000,300.000);", pass);
        Assert.Contains("AddSegmentToPolyline(0.000,300.000);", pass);
        Assert.Contains("SetCompensationMode(false);", pass);
        Assert.Contains("SetApproachStrategy(false,true,2);", pass);
        Assert.Contains("SetRetractStrategy(false,true,2.0,2);", pass);
        Assert.Contains("SetPneumaticHoodPosition(null);", pass);
        Assert.Contains("CreateRoughFinish(\"CUT_1_OP\",19.000,\"\", TypeOfProcess.GeneralRouting ,\"E010\",\"-1\",2,-1,-1,-1,0);", pass);
        Assert.Contains("ResetApproachStrategy();", pass);
        Assert.Contains("ResetRetractStrategy();", pass);
    }

    #endregion

    #region EmitPolylinePassWithArcs — NEW

    [Fact]
    public void EmitPolylinePassWithArcs_MixedSegments()
    {
        var emitter = CreateEmitter(out _);
        var segments = new List<PolySegment>
        {
            new(883.5, 0, IsArc: false),                                      // Line
            new(903.5, 20, IsArc: true, CenterX: 883.5, CenterY: 20, Clockwise: false), // Arc
            new(903.5, 280, IsArc: false),                                     // Line
            new(883.5, 300, IsArc: true, CenterX: 883.5, CenterY: 280, Clockwise: false), // Arc
        };

        var result = emitter.EmitPolylinePassWithArcs("Poly_1", "Poly_1_OP",
            451.75, 0, segments, "E010", 19, 9.5);

        Assert.Contains("CreatePolyline(\"Poly_1\", 451.750,0.000);", result);
        Assert.Contains("AddSegmentToPolyline(883.500,0.000);", result);
        Assert.Contains("AddArc2PointCenterToPolyline(903.500,20.000,883.500,20.000,false);", result);
        Assert.Contains("AddSegmentToPolyline(903.500,280.000);", result);
        Assert.Contains("AddArc2PointCenterToPolyline(883.500,300.000,883.500,280.000,false);", result);
        Assert.Contains("CreateRoughFinish(", result);
    }

    [Fact]
    public void EmitPolylinePassWithArcs_ClockwiseArc()
    {
        var emitter = CreateEmitter(out _);
        var segments = new List<PolySegment>
        {
            new(100, 50, IsArc: true, CenterX: 100, CenterY: 0, Clockwise: true),
        };

        var result = emitter.EmitPolylinePassWithArcs("Arc_1", "Arc_1_OP",
            50, 0, segments, "E010", 10, 6);

        Assert.Contains("AddArc2PointCenterToPolyline(100.000,50.000,100.000,0.000,true);", result);
    }

    #endregion

    #region EmitRnt — unchanged

    [Fact]
    public void EmitRntX_Matches_test_02_Reference()
    {
        var emitter = CreateEmitter(out _);
        var rnt = emitter.EmitRntX("Nut_in_X_Richtung", -5.0, 280.0, 6.0, 2250.0, 8.0, "066");

        Assert.Contains("SelectWorkplane(\"Top\");", rnt);
        Assert.Contains("CreateMacro(\"Nut_in_X_Richtung\",\"RNT\",-5.000,280.000,6.000,-1,-1,-1,2250.000,8.000,true,\"066\",\"-1\",false,false,true,280.000,null,null,null,null,true);", rnt);
    }

    #endregion

    #region EmitWorkplane — NEW

    [Fact]
    public void EmitWorkplane_Creates_FreeWorkplane()
    {
        var emitter = CreateEmitter(out _);
        var result = emitter.EmitWorkplane("Freie Ebene_803", 0, 43, 9.5, -90, 90);

        Assert.Contains("CreateWorkplane(\"Freie Ebene_803\",0,43,9.5,-90,90);", result);
    }

    [Fact]
    public void EmitSelectWorkplane_SelectsNamedPlane()
    {
        var emitter = CreateEmitter(out _);
        var result = emitter.EmitSelectWorkplane("Freie Ebene_803");

        Assert.Contains("SelectWorkplane(\"Freie Ebene_803\");", result);
    }

    #endregion

    #region EmitHorizontalDrill — NEW

    [Fact]
    public void EmitHorizontalDrill_LeftSide()
    {
        var emitter = CreateEmitter(out var names);
        var spec = new HorizontalDrillSpec(8, 30, 'L');
        var result = EmitHorizontalDrill.Emit(emitter, names, "HDRILL_1",
            0, 43, dz: 19, dx: 300, dy: 280, spec);

        Assert.Contains("CreateWorkplane(", result);
        Assert.Contains("-90", result); // rotX
        Assert.Contains("90", result);  // rotY
        Assert.Contains("CreateDrill(", result);
        Assert.Contains("30.000", result); // depth
        Assert.Contains("8.000", result);  // diameter
    }

    [Fact]
    public void EmitHorizontalDrill_RightSide()
    {
        var emitter = CreateEmitter(out var names);
        var spec = new HorizontalDrillSpec(8, 25, 'R');
        var result = EmitHorizontalDrill.Emit(emitter, names, "HDRILL_2",
            300, 100, dz: 19, dx: 300, dy: 280, spec);

        Assert.Contains("CreateWorkplane(", result);
        Assert.Contains("CreateDrill(", result);
    }

    #endregion

    #region EmitCut — multi-pass

    [Fact]
    public void EmitCut_SinglePass_No_Stepdown()
    {
        var emitter = CreateEmitter(out var names);
        var pts = new List<(double, double)> { (0, 0), (100, 0), (100, 50), (0, 50), (0, 0) };
        var spec = new CutSpec("E010", 19.0, null, 9.5);

        var result = EmitCut.Emit(emitter, names, "CUT_1", pts, spec, false);
        Assert.Contains("CreatePolyline(\"CUT_1\"", result);
        Assert.Contains("CreateRoughFinish(\"CUT_1_OP\",19.000", result);
    }

    [Fact]
    public void EmitCut_LayerStepdown_MultiPass()
    {
        var emitter = CreateEmitter(out var names);
        var pts = new List<(double, double)> { (0, 0), (100, 0), (100, 50), (0, 50), (0, 0) };
        var spec = new CutSpec("E010", 10.0, 3.0, 9.5);

        var result = EmitCut.Emit(emitter, names, "CUT_1", pts, spec, true);
        Assert.Contains("3.0", result);
        Assert.Contains("6.0", result);
        Assert.Contains("9.0", result);
        Assert.Contains("10.0", result);
    }

    #endregion

    #region EmitDrillRow

    [Fact]
    public void EmitDrillRow_CorrectCount()
    {
        var emitter = CreateEmitter(out var names);
        var points = new List<(double, double)>
        {
            (150.0, 57.0), (182.0, 57.0), (214.0, 57.0)
        };
        var spec = new DrillRowSpec(5.0, 13.0, 32.0, null);

        var result = EmitRow.Emit(emitter, names, "DRILLROW_1", points, spec);
        Assert.Contains("DRILLROW_1_1", result);
        Assert.Contains("DRILLROW_1_2", result);
        Assert.Contains("DRILLROW_1_3", result);
        Assert.Contains("150.000,57.000,13.000,5.000", result);
    }

    #endregion

    #region EmitGrooveRnt

    [Fact]
    public void EmitGrooveRnt_XAxis_Matches_Reference()
    {
        var emitter = CreateEmitter(out var names);
        var ends = new EmitGrooveRnt.GrooveEndpoints
        {
            XStart = -5.0, XEnd = 2245.0,
            YCenter = 280.0,
            YStart = 277.0, YEnd = 283.0
        };
        var spec = new GrooveRntSpec(Axis.X, 6.0, 8.0, "066", Place.Center);

        var result = EmitGrooveRnt.Emit(emitter, names, "RBNUT_RNT_1", ends, spec);
        Assert.Contains("Nut_in_X_Richtung", result);
        Assert.Contains("RNT", result);
        Assert.Contains("-5.000", result);
        Assert.Contains("280.000", result);
        Assert.Contains("6.000", result);
        Assert.Contains("2250.000", result);
        Assert.Contains("8.000", result);
        Assert.Contains("066", result);
    }

    #endregion

    #region Biesse Emitter Tests

    private BiesseEmitter CreateBiesseEmitter(out NameService names)
    {
        names = new NameService(63);
        return new BiesseEmitter(names);
    }

    [Fact]
    public void BiesseHeader_Contains_CIX_Structure()
    {
        var emitter = CreateBiesseEmitter(out _);
        var header = emitter.EmitHeader("test_biesse", 800.0, 320.0, 18.0);

        Assert.Contains("BEGIN ID CID3", header);
        Assert.Contains("REL= 5.0", header);
        Assert.Contains("END ID", header);
        Assert.Contains("BEGIN MAINDATA", header);
        Assert.Contains("LPX=800.00000", header);
        Assert.Contains("LPY=320.00000", header);
        Assert.Contains("LPZ=18.00000", header);
        Assert.Contains("ORLST=\"1\"", header);
        Assert.Contains("MATERIAL=\"wood\"", header);
        Assert.Contains("END MAINDATA", header);
        Assert.Contains("\r\n", header);
    }

    [Fact]
    public void BiesseDrill_Uses_BG_Macro()
    {
        var emitter = CreateBiesseEmitter(out _);
        var drill = emitter.EmitDrill("Drill_1", 150.0, 57.0, 13.0, 5.0, "Top", "P");

        Assert.Contains("BEGIN MACRO", drill);
        Assert.Contains("NAME=BG", drill);
        Assert.Contains("PARAM,NAME=ID,VALUE=\"Drill_1\"", drill);
        Assert.Contains("PARAM,NAME=SIDE,VALUE=0", drill);
        Assert.Contains("PARAM,NAME=X,VALUE=150.0", drill);
        Assert.Contains("PARAM,NAME=Y,VALUE=57.0", drill);
        Assert.Contains("PARAM,NAME=DP,VALUE=13.0", drill);
        Assert.Contains("PARAM,NAME=DIA,VALUE=5.0", drill);
        Assert.Contains("PARAM,NAME=THR,VALUE=YES", drill);
        Assert.Contains("END MACRO", drill);
        Assert.Contains("\r\n", drill);
    }

    [Fact]
    public void BiesseDrillPattern_Uses_RTY_Grid()
    {
        var emitter = CreateBiesseEmitter(out _);
        var result = emitter.EmitDrillPattern("Pat_1", 24, 75, 14, 15,
            xCount: 1, yCount: 4, xSpacing: 0, ySpacing: 64);

        Assert.Contains("NAME=BG", result);
        Assert.Contains("PARAM,NAME=RTY,VALUE=rpGRD", result);
        Assert.Contains("PARAM,NAME=DX,VALUE=0.0", result);
        Assert.Contains("PARAM,NAME=DY,VALUE=64.0", result);
        Assert.Contains("PARAM,NAME=NRX,VALUE=1", result);
        Assert.Contains("PARAM,NAME=NRY,VALUE=4", result);
    }

    [Fact]
    public void BiessePolylinePass_Uses_GEO_ROUTG()
    {
        var emitter = CreateBiesseEmitter(out _);
        var pts = new List<(double, double)>
        {
            (0.0, 0.0),
            (100.0, 0.0),
            (100.0, 50.0),
            (0.0, 50.0),
            (0.0, 0.0)
        };

        var pass = emitter.EmitPolylinePass("Cut_1", "Cut_1_OP", pts, "T01", 18.0, 10.0);

        Assert.Contains("NAME=GEO", pass);
        Assert.Contains("PARAM,NAME=ID,VALUE=\"G1003.1001\"", pass);
        Assert.Contains("NAME=START_POINT", pass);
        Assert.Contains("PARAM,NAME=X,VALUE=0.0", pass);
        Assert.Contains("PARAM,NAME=Y,VALUE=0.0", pass);
        Assert.Contains("NAME=LINE_EP", pass);
        Assert.Contains("PARAM,NAME=XE,VALUE=100.0", pass);
        Assert.Contains("NAME=ENDPATH", pass);
        Assert.Contains("NAME=ROUTG", pass);
        Assert.Contains("PARAM,NAME=TNM,VALUE=\"T01\"", pass);
        Assert.Contains("PARAM,NAME=DP,VALUE=18.0", pass);
        Assert.Contains("PARAM,NAME=DIA,VALUE=10.0", pass);
        Assert.Contains("\r\n", pass);
    }

    [Fact]
    public void BiessePolylinePassWithArcs_Uses_ARC_EPCE()
    {
        var emitter = CreateBiesseEmitter(out _);
        var segments = new List<PolySegment>
        {
            new(100, 0, IsArc: false),
            new(120, 20, IsArc: true, CenterX: 100, CenterY: 20, Clockwise: false),
        };

        var result = emitter.EmitPolylinePassWithArcs("P1", "P1_OP", 0, 0, segments, "T01", 18, 10);

        Assert.Contains("NAME=LINE_EP", result);
        Assert.Contains("PARAM,NAME=XE,VALUE=100.0", result);
        Assert.Contains("NAME=ARC_EPCE", result);
        Assert.Contains("PARAM,NAME=XE,VALUE=120.0", result);
        Assert.Contains("PARAM,NAME=XC,VALUE=100.0", result);
        Assert.Contains("PARAM,NAME=YC,VALUE=20.0", result);
        Assert.Contains("PARAM,NAME=DIR,VALUE=dirCCW", result);
    }

    [Fact]
    public void BiesseRntX_Converts_To_Rectangular_Route()
    {
        var emitter = CreateBiesseEmitter(out _);
        var result = emitter.EmitRntX("Groove_X", 10.0, 150.0, 6.0, 200.0, 8.0, "T02");

        Assert.Contains("NAME=GEO", result);
        Assert.Contains("NAME=ROUTG", result);
        Assert.Contains("PARAM,NAME=DP,VALUE=8.0", result);
        Assert.Contains("PARAM,NAME=TNM,VALUE=\"T02\"", result);
        Assert.Contains("\r\n", result);
    }

    #endregion

    #region Interface Tests

    [Fact]
    public void XilogEmitter_Implements_IEmitter()
    {
        var names = new NameService(31);
        IEmitter emitter = new XilogEmitter(names);
        
        Assert.NotNull(emitter);
        var header = emitter.EmitHeader("test", 100, 100, 19);
        Assert.Contains("CreateFinishedWorkpieceBox", header);
    }

    [Fact]
    public void BiesseEmitter_Implements_IEmitter()
    {
        var names = new NameService(63);
        IEmitter emitter = new BiesseEmitter(names);
        
        Assert.NotNull(emitter);
        var header = emitter.EmitHeader("test", 100, 100, 18);
        Assert.Contains("BEGIN MAINDATA", header);
    }

    [Fact]
    public void BiesseProfile_Implements_IMachineProfile()
    {
        IMachineProfile profile = new BiesseProfile();
        
        Assert.Equal(18.0, profile.DefaultDz);
        Assert.Equal(10.0, profile.DefaultToolDiameter);
        Assert.Equal(63, profile.MaxNameLength);
        Assert.Equal(".cix", profile.FileExtension);
        Assert.True(profile.UseRntMacro);
        Assert.True(profile.UseCornerRounding);
        Assert.Equal("T01", profile.DefaultTech);
    }

    [Fact]
    public void MachineProfile_Has_SetupOffsets()
    {
        IMachineProfile profile = new MaestroCadTProfile();

        Assert.Equal(2.5, profile.SetupOffsetX);
        Assert.Equal(2.5, profile.SetupOffsetY);
        Assert.Equal(0.0, profile.SetupOffsetZ);
        Assert.Equal(0.0, profile.SetupOffsetRot);
    }

    #endregion
}
