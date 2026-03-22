using System.Collections.Generic;
using System.Globalization;
using RhinoCNCExporter.Core.Emitters;
using RhinoCNCExporter.Core.LayerParser;
using RhinoCNCExporter.Core.Naming;
using Xunit;

namespace RhinoCNCExporter.Tests;

public class EmitterTests
{
    private XilogEmitter CreateEmitter(out NameService names)
    {
        names = new NameService(31);
        return new XilogEmitter(names);
    }

    [Fact]
    public void Header_Matches_Python_Reference()
    {
        var emitter = CreateEmitter(out _);
        var header = emitter.EmitHeader("test_01", 2240.0, 300.0, 19.0);

        Assert.Contains("// *** Programm created by Rhino→Maestro Generator ***", header);
        Assert.Contains("SetMachiningParameters(\"IJ\",1,10,196608,false);", header);
        Assert.Contains("CreateFinishedWorkpieceBox(\"test_01\", 2240.000, 300.000, 19.000);", header);
        Assert.Contains("double DZ = 19.000;", header);
        Assert.Contains("SetWorkpieceSetupPosition(2.5,2.5,0,0);", header);
    }

    [Fact]
    public void Footer_Is_XPARK()
    {
        var emitter = CreateEmitter(out _);
        var footer = emitter.EmitFooter();
        Assert.Contains("CreateMacro(\"Wegfahrschritt\",\"XPARK\");", footer);
    }

    [Fact]
    public void EmitDrill_Matches_Reference_Format()
    {
        var emitter = CreateEmitter(out _);
        var drill = emitter.EmitDrill("DRILLROW_1_1", 150.0, 57.0, 13.0, 5.0, "Top", "P");

        Assert.Contains("SelectWorkplane(\"Top\");", drill);
        Assert.Contains("CreateDrill(\"DRILLROW_1_1\",150.000,57.000,13.000,5.000,\"\",TypeOfProcess.Drilling,\"-1\",\"-1\",1,-1,-1,\"P\");", drill);
        Assert.Contains("ResetPattern();", drill);
    }

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
        Assert.Contains("AddSegmentToPolyline(0.000,0.000);", pass);
        Assert.Contains("AddSegmentToPolyline(2240.000,0.000);", pass);
        Assert.Contains("AddSegmentToPolyline(2240.000,300.000);", pass);
        Assert.Contains("AddSegmentToPolyline(1120.000,300.000);", pass);
        Assert.Contains("SetCompensationMode(false);", pass);
        Assert.Contains("SetApproachStrategy(false,true,2);", pass);
        Assert.Contains("SetRetractStrategy(false,true,2.0,2);", pass);
        Assert.Contains("SetPneumaticHoodPosition(null);", pass);
        Assert.Contains("CreateRoughFinish(\"CUT_1_OP\",19.000,\"\", TypeOfProcess.GeneralRouting ,\"E010\",\"-1\",2,-1,-1,-1,0);", pass);
        Assert.Contains("ResetApproachStrategy();", pass);
        Assert.Contains("ResetRetractStrategy();", pass);
    }

    [Fact]
    public void EmitRntX_Matches_test_02_Reference()
    {
        var emitter = CreateEmitter(out _);
        // test_02.xcs: CreateMacro("Nut_in_X_Richtung","RNT",-5.000,280.000,6.000,-1,-1,-1,2250.000,8.000,true,"066","-1",false,false,true,280.000,null,null,null,null,true);
        var rnt = emitter.EmitRntX("Nut_in_X_Richtung", -5.0, 280.0, 6.0, 2250.0, 8.0, "066");

        Assert.Contains("SelectWorkplane(\"Top\");", rnt);
        Assert.Contains("CreateMacro(\"Nut_in_X_Richtung\",\"RNT\",-5.000,280.000,6.000,-1,-1,-1,2250.000,8.000,true,\"066\",\"-1\",false,false,true,280.000,null,null,null,null,true);", rnt);
    }

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
        // 10/3 = ceil(3.33) = 4 passes: 3, 6, 9, 10
        Assert.Contains("3.0", result);
        Assert.Contains("6.0", result);
        Assert.Contains("9.0", result);
        Assert.Contains("10.0", result);
    }

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
        Assert.Contains("2250.000", result); // xLen = 2245 - (-5) = 2250
        Assert.Contains("8.000", result);
        Assert.Contains("066", result);
    }
}
