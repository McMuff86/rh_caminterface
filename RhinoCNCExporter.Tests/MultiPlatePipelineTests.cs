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
/// Integration tests for the multi-plate pipeline:
/// CoordinateTransformer → MachiningFactory → EmitterRouter
/// AssignmentResolver tests are in AssignmentResolverTests.cs (via Plugin project or mock).
/// </summary>
public class MultiPlatePipelineTests
{
    // === Full Pipeline: Plate + Blocks → CNC Program ===

    [Fact]
    public void FullPipeline_FlatPlate_DrillBlock_ProducesXCS()
    {
        var plate = CreateFlatPlate("Seite_links", 300, 700, 19);
        var block = CreateBlock("Topfband_35", "DRILL", "Seite_links", 50, 100, 0);

        // Transform
        var (localX, localY, localZ) = CoordinateTransformer.WorldToPlateLocal(
            plate.Origin, block.InsertionPoint.X, block.InsertionPoint.Y, block.InsertionPoint.Z);

        // Create machining
        var attrs = new Dictionary<string, string>
        {
            ["CNC_Type"] = "DRILL",
            ["CNC_Diameter"] = "35",
            ["CNC_Depth"] = "13",
            ["CNC_Side"] = "TOP"
        };
        var fittingBlock = new FittingBlock
        {
            BlockName = "Topfband_35",
            CncType = "DRILL",
            InsertionPoint = (50, 100, 0),
            CncAttributes = attrs
        };
        var machinings = MachiningFactory.CreateFromBlock(fittingBlock, localX, localY, localZ, plate.Thickness);

        Assert.Single(machinings);
        var drill = Assert.IsType<DrillMachining>(machinings[0]);
        Assert.Equal(50, drill.X, 0.001);
        Assert.Equal(100, drill.Y, 0.001);
        Assert.Equal(35, drill.Diameter);
        Assert.Equal(13, drill.Depth);

        // Emit
        var plateWithMachinings = plate with { Machinings = machinings };
        var emitter = new XilogEmitter(new NameService());
        var nameService = new NameService();
        var profile = new MaestroCadTProfile();
        var router = new EmitterRouter(emitter, nameService, profile);
        var program = router.GenerateProgram(plateWithMachinings);

        Assert.Contains("Seite_links", program);
        Assert.Contains("CreateDrill", program);
    }

    [Fact]
    public void FullPipeline_ClamexVertical_ProducesCreateMacro()
    {
        var plate = CreateFlatPlate("Seite_links", 300, 700, 19);

        // Create CLAMEX machining
        var block = new FittingBlock
        {
            BlockName = "CLAMEX_P14",
            CncType = "MACRO",
            InsertionPoint = (9.5, 50.03, 0),
            Rotation = 270,
            CncAttributes = new Dictionary<string, string>
            {
                ["CNC_Type"] = "MACRO",
                ["CNC_MacroName"] = "SawCut_Lamello",
                ["CNC_Orientation"] = "0",
                ["CNC_Side"] = "TOP"
            }
        };

        var clamexMachining = ClamexMacroBuilder.CreateMachining(block, 9.5, 50.03, 19, 1);
        Assert.NotNull(clamexMachining);

        var plateWithMachinings = plate with { Machinings = new[] { clamexMachining } };
        var emitter = new XilogEmitter(new NameService());
        var nameService = new NameService();
        var profile = new MaestroCadTProfile();
        var router = new EmitterRouter(emitter, nameService, profile);
        var program = router.GenerateProgram(plateWithMachinings);

        Assert.Contains("CreateMacro(", program);
        Assert.Contains("SawCut_Lamello", program);
        Assert.Contains("E015", program);
        Assert.Contains("E004", program);
    }

    [Fact]
    public void FullPipeline_ClamexHorizontal_ProducesCreateMacroWithDzOffset()
    {
        var plate = CreateFlatPlate("Boden", 268, 300, 19);

        var block = new FittingBlock
        {
            BlockName = "CLAMEX_P14",
            CncType = "MACRO",
            InsertionPoint = (0, 60.03, 0),
            Rotation = 270,
            CncAttributes = new Dictionary<string, string>
            {
                ["CNC_Type"] = "MACRO",
                ["CNC_MacroName"] = "SawCut_Lamello",
                ["CNC_Orientation"] = "90",
                ["CNC_Side"] = "TOP"
            }
        };

        var clamexMachining = ClamexMacroBuilder.CreateMachining(block, 0, 60.03, 19, 1);
        Assert.NotNull(clamexMachining);

        var plateWithMachinings = plate with { Machinings = new[] { clamexMachining } };
        var emitter = new XilogEmitter(new NameService());
        var nameService = new NameService();
        var profile = new MaestroCadTProfile();
        var router = new EmitterRouter(emitter, nameService, profile);
        var program = router.GenerateProgram(plateWithMachinings);

        Assert.Contains("CreateMacro(", program);
        Assert.Contains("SawCut_Lamello", program);
        Assert.Contains("DZ-9.5", program);
        Assert.Contains("E005", program);
    }

    [Fact]
    public void FullPipeline_MixedOperations_AllEmitted()
    {
        var plate = CreateFlatPlate("Seite_links", 300, 700, 19);

        var drill = new DrillMachining
        {
            Name = "Topfband",
            X = 50,
            Y = 100,
            Depth = 13,
            Diameter = 35,
            Side = MachiningSide.Top,
            Source = MachiningSource.BlockDetection
        };

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
        var clamex = ClamexMacroBuilder.CreateMachining(clamexBlock, 9.5, 200, 19, 1)!;

        var plateWithMachinings = plate with { Machinings = new Machining[] { drill, clamex } };
        var emitter = new XilogEmitter(new NameService());
        var nameService = new NameService();
        var profile = new MaestroCadTProfile();
        var router = new EmitterRouter(emitter, nameService, profile);
        var program = router.GenerateProgram(plateWithMachinings);

        Assert.Contains("CreateDrill", program);
        Assert.Contains("CreateMacro", program);
        Assert.Contains("SawCut_Lamello", program);
    }

    [Fact]
    public void FullPipeline_UprightPlate_CoordinateTransformApplied()
    {
        // Side panel standing upright in XZ plane
        var origin = CoordinateTransformer.CreateUprightXZOrigin(0, 0, 0);
        var plate = new Plate
        {
            Name = "Seite_links",
            LengthX = 600,   // World X dimension
            WidthY = 700,    // World Z dimension (height)
            Thickness = 19,
            Origin = origin,
            Source = PlateSource.SolidDetection
        };

        // Block at world position (300, 0, 350) — middle of plate face
        var (localX, localY, localZ) = CoordinateTransformer.WorldToPlateLocal(
            origin, 300, 0, 350);

        Assert.Equal(300, localX, 0.001);   // Along plate length
        Assert.Equal(350, localY, 0.001);   // Along plate height
        Assert.Equal(0, localZ, 0.001);     // On the face
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

    private static FittingBlock CreateBlock(string name, string cncType, string layer,
        double x, double y, double z)
    {
        var attrs = new Dictionary<string, string>
        {
            ["CNC_Type"] = cncType
        };
        if (cncType == "DRILL")
        {
            attrs["CNC_Diameter"] = "35";
            attrs["CNC_Depth"] = "13";
        }
        else if (cncType == "MACRO")
        {
            attrs["CNC_MacroName"] = "SawCut_Lamello";
            attrs["CNC_Orientation"] = "0";
        }

        return new FittingBlock
        {
            BlockName = name,
            CncType = cncType,
            InsertionPoint = (x, y, z),
            CncAttributes = attrs,
            LayerName = layer
        };
    }
}
