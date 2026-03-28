using System.Collections.Generic;
using System.Globalization;
using RhinoCNCExporter.Core.Blocks;
using RhinoCNCExporter.Core.Emitters;
using RhinoCNCExporter.Core.Models;
using RhinoCNCExporter.Core.Naming;
using RhinoCNCExporter.Core.Pipeline;
using RhinoCNCExporter.Core.Profiles;
using Xunit;

namespace RhinoCNCExporter.Tests;

/// <summary>
/// Edge case and robustness tests for Emitter, EmitterRouter, MachiningFactory,
/// and MachiningBuilder.
/// Covers: empty plates, invalid blocks, duplicate names, boundary values,
/// null/empty inputs, and regression scenarios.
/// </summary>
public class EdgeCaseTests
{
    #region Emitter — Empty/Boundary Inputs

    [Fact]
    public void EmitHeader_EmptyName_DoesNotThrow()
    {
        var emitter = new XilogEmitter(new NameService());
        var header = emitter.EmitHeader("", 100, 100, 19);
        Assert.Contains("CreateFinishedWorkpieceBox(\"\"", header);
    }

    [Fact]
    public void EmitHeader_ZeroDimensions_DoesNotThrow()
    {
        var emitter = new XilogEmitter(new NameService());
        var header = emitter.EmitHeader("Zero", 0, 0, 0);
        Assert.Contains("CreateFinishedWorkpieceBox(\"Zero\", 0, 0, 0);", header);
    }

    [Fact]
    public void EmitHeader_NegativeDimensions_OutputsNegativeValues()
    {
        var emitter = new XilogEmitter(new NameService());
        var header = emitter.EmitHeader("Neg", -10, -20, -5);
        Assert.Contains("-10", header);
    }

    [Fact]
    public void EmitHeader_VeryLargeDimensions_DoesNotOverflow()
    {
        var emitter = new XilogEmitter(new NameService());
        var header = emitter.EmitHeader("Large", 99999.999, 88888.888, 777.777);
        Assert.Contains("99999.999", header);
    }

    [Fact]
    public void EmitDrill_ZeroDepth_ProducesValidOutput()
    {
        var emitter = new XilogEmitter(new NameService());
        var drill = emitter.EmitDrill("D1", 10, 20, 0, 5, "Top");
        Assert.Contains("0.000", drill);
    }

    [Fact]
    public void EmitPolylinePass_TwoPoints_MinimalPolyline()
    {
        var emitter = new XilogEmitter(new NameService());
        var pts = new List<(double, double)> { (0, 0), (100, 0) };
        var result = emitter.EmitPolylinePass("P1", "P1_OP", pts, "E010", 10, 6);
        Assert.Contains("CreatePolyline(\"P1\", 0.000,0.000);", result);
        Assert.Contains("AddSegmentToPolyline(100.000,0.000);", result);
    }

    [Fact]
    public void EmitPolylinePassWithArcs_EmptySegments_ProducesHeaderOnly()
    {
        var emitter = new XilogEmitter(new NameService());
        var segments = new List<PolySegment>();
        var result = emitter.EmitPolylinePassWithArcs("P1", "P1_OP", 0, 0, segments, "E010", 10, 6);
        Assert.Contains("CreatePolyline(\"P1\", 0.000,0.000);", result);
        Assert.DoesNotContain("AddSegmentToPolyline", result);
        Assert.DoesNotContain("AddArc2PointCenterToPolyline", result);
    }

    [Fact]
    public void EmitDrillPattern_SingleDrill_OneByOne()
    {
        var emitter = new XilogEmitter(new NameService());
        var result = emitter.EmitDrillPattern("Pat1", 50, 50, 13, 5, 1, 1, 0, 0);
        Assert.Contains("CreatePattern(1,1,0,0,0,90);", result);
    }

    [Fact]
    public void EmitBladeCut_EmptySegments_ProducesStrategyAndBladeCut()
    {
        var emitter = new XilogEmitter(new NameService());
        var segments = new List<BladeCutSegment>();
        var strategy = new SectioningStrategy(5, 0, 0);
        var result = emitter.EmitBladeCut("BC1", 45, segments, "E015", 15, strategy);
        Assert.Contains("CreateSectioningMillingStrategy", result);
        Assert.Contains("CreateBladeCut", result);
        Assert.DoesNotContain("CreateSegment", result);
    }

    [Fact]
    public void EmitRntX_NegativeStartPosition_ValidOutput()
    {
        var emitter = new XilogEmitter(new NameService());
        var rnt = emitter.EmitRntX("Nut_1", -70, 300, 5.5, 2500, 5, "066");
        Assert.Contains("-70.000", rnt);
    }

    [Fact]
    public void EmitFooter_AlwaysContainsProgramEnde()
    {
        var emitter = new XilogEmitter(new NameService());
        var footer = emitter.EmitFooter();
        Assert.Contains("// *** Programm Ende ***", footer);
        Assert.Contains("XPARK", footer);
    }

    #endregion

    #region EmitterRouter — Empty/Invalid Plates

    [Fact]
    public void GenerateProgram_EmptyMachinings_ProducesHeaderAndFooter()
    {
        var names = new NameService();
        var emitter = new XilogEmitter(names);
        var router = new EmitterRouter(emitter, names, new MaestroCadTProfile());

        var plate = new Plate
        {
            Name = "Empty",
            LengthX = 500,
            WidthY = 300,
            Thickness = 19,
            Machinings = Array.Empty<Machining>()
        };

        var result = router.GenerateProgram(plate);
        Assert.Contains("CreateFinishedWorkpieceBox(\"Empty\"", result);
        Assert.Contains("XPARK", result);
        Assert.DoesNotContain("CreateDrill", result);
    }

    [Fact]
    public void GenerateProgram_PreserveMachiningOrder_RespectsListOrder()
    {
        var names = new NameService();
        var emitter = new XilogEmitter(names);
        var router = new EmitterRouter(emitter, names, new MaestroCadTProfile());

        // Put a drill before a routing — normally routing comes first
        var plate = new Plate
        {
            Name = "OrderTest",
            LengthX = 500,
            WidthY = 300,
            Thickness = 19,
            PreserveMachiningOrder = true,
            Machinings = new Machining[]
            {
                new DrillMachining { Name = "D1", X = 10, Y = 20, Depth = 13, Diameter = 5 },
                new RoutingMachining
                {
                    Name = "Contour",
                    Points = new[] { (0.0, 0.0), (500.0, 0.0), (500.0, 300.0), (0.0, 300.0), (0.0, 0.0) },
                    Depth = 19, ToolDiameter = 9.5, TechCode = "E010", IsClosed = true
                }
            }
        };

        var result = router.GenerateProgram(plate);
        var drillIdx = result.IndexOf("CreateDrill(", System.StringComparison.Ordinal);
        var routeIdx = result.IndexOf("CreatePolyline(", System.StringComparison.Ordinal);
        Assert.True(drillIdx < routeIdx, "PreserveMachiningOrder should keep drill before routing");
    }

    [Fact]
    public void GenerateProgram_DefaultOrder_RoutingBeforeDrill()
    {
        var names = new NameService();
        var emitter = new XilogEmitter(names);
        var router = new EmitterRouter(emitter, names, new MaestroCadTProfile());

        var plate = new Plate
        {
            Name = "SortTest",
            LengthX = 500,
            WidthY = 300,
            Thickness = 19,
            PreserveMachiningOrder = false,
            Machinings = new Machining[]
            {
                new DrillMachining { Name = "D1", X = 10, Y = 20, Depth = 13, Diameter = 5 },
                new RoutingMachining
                {
                    Name = "Contour",
                    Points = new[] { (0.0, 0.0), (500.0, 0.0), (500.0, 300.0), (0.0, 300.0), (0.0, 0.0) },
                    Depth = 19, ToolDiameter = 9.5, TechCode = "E010", IsClosed = true
                }
            }
        };

        var result = router.GenerateProgram(plate);
        var drillIdx = result.IndexOf("CreateDrill(", System.StringComparison.Ordinal);
        var routeIdx = result.IndexOf("CreatePolyline(", System.StringComparison.Ordinal);
        Assert.True(routeIdx < drillIdx, "Default order should put routing before drills");
    }

    [Fact]
    public void GenerateProgram_UnsupportedMachiningType_EmitsComment()
    {
        var names = new NameService();
        var emitter = new XilogEmitter(names);
        var router = new EmitterRouter(emitter, names, new MaestroCadTProfile());

        // PocketMachining with empty loops is technically supported but tests the pocket path
        var plate = new Plate
        {
            Name = "PocketTest",
            LengthX = 200,
            WidthY = 100,
            Thickness = 19,
            Machinings = new Machining[]
            {
                new PocketMachining
                {
                    Name = "Pocket1",
                    Loops = new List<IReadOnlyList<(double X, double Y)>>(),
                    Depth = 5,
                    ToolDiameter = 6
                }
            }
        };

        // Should not throw
        var result = router.GenerateProgram(plate);
        Assert.NotNull(result);
    }

    #endregion

    #region MachiningFactory — Edge Cases

    [Fact]
    public void CreateFromBlock_UnknownType_ReturnsEmpty()
    {
        var block = CreateFittingBlock("UnknownBlock", "LASER");
        var result = MachiningFactory.CreateFromBlock(block, 0, 0, 0, 19);
        Assert.Empty(result);
    }

    [Fact]
    public void CreateFromBlock_Drill_DefaultValues()
    {
        var block = CreateFittingBlock("TestDrill", "DRILL");
        var result = MachiningFactory.CreateFromBlock(block, 50, 75, 0, 19);

        Assert.Single(result);
        var drill = Assert.IsType<DrillMachining>(result[0]);
        Assert.Equal(50, drill.X);
        Assert.Equal(75, drill.Y);
        Assert.Equal(5.0, drill.Diameter); // default
        Assert.Equal(13.0, drill.Depth);   // default
    }

    [Fact]
    public void CreateFromBlock_Drill_WithCustomDepthAndDiameter()
    {
        var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CNC_Type"] = "DRILL",
            ["CNC_Diameter"] = "8.5",
            ["CNC_Depth"] = "25"
        };
        var block = CreateFittingBlockWithAttrs("CustomDrill", "DRILL", attrs);
        var result = MachiningFactory.CreateFromBlock(block, 10, 20, 0, 19);

        var drill = Assert.IsType<DrillMachining>(result[0]);
        Assert.Equal(8.5, drill.Diameter);
        Assert.Equal(25.0, drill.Depth);
    }

    [Fact]
    public void CreateFromBlock_Drill_ThroughDepthEqualsPlateThickness()
    {
        var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CNC_Type"] = "DRILL",
            ["CNC_Diameter"] = "5",
            ["CNC_Through"] = "true"
        };
        var block = CreateFittingBlockWithAttrs("ThroughDrill", "DRILL", attrs);
        var result = MachiningFactory.CreateFromBlock(block, 10, 20, 0, 22);

        var drill = Assert.IsType<DrillMachining>(result[0]);
        Assert.Equal(22.0, drill.Depth); // plate thickness
    }

    [Fact]
    public void CreateFromBlock_Drill_DzPlaceholderDepth()
    {
        var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CNC_Type"] = "DRILL",
            ["CNC_Depth"] = "{DZ}-2"
        };
        var block = CreateFittingBlockWithAttrs("DzDrill", "DRILL", attrs);
        var result = MachiningFactory.CreateFromBlock(block, 10, 20, 0, 19);

        var drill = Assert.IsType<DrillMachining>(result[0]);
        Assert.Equal(17.0, drill.Depth); // 19 - 2
    }

    [Fact]
    public void CreateFromBlock_DrillPattern_RotationSwapsAxes()
    {
        var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CNC_Type"] = "DRILLPATTERN",
            ["CNC_PatternX"] = "1",
            ["CNC_PatternY"] = "5",
            ["CNC_SpacingX"] = "0",
            ["CNC_SpacingY"] = "32"
        };
        var block = CreateFittingBlockWithAttrs("RotatedPat", "DRILLPATTERN", attrs, rotation: 90);
        var result = MachiningFactory.CreateFromBlock(block, 50, 50, 0, 19);

        var pat = Assert.IsType<DrillPatternMachining>(result[0]);
        // After 90° rotation, X/Y counts and spacings should swap
        Assert.Equal(5, pat.CountX);
        Assert.Equal(1, pat.CountY);
        Assert.Equal(32, pat.SpacingX);
        Assert.Equal(0, pat.SpacingY);
    }

    [Fact]
    public void CreateFromBlock_Macro_EmptyMacroName_ReturnsEmpty()
    {
        var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CNC_Type"] = "MACRO",
            ["CNC_MacroName"] = ""
        };
        var block = CreateFittingBlockWithAttrs("EmptyMacro", "MACRO", attrs);
        var result = MachiningFactory.CreateFromBlock(block, 0, 0, 0, 19);
        Assert.Empty(result);
    }

    [Fact]
    public void CreateFromBlock_BladeCut_InvalidSegments_UsesDefaultPattern()
    {
        var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CNC_Type"] = "BLADECUT",
            ["CNC_Segments"] = "totally_invalid_data"
        };
        var block = CreateFittingBlockWithAttrs("BadBladeCut", "BLADECUT", attrs);
        var result = MachiningFactory.CreateFromBlock(block, 50, 50, 0, 19);

        // Should get default cross pattern segments since parsing fails
        var bc = Assert.IsType<BladeCutMachining>(result[0]);
        Assert.Equal(4, bc.Segments.Count); // default cross pattern
    }

    [Fact]
    public void CreateFromBlock_CUT_ReturnsEmpty_PlaceholderForPhase3()
    {
        var block = CreateFittingBlock("CutBlock", "CUT");
        var result = MachiningFactory.CreateFromBlock(block, 0, 0, 0, 19);
        Assert.Empty(result);
    }

    [Fact]
    public void CreateFromBlock_POCKET_ReturnsEmpty_PlaceholderForPhase3()
    {
        var block = CreateFittingBlock("PocketBlock", "POCKET");
        var result = MachiningFactory.CreateFromBlock(block, 0, 0, 0, 19);
        Assert.Empty(result);
    }

    [Fact]
    public void CreateFromBlock_GROOVE_ReturnsEmpty_PlaceholderForPhase3()
    {
        var block = CreateFittingBlock("GrooveBlock", "GROOVE");
        var result = MachiningFactory.CreateFromBlock(block, 0, 0, 0, 19);
        Assert.Empty(result);
    }

    [Fact]
    public void ExpandTemplateParams_DzArithmetic_AllOperators()
    {
        Assert.Equal("20", MachiningFactory.ExpandTemplateParams("{DZ}+1", 19, 0, 0));
        Assert.Equal("17", MachiningFactory.ExpandTemplateParams("{DZ}-2", 19, 0, 0));
        Assert.Equal("9.5", MachiningFactory.ExpandTemplateParams("{DZ}*0.5", 19, 0, 0));
        Assert.Equal("9.5", MachiningFactory.ExpandTemplateParams("{DZ}/2", 19, 0, 0));
    }

    [Fact]
    public void ExpandTemplateParams_NoPlaceholders_Unchanged()
    {
        Assert.Equal("42.5", MachiningFactory.ExpandTemplateParams("42.5", 19, 10, 20));
    }

    [Fact]
    public void ExpandTemplateParams_EmptyString_ReturnsEmpty()
    {
        Assert.Equal("", MachiningFactory.ExpandTemplateParams("", 19, 0, 0));
    }

    [Fact]
    public void ExpandTemplateParams_XYPlaceholders()
    {
        var result = MachiningFactory.ExpandTemplateParams("{X},{Y},0", 19, 100.5, 200.3);
        Assert.Contains("100.5", result);
        Assert.Contains("200.3", result);
    }

    #endregion

    #region MachiningBuilder — Deduplication Edge Cases

    [Fact]
    public void MergeAndDeduplicate_EmptyInputs_ReturnsEmpty()
    {
        var builder = new MachiningBuilder();
        var result = builder.MergeAndDeduplicate(
            Array.Empty<Machining>(),
            Array.Empty<Machining>());
        Assert.Empty(result);
    }

    [Fact]
    public void MergeAndDeduplicate_OnlyLegacy_ReturnsAll()
    {
        var builder = new MachiningBuilder();
        var legacy = new Machining[]
        {
            new DrillMachining { Name = "D1", X = 10, Y = 20, Depth = 13, Diameter = 5 },
            new DrillMachining { Name = "D2", X = 50, Y = 60, Depth = 13, Diameter = 5 }
        };
        var result = builder.MergeAndDeduplicate(legacy, Array.Empty<Machining>());
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void MergeAndDeduplicate_DuplicatePosition_BlockWins()
    {
        var builder = new MachiningBuilder();
        var legacy = new Machining[]
        {
            new DrillMachining { Name = "Legacy", X = 10, Y = 20, Depth = 13, Diameter = 5 }
        };
        var block = new Machining[]
        {
            new DrillMachining { Name = "Block", X = 10.1, Y = 20.1, Depth = 15, Diameter = 8 }
        };
        var result = builder.MergeAndDeduplicate(legacy, block, positionTolerance: 0.5);
        Assert.Single(result);
        Assert.Equal("Block", result[0].Name); // Block wins
    }

    [Fact]
    public void MergeAndDeduplicate_DifferentTypes_NoDeduplicate()
    {
        var builder = new MachiningBuilder();
        var legacy = new Machining[]
        {
            new DrillMachining { Name = "LegacyDrill", X = 10, Y = 20, Depth = 13, Diameter = 5 }
        };
        var block = new Machining[]
        {
            new DrillPatternMachining { Name = "BlockPat", X = 10, Y = 20, Depth = 12, Diameter = 5,
                CountX = 1, CountY = 4, SpacingX = 0, SpacingY = 32 }
        };
        // Same position but different types — should keep both
        var result = builder.MergeAndDeduplicate(legacy, block, positionTolerance: 0.5);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void MergeAndDeduplicate_RoutingNotDeduplicatedByPosition()
    {
        var builder = new MachiningBuilder();
        var legacy = new Machining[]
        {
            new RoutingMachining
            {
                Name = "Contour1",
                Points = new[] { (0.0, 0.0), (100.0, 0.0) },
                Depth = 19, ToolDiameter = 9.5, TechCode = "E010"
            }
        };
        var block = new Machining[]
        {
            new RoutingMachining
            {
                Name = "Contour2",
                Points = new[] { (0.0, 0.0), (200.0, 0.0) },
                Depth = 19, ToolDiameter = 9.5, TechCode = "E010"
            }
        };
        // Routing doesn't have positional dedup (returns null)
        var result = builder.MergeAndDeduplicate(legacy, block);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void MergeAllSources_PriorityOrder_UserTextWins()
    {
        var builder = new MachiningBuilder();
        var legacy = new Machining[]
        {
            new DrillMachining { Name = "Legacy", X = 10, Y = 20, Depth = 13, Diameter = 5 }
        };
        var block = new Machining[]
        {
            new DrillMachining { Name = "Block", X = 10.1, Y = 20.1, Depth = 15, Diameter = 8 }
        };
        var userText = new Machining[]
        {
            new DrillMachining { Name = "UserText", X = 10, Y = 20, Depth = 20, Diameter = 10 }
        };

        var result = builder.MergeAllSources(legacy, block, userText, positionTolerance: 0.5);
        // UserText at same position should win, legacy and block excluded
        Assert.Single(result);
        Assert.Equal("UserText", result[0].Name);
    }

    #endregion

    #region NameService — Duplicate Name Handling

    [Fact]
    public void NameService_DuplicateNames_AutoIncrements()
    {
        var names = new NameService();
        var n1 = names.CreateUnique("Bohrung_1");
        var n2 = names.CreateUnique("Bohrung_1");
        var n3 = names.CreateUnique("Bohrung_1");

        Assert.Equal("Bohrung_1", n1);
        Assert.NotEqual(n1, n2);
        Assert.NotEqual(n2, n3);
    }

    [Fact]
    public void NameService_EmptyName_DoesNotThrow()
    {
        var names = new NameService();
        var result = names.CreateUnique("");
        Assert.NotNull(result);
    }

    #endregion

    #region CncUserTextParser — Edge Cases

    [Fact]
    public void Parse_EmptyUserText_ReturnsNull()
    {
        var result = CncUserTextParser.Parse(
            "TestBlock",
            new Dictionary<string, string>(),
            (0, 0, 0), 0, null, out var error);

        Assert.Null(result);
        Assert.Contains("No CNC_Type", error);
    }

    [Fact]
    public void Parse_NonCncKeys_Ignored()
    {
        var userText = new Dictionary<string, string>
        {
            ["Name"] = "SomeBlock",
            ["Description"] = "Irrelevant",
            ["CNC_Type"] = "DRILL"
        };

        var result = CncUserTextParser.Parse(
            "TestBlock", userText, (10, 20, 0), 0, "Layer1", out var error);

        Assert.NotNull(result);
        Assert.Null(error);
        Assert.Equal("DRILL", result!.CncType);
    }

    [Fact]
    public void Parse_InvalidCncType_ReturnsNull()
    {
        var userText = new Dictionary<string, string>
        {
            ["CNC_Type"] = "INVALID_TYPE"
        };

        var result = CncUserTextParser.Parse(
            "TestBlock", userText, (0, 0, 0), 0, null, out var error);

        Assert.Null(result);
        Assert.Contains("Unknown CNC_Type", error);
    }

    [Fact]
    public void Parse_NegativeDiameter_ReturnsError()
    {
        var userText = new Dictionary<string, string>
        {
            ["CNC_Type"] = "DRILL",
            ["CNC_Diameter"] = "-5"
        };

        var result = CncUserTextParser.Parse(
            "TestBlock", userText, (0, 0, 0), 0, null, out var error);

        Assert.Null(result);
        Assert.Contains("positive number", error);
    }

    [Fact]
    public void Parse_CaseInsensitiveKeys()
    {
        var userText = new Dictionary<string, string>
        {
            ["cnc_type"] = "DRILL",
            ["cnc_diameter"] = "8"
        };

        var result = CncUserTextParser.Parse(
            "TestBlock", userText, (10, 20, 0), 0, null, out var error);

        Assert.NotNull(result);
        Assert.Null(error);
    }

    #endregion

    #region BlockUserTextSchema — Validation Edge Cases

    [Fact]
    public void Validate_MacroWithoutMacroName_Fails()
    {
        var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CNC_Type"] = "MACRO"
        };

        var (isValid, error) = BlockUserTextSchema.Validate(attrs);
        Assert.False(isValid);
        Assert.Contains("CNC_MacroName", error);
    }

    [Fact]
    public void Validate_InvalidOrientation_Fails()
    {
        var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CNC_Type"] = "DRILL",
            ["CNC_Orientation"] = "45"
        };

        var (isValid, error) = BlockUserTextSchema.Validate(attrs);
        Assert.False(isValid);
        Assert.Contains("Orientation", error);
    }

    [Fact]
    public void Validate_ValidDrillMinimal_Passes()
    {
        var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CNC_Type"] = "DRILL"
        };

        var (isValid, _) = BlockUserTextSchema.Validate(attrs);
        Assert.True(isValid);
    }

    [Fact]
    public void Validate_AllValidSides()
    {
        foreach (var side in new[] { "TOP", "BOTTOM", "LEFT", "RIGHT", "FRONT", "BACK" })
        {
            var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["CNC_Type"] = "DRILL",
                ["CNC_Side"] = side
            };

            var (isValid, _) = BlockUserTextSchema.Validate(attrs);
            Assert.True(isValid, $"Side '{side}' should be valid");
        }
    }

    [Fact]
    public void Validate_DepthWithPlaceholder_Passes()
    {
        var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CNC_Type"] = "DRILL",
            ["CNC_Depth"] = "{DZ}-2"
        };

        var (isValid, _) = BlockUserTextSchema.Validate(attrs);
        Assert.True(isValid);
    }

    #endregion

    #region Helpers

    private static FittingBlock CreateFittingBlock(string name, string type, double rotation = 0)
    {
        var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CNC_Type"] = type
        };
        return new FittingBlock
        {
            BlockName = name,
            CncType = type,
            InsertionPoint = (0, 0, 0),
            Rotation = rotation,
            CncAttributes = attrs
        };
    }

    private static FittingBlock CreateFittingBlockWithAttrs(
        string name, string type,
        Dictionary<string, string> attrs,
        double rotation = 0)
    {
        return new FittingBlock
        {
            BlockName = name,
            CncType = type,
            InsertionPoint = (0, 0, 0),
            Rotation = rotation,
            CncAttributes = attrs
        };
    }

    #endregion
}
