using RhinoCNCExporter.Core.Models;
using Xunit;

namespace RhinoCNCExporter.Tests;

public class ModelTests
{
    // --- Plate ---

    [Fact]
    public void Plate_RequiredProperties_Set()
    {
        var plate = new Plate { Name = "Seite_links", LengthX = 800, WidthY = 400, Thickness = 19 };
        Assert.Equal("Seite_links", plate.Name);
        Assert.Equal(800, plate.LengthX);
        Assert.Equal(400, plate.WidthY);
        Assert.Equal(19, plate.Thickness);
    }

    [Fact]
    public void Plate_Defaults_AreCorrect()
    {
        var plate = new Plate { Name = "Test", LengthX = 100, WidthY = 50, Thickness = 19 };
        Assert.Null(plate.Material);
        Assert.Null(plate.LayerPath);
        Assert.Equal(PlateSource.LegacyLayer, plate.Source);
        Assert.Empty(plate.Machinings);
        Assert.NotNull(plate.Origin);
        Assert.Equal(0, plate.Origin.OriginX);
    }

    [Fact]
    public void Plate_WithMachinings()
    {
        var drill = new DrillMachining { Name = "D1", X = 10, Y = 20, Depth = 13, Diameter = 5 };
        var plate = new Plate
        {
            Name = "Test",
            LengthX = 800,
            WidthY = 400,
            Thickness = 19,
            Machinings = new[] { drill }
        };
        Assert.Single(plate.Machinings);
        Assert.IsType<DrillMachining>(plate.Machinings[0]);
    }

    [Fact]
    public void Plate_Record_Equality()
    {
        var p1 = new Plate { Name = "A", LengthX = 100, WidthY = 50, Thickness = 19 };
        var p2 = new Plate { Name = "A", LengthX = 100, WidthY = 50, Thickness = 19 };
        Assert.Equal(p1, p2);
    }

    [Fact]
    public void Plate_WithModification()
    {
        var plate = new Plate { Name = "A", LengthX = 100, WidthY = 50, Thickness = 19 };
        var modified = plate with { Name = "B" };
        Assert.Equal("B", modified.Name);
        Assert.Equal(100, modified.LengthX);
    }

    // --- PlateOrigin ---

    [Fact]
    public void PlateOrigin_Identity()
    {
        var id = PlateOrigin.Identity;
        Assert.Equal(0, id.OriginX);
        Assert.Equal(0, id.OriginY);
        Assert.Equal(0, id.OriginZ);
        Assert.Equal((1, 0, 0), id.XAxis);
        Assert.Equal((0, 1, 0), id.YAxis);
        Assert.Equal((0, 0, 1), id.Normal);
    }

    [Fact]
    public void PlateOrigin_Custom()
    {
        var origin = new PlateOrigin
        {
            OriginX = 100,
            OriginY = 200,
            OriginZ = 300,
            XAxis = (0, 1, 0),
            YAxis = (0, 0, 1),
            Normal = (1, 0, 0)
        };
        Assert.Equal(100, origin.OriginX);
        Assert.Equal((0, 1, 0), origin.XAxis);
    }

    // --- Machining subtypes ---

    [Fact]
    public void DrillMachining_Properties()
    {
        var drill = new DrillMachining
        {
            Name = "Topfband_35",
            X = 50, Y = 100, Depth = 13, Diameter = 35,
            Side = MachiningSide.Top,
            TechCode = "E009",
            Source = MachiningSource.BlockDetection
        };
        Assert.Equal(50, drill.X);
        Assert.Equal(35, drill.Diameter);
        Assert.Equal(MachiningSide.Top, drill.Side);
        Assert.Equal(MachiningSource.BlockDetection, drill.Source);
    }

    [Fact]
    public void DrillPatternMachining_Properties()
    {
        var dp = new DrillPatternMachining
        {
            Name = "Lochreihe_32",
            X = 37, Y = 96, Depth = 13, Diameter = 5,
            CountX = 1, CountY = 10, SpacingX = 0, SpacingY = 32
        };
        Assert.Equal(10, dp.CountY);
        Assert.Equal(32, dp.SpacingY);
    }

    [Fact]
    public void RoutingMachining_ClosedContour()
    {
        var routing = new RoutingMachining
        {
            Name = "Cut1",
            Points = new[] { (0.0, 0.0), (100.0, 0.0), (100.0, 50.0), (0.0, 50.0), (0.0, 0.0) },
            Depth = 19, ToolDiameter = 9.5, IsClosed = true
        };
        Assert.True(routing.IsClosed);
        Assert.Equal(5, routing.Points.Count);
    }

    [Fact]
    public void MacroMachining_Properties()
    {
        var macro = new MacroMachining
        {
            Name = "CLAMEX_P14",
            MacroName = "SawCut_Lamello",
            Parameters = new[] { "9.5", "100", null, "0" }
        };
        Assert.Equal("SawCut_Lamello", macro.MacroName);
        Assert.Equal(4, macro.Parameters.Count);
        Assert.Null(macro.Parameters[2]);
    }

    [Fact]
    public void HorizontalDrillMachining_Properties()
    {
        var hdrill = new HorizontalDrillMachining
        {
            Name = "HDrill_L",
            X = 50, Y = 100, Depth = 30, Diameter = 8,
            DrillSide = 'L', Side = MachiningSide.Left
        };
        Assert.Equal('L', hdrill.DrillSide);
        Assert.Equal(MachiningSide.Left, hdrill.Side);
    }

    [Fact]
    public void GrooveRntMachining_Properties()
    {
        var groove = new GrooveRntMachining
        {
            Name = "RNT_1",
            Axis = Core.LayerParser.Axis.X,
            XStart = 10, YStart = 200, Length = 780, Width = 5.5,
            Depth = 8.3, RntCode = "066"
        };
        Assert.Equal(Core.LayerParser.Axis.X, groove.Axis);
        Assert.Equal("066", groove.RntCode);
    }

    [Fact]
    public void PocketMachining_Properties()
    {
        var pocket = new PocketMachining
        {
            Name = "Pocket1",
            Loops = new[] { new[] { (0.0, 0.0), (50.0, 0.0), (50.0, 30.0), (0.0, 30.0), (0.0, 0.0) } },
            Depth = 8, ToolDiameter = 6
        };
        Assert.Single(pocket.Loops);
        Assert.Equal(5, pocket.Loops[0].Count);
    }

    [Fact]
    public void Machining_DefaultValues()
    {
        var drill = new DrillMachining { Name = "D1", X = 0, Y = 0, Depth = 5, Diameter = 5 };
        Assert.Equal(MachiningSide.Top, drill.Side);
        Assert.Null(drill.TechCode);
        Assert.Equal(MachiningSource.LegacyLayer, drill.Source);
    }

    // --- FittingBlock ---

    [Fact]
    public void FittingBlock_BasicProperties()
    {
        var attrs = new Dictionary<string, string>
        {
            ["CNC_Type"] = "DRILL",
            ["CNC_Diameter"] = "35",
            ["CNC_Depth"] = "13",
            ["CNC_Side"] = "TOP"
        };

        var block = new FittingBlock
        {
            BlockName = "Topfband_35",
            CncType = "DRILL",
            InsertionPoint = (100.0, 200.0, 0.0),
            CncAttributes = attrs
        };

        Assert.Equal("Topfband_35", block.BlockName);
        Assert.Equal(35.0, block.Diameter);
        Assert.Equal(13.0, block.Depth);
        Assert.Equal(MachiningSide.Top, block.CncSide);
    }

    [Fact]
    public void FittingBlock_MacroAccessors()
    {
        var attrs = new Dictionary<string, string>
        {
            ["CNC_Type"] = "MACRO",
            ["CNC_MacroName"] = "SawCut_Lamello",
            ["CNC_MacroParams"] = "{DZ}-9.5,{Y},{DZ}-9.5",
            ["CNC_Orientation"] = "90"
        };

        var block = new FittingBlock
        {
            BlockName = "CLAMEX_P14",
            CncType = "MACRO",
            InsertionPoint = (50, 100, 0),
            CncAttributes = attrs
        };

        Assert.Equal("SawCut_Lamello", block.MacroName);
        Assert.Equal("{DZ}-9.5,{Y},{DZ}-9.5", block.MacroParams);
        Assert.Equal("90", block.Orientation);
    }

    [Fact]
    public void FittingBlock_Through_ParsesBool()
    {
        var attrs = new Dictionary<string, string>
        {
            ["CNC_Type"] = "DRILL",
            ["CNC_Through"] = "true"
        };
        var block = new FittingBlock
        {
            BlockName = "Test", CncType = "DRILL",
            InsertionPoint = (0, 0, 0), CncAttributes = attrs
        };
        Assert.True(block.Through);
    }

    [Fact]
    public void FittingBlock_NullableAccessors_ReturnNull()
    {
        var attrs = new Dictionary<string, string> { ["CNC_Type"] = "DRILL" };
        var block = new FittingBlock
        {
            BlockName = "Test", CncType = "DRILL",
            InsertionPoint = (0, 0, 0), CncAttributes = attrs
        };
        Assert.Null(block.Diameter);
        Assert.Null(block.Depth);
        Assert.Null(block.MacroName);
        Assert.Null(block.CncSide);
        Assert.False(block.Through);
    }

    [Fact]
    public void FittingBlock_InvalidSide_ReturnsNull()
    {
        var attrs = new Dictionary<string, string>
        {
            ["CNC_Type"] = "DRILL",
            ["CNC_Side"] = "INVALID"
        };
        var block = new FittingBlock
        {
            BlockName = "Test", CncType = "DRILL",
            InsertionPoint = (0, 0, 0), CncAttributes = attrs
        };
        Assert.Null(block.CncSide);
    }

    // --- ExportJob ---

    [Fact]
    public void ExportJob_Properties()
    {
        var plate = new Plate { Name = "P1", LengthX = 800, WidthY = 400, Thickness = 19 };
        var job = new ExportJob
        {
            Plates = new[] { plate },
            Format = MachineFormat.Xilog,
            OutputDirectory = "/tmp/export",
            ProfileName = "MaestroCadT"
        };
        Assert.Single(job.Plates);
        Assert.Equal(MachineFormat.Xilog, job.Format);
        Assert.True(job.UseLegacyLayers);
        Assert.False(job.UseBlockDetection);
    }

    // --- Enums ---

    [Fact]
    public void MachineFormat_Values()
    {
        Assert.Equal(0, (int)MachineFormat.Xilog);
        Assert.Equal(1, (int)MachineFormat.Biesse);
        Assert.Equal(2, (int)MachineFormat.Homag);
    }

    [Fact]
    public void MachiningSide_AllValues()
    {
        Assert.Equal(6, Enum.GetValues<MachiningSide>().Length);
    }

    [Fact]
    public void MachiningSource_AllValues()
    {
        Assert.Equal(3, Enum.GetValues<MachiningSource>().Length);
    }
}
