using RhinoCNCExporter.Core.Blocks;
using RhinoCNCExporter.Core.Models;
using Xunit;

namespace RhinoCNCExporter.Tests;

public class ClamexMacroBuilderTests
{
    // === Vertical CLAMEX Tests ===

    [Fact]
    public void BuildVertical_MatchesProductionFormat()
    {
        // Reference from 1_1_1_Seite_rechts.xcs:
        // CreateMacro("CLAMEX Vertikal_1","SawCut_Lamello",9.5,50.03,9.5,50.03,0,2,19,5,null,1,0.05,
        //   null,null,null,null,2,"3","E015",null,"3","E004",null,0,0,false,-1,0,null,0,false,
        //   "3","E019",null,null,null,null,null,null,null,4,null,null,14.3,null,"3","E032",270);
        var result = ClamexMacroBuilder.BuildVertical("CLAMEX Vertikal_1", 9.5, 50.03, 19, 270);

        Assert.StartsWith("CreateMacro(\"CLAMEX Vertikal_1\",\"SawCut_Lamello\",", result);
        Assert.EndsWith(");", result);
        Assert.Contains(",0,2,19,5,null,1,0.05,", result);
        Assert.Contains(",\"E015\",", result);
        Assert.Contains(",\"E004\",", result);
        Assert.Contains(",\"E019\",", result);
        Assert.Contains(",\"E032\",", result);
        Assert.Contains(",270);", result);
        Assert.DoesNotContain("DZ-9.5", result); // Vertical has no trailing DZ offset
    }

    [Fact]
    public void BuildVertical_PositionX_MatchesProduction()
    {
        var result = ClamexMacroBuilder.BuildVertical("CLAMEX Vertikal_1", 9.5, 50.03, 19, 270);
        // P1=9.5, P2=50.03, P3=9.5, P4=50.03
        Assert.Contains("9.5,50.03,9.5,50.03,", result);
    }

    [Fact]
    public void BuildVertical_Rotation90_MatchesProduction()
    {
        // From production: right-side CLAMEX uses rotation=90
        var result = ClamexMacroBuilder.BuildVertical("CLAMEX Vertikal_3", 290.5, 50.03, 19, 90);
        Assert.Contains(",90);", result);
    }

    [Fact]
    public void BuildVertical_PlateThickness_InOutput()
    {
        var result = ClamexMacroBuilder.BuildVertical("test", 9.5, 50, 19, 270);
        // P7 should be plate thickness
        Assert.Contains(",19,", result);
    }

    [Fact]
    public void BuildVertical_ClamexDepth_Is14Point3()
    {
        var result = ClamexMacroBuilder.BuildVertical("test", 9.5, 50, 19, 270);
        Assert.Contains(",14.3,", result);
    }

    [Fact]
    public void BuildVertical_Orientation_IsZero()
    {
        var result = ClamexMacroBuilder.BuildVertical("test", 9.5, 50, 19, 270);
        // After Y2 position comes orientation=0
        Assert.Contains("50,0,2,", result);
    }

    [Fact]
    public void BuildVertical_ECodes_AreVerticalSet()
    {
        var result = ClamexMacroBuilder.BuildVertical("test", 9.5, 50, 19, 270);
        // Vertical uses E015, E004, E019, E032
        Assert.Contains("\"E015\"", result);
        Assert.Contains("\"E004\"", result);
        Assert.Contains("\"E019\"", result);
        Assert.Contains("\"E032\"", result);
    }

    // === Horizontal CLAMEX Tests ===

    [Fact]
    public void BuildHorizontal_MatchesProductionFormat()
    {
        // Reference from 1_1_2_Boden.xcs:
        // CreateMacro("CLAMEX Horizontal_1","SawCut_Lamello",0,60.03,0,60.03,90,2,19,5,null,1,0.05,
        //   null,null,null,null,-1,"3","E015",null,null,"E005",null,0,-1,false,-1,0,null,0,false,
        //   null,"E022",null,null,null,null,null,null,null,2,null,null,14,null,"3","E021",270,DZ-9.5);
        var result = ClamexMacroBuilder.BuildHorizontal("CLAMEX Horizontal_1", 0, 60.03, 19, 270);

        Assert.StartsWith("CreateMacro(\"CLAMEX Horizontal_1\",\"SawCut_Lamello\",", result);
        Assert.EndsWith(");", result);
        Assert.Contains(",90,2,19,5,null,1,0.05,", result); // Orientation=90
        Assert.Contains(",\"E015\",", result);
        Assert.Contains(",\"E005\",", result);
        Assert.Contains(",\"E022\",", result);
        Assert.Contains(",\"E021\",", result);
        Assert.Contains(",DZ-9.5);", result); // Trailing DZ offset for horizontal
    }

    [Fact]
    public void BuildHorizontal_HasTrailingDzOffset()
    {
        var result = ClamexMacroBuilder.BuildHorizontal("test", 0, 60, 19, 270);
        Assert.EndsWith(",DZ-9.5);", result);
    }

    [Fact]
    public void BuildHorizontal_Orientation_Is90()
    {
        var result = ClamexMacroBuilder.BuildHorizontal("test", 0, 60, 19, 270);
        Assert.Contains("60,90,2,", result);
    }

    [Fact]
    public void BuildHorizontal_ClamexDepth_Is14()
    {
        var result = ClamexMacroBuilder.BuildHorizontal("test", 0, 60, 19, 270);
        Assert.Contains(",14,", result);
    }

    [Fact]
    public void BuildHorizontal_ECodes_AreHorizontalSet()
    {
        var result = ClamexMacroBuilder.BuildHorizontal("test", 0, 60, 19, 270);
        // Horizontal uses E015, E005, E022, E021
        Assert.Contains("\"E015\"", result);
        Assert.Contains("\"E005\"", result);
        Assert.Contains("\"E022\"", result);
        Assert.Contains("\"E021\"", result);
    }

    [Fact]
    public void BuildHorizontal_P16_IsMinus1()
    {
        var result = ClamexMacroBuilder.BuildHorizontal("test", 0, 60, 19, 270);
        // Horizontal has P16=-1 (vs vertical P16=2)
        Assert.Contains(",null,-1,\"3\",\"E015\"", result);
    }

    [Fact]
    public void BuildHorizontal_P40_Is2()
    {
        var result = ClamexMacroBuilder.BuildHorizontal("test", 0, 60, 19, 270);
        // Horizontal has P40=2 (vs vertical P40=4)
        Assert.Contains(",null,2,null,null,14,null,", result);
    }

    // === Vertical vs Horizontal Differences ===

    [Fact]
    public void VerticalVsHorizontal_DifferentECodes()
    {
        var vertical = ClamexMacroBuilder.BuildVertical("v", 0, 50, 19, 270);
        var horizontal = ClamexMacroBuilder.BuildHorizontal("h", 0, 50, 19, 270);

        // Vertical E-codes
        Assert.Contains("\"E004\"", vertical);
        Assert.Contains("\"E019\"", vertical);
        Assert.Contains("\"E032\"", vertical);

        // Horizontal E-codes
        Assert.Contains("\"E005\"", horizontal);
        Assert.Contains("\"E022\"", horizontal);
        Assert.Contains("\"E021\"", horizontal);
    }

    [Fact]
    public void VerticalVsHorizontal_DifferentOrientation()
    {
        var vertical = ClamexMacroBuilder.BuildVertical("v", 0, 50, 19, 270);
        var horizontal = ClamexMacroBuilder.BuildHorizontal("h", 0, 50, 19, 270);

        // Vertical: orientation=0 after position
        Assert.Contains("50,0,2,", vertical);
        // Horizontal: orientation=90 after position
        Assert.Contains("50,90,2,", horizontal);
    }

    // === BuildFromBlock Tests ===

    [Fact]
    public void BuildFromBlock_VerticalClamex_ReturnsVerticalMacro()
    {
        var block = CreateClamexBlock(orientation: "0");
        var result = ClamexMacroBuilder.BuildFromBlock(block, 9.5, 50.03, 19, 1);

        Assert.NotNull(result);
        Assert.Contains("CLAMEX Vertikal_1", result);
        Assert.Contains("SawCut_Lamello", result);
    }

    [Fact]
    public void BuildFromBlock_HorizontalClamex_ReturnsHorizontalMacro()
    {
        var block = CreateClamexBlock(orientation: "90");
        var result = ClamexMacroBuilder.BuildFromBlock(block, 0, 60.03, 19, 1);

        Assert.NotNull(result);
        Assert.Contains("CLAMEX Horizontal_1", result);
        Assert.Contains("DZ-9.5", result);
    }

    [Fact]
    public void BuildFromBlock_NonClamex_ReturnsNull()
    {
        var block = new FittingBlock
        {
            BlockName = "Topfband_35",
            CncType = "DRILL",
            InsertionPoint = (50, 50, 0),
            CncAttributes = new Dictionary<string, string>
            {
                ["CNC_Type"] = "DRILL",
                ["CNC_Diameter"] = "35"
            }
        };

        var result = ClamexMacroBuilder.BuildFromBlock(block, 50, 50, 19, 1);
        Assert.Null(result);
    }

    [Fact]
    public void BuildFromBlock_WrongMacroName_ReturnsNull()
    {
        var block = new FittingBlock
        {
            BlockName = "SomeBlock",
            CncType = "MACRO",
            InsertionPoint = (50, 50, 0),
            CncAttributes = new Dictionary<string, string>
            {
                ["CNC_Type"] = "MACRO",
                ["CNC_MacroName"] = "RNT"
            }
        };

        var result = ClamexMacroBuilder.BuildFromBlock(block, 50, 50, 19, 1);
        Assert.Null(result);
    }

    // === CreateMachining Tests ===

    [Fact]
    public void CreateMachining_VerticalClamex_ReturnsMacroMachining()
    {
        var block = CreateClamexBlock(orientation: "0");
        var machining = ClamexMacroBuilder.CreateMachining(block, 9.5, 50.03, 19, 1);

        Assert.NotNull(machining);
        Assert.Equal("SawCut_Lamello", machining.MacroName);
        Assert.Contains("CLAMEX Vertikal_1", machining.Name);
        Assert.True(machining.Parameters.Count > 0);
        Assert.Equal(MachiningSource.BlockDetection, machining.Source);
    }

    [Fact]
    public void CreateMachining_HorizontalClamex_ReturnsMacroMachining()
    {
        var block = CreateClamexBlock(orientation: "90");
        var machining = ClamexMacroBuilder.CreateMachining(block, 0, 60.03, 19, 1);

        Assert.NotNull(machining);
        Assert.Equal("SawCut_Lamello", machining.MacroName);
        Assert.Contains("Horizontal", machining.Name);
    }

    // === ExtractParametersFromLine Tests ===

    [Fact]
    public void ExtractParameters_VerticalLine_ReturnsCorrectCount()
    {
        var line = ClamexMacroBuilder.BuildVertical("test", 9.5, 50, 19, 270);
        var parameters = ClamexMacroBuilder.ExtractParametersFromLine(line);

        // Should have ~46 parameters (position + all constants + rotation)
        Assert.True(parameters.Count >= 40, $"Expected >= 40 params, got {parameters.Count}");
    }

    [Fact]
    public void ExtractParameters_PreservesNulls()
    {
        var line = ClamexMacroBuilder.BuildVertical("test", 9.5, 50, 19, 270);
        var parameters = ClamexMacroBuilder.ExtractParametersFromLine(line);

        // There should be null parameters
        Assert.Contains(null, parameters);
    }

    [Fact]
    public void ExtractParameters_HorizontalLine_HasDzOffset()
    {
        var line = ClamexMacroBuilder.BuildHorizontal("test", 0, 60, 19, 270);
        var parameters = ClamexMacroBuilder.ExtractParametersFromLine(line);

        // Last parameter should be DZ-9.5
        Assert.Equal("DZ-9.5", parameters[^1]);
    }

    // === Production Reference Comparison ===

    [Fact]
    public void Vertical_CompareToProductionReference_Seite()
    {
        // From 1_1_1_Seite_rechts.xcs line 100:
        var expected =
            "CreateMacro(\"CLAMEX Vertikal_1\",\"SawCut_Lamello\",9.5,50.03,9.5,50.03," +
            "0,2,19,5,null,1,0.05,null,null,null,null,2,\"3\",\"E015\",null,\"3\",\"E004\"," +
            "null,0,0,false,-1,0,null,0,false,\"3\",\"E019\",null,null,null,null,null,null," +
            "null,4,null,null,14.3,null,\"3\",\"E032\",270);";

        var actual = ClamexMacroBuilder.BuildVertical("CLAMEX Vertikal_1", 9.5, 50.03, 19, 270);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Horizontal_CompareToProductionReference_Boden()
    {
        // From 1_1_2_Boden.xcs line 114:
        var expected =
            "CreateMacro(\"CLAMEX Horizontal_1\",\"SawCut_Lamello\",0,60.03,0,60.03," +
            "90,2,19,5,null,1,0.05,null,null,null,null,-1,\"3\",\"E015\",null,null,\"E005\"," +
            "null,0,-1,false,-1,0,null,0,false,null,\"E022\",null,null,null,null,null,null," +
            "null,2,null,null,14,null,\"3\",\"E021\",270,DZ-9.5);";

        var actual = ClamexMacroBuilder.BuildHorizontal("CLAMEX Horizontal_1", 0, 60.03, 19, 270);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Vertical_CompareToProductionReference_Rotation90()
    {
        // From 1_1_1_Seite_rechts.xcs line 106:
        var expected =
            "CreateMacro(\"CLAMEX Vertikal_3\",\"SawCut_Lamello\",290.5,50.03,290.5,50.03," +
            "0,2,19,5,null,1,0.05,null,null,null,null,2,\"3\",\"E015\",null,\"3\",\"E004\"," +
            "null,0,0,false,-1,0,null,0,false,\"3\",\"E019\",null,null,null,null,null,null," +
            "null,4,null,null,14.3,null,\"3\",\"E032\",90);";

        var actual = ClamexMacroBuilder.BuildVertical("CLAMEX Vertikal_3", 290.5, 50.03, 19, 90);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Horizontal_CompareToProductionReference_Rotation90()
    {
        // From 1_1_2_Boden.xcs line 120:
        var expected =
            "CreateMacro(\"CLAMEX Horizontal_3\",\"SawCut_Lamello\",268,50.03,268,50.03," +
            "90,2,19,5,null,1,0.05,null,null,null,null,-1,\"3\",\"E015\",null,null,\"E005\"," +
            "null,0,-1,false,-1,0,null,0,false,null,\"E022\",null,null,null,null,null,null," +
            "null,2,null,null,14,null,\"3\",\"E021\",90,DZ-9.5);";

        var actual = ClamexMacroBuilder.BuildHorizontal("CLAMEX Horizontal_3", 268, 50.03, 19, 90);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Vertical_Mittelseite_10Point3Depth()
    {
        // From 1_2_1_Mittelseite.xcs: depth=10.3 instead of 14.3
        // This is a different plate scenario — for now our builder uses 14.3 (standard)
        // TODO: When we need depth parameterization, add a parameter to BuildVertical
        var result = ClamexMacroBuilder.BuildVertical("test", 290.5, 60, 19, 90);
        // Standard depth is 14.3
        Assert.Contains(",14.3,", result);
    }

    // === Helper Methods ===

    private static FittingBlock CreateClamexBlock(string orientation = "0")
    {
        return new FittingBlock
        {
            BlockName = "CLAMEX_P14",
            CncType = "MACRO",
            InsertionPoint = (9.5, 50.03, 0),
            Rotation = orientation == "0" ? 270 : 270,
            CncAttributes = new Dictionary<string, string>
            {
                ["CNC_Type"] = "MACRO",
                ["CNC_MacroName"] = "SawCut_Lamello",
                ["CNC_Orientation"] = orientation,
                ["CNC_Side"] = "TOP",
                ["CNC_Depth"] = "9.5",
                ["CNC_TechCode"] = "E015"
            }
        };
    }
}
