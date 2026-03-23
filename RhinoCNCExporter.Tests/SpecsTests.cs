using RhinoCNCExporter.Core.LayerParser;
using Xunit;

namespace RhinoCNCExporter.Tests;

/// <summary>
/// Tests for Specs (DTOs) and Defaults.
/// Validates default values match the Python reference.
/// </summary>
public class SpecsTests
{
    // --- Default Values (must match Python reference exactly) ---

    [Fact]
    public void DefaultDz_Is_19()
    {
        Assert.Equal(19.0, Defaults.DefaultDz);
    }

    [Fact]
    public void DefaultToolDiameter_Is_9_5()
    {
        Assert.Equal(9.5, Defaults.DefaultToolDiameter);
    }

    [Fact]
    public void DefaultPocketStepover_Is_0_7()
    {
        Assert.Equal(0.7, Defaults.DefaultPocketStepover);
    }

    [Fact]
    public void GrooveOvertravel_Is_5()
    {
        Assert.Equal(5.0, Defaults.GrooveOvertravel);
    }

    [Fact]
    public void PolyTolerance_Is_0_05()
    {
        Assert.Equal(0.05, Defaults.PolyTolerance);
    }

    [Fact]
    public void SetupOffsets_MatchPython()
    {
        Assert.Equal(2.5, Defaults.DefaultSetupOffsetX);
        Assert.Equal(2.5, Defaults.DefaultSetupOffsetY);
        Assert.Equal(0.0, Defaults.DefaultSetupOffsetZ);
        Assert.Equal(0.0, Defaults.DefaultSetupOffsetRot);
    }

    [Fact]
    public void UseCornerRounding_IsFalse()
    {
        Assert.False(Defaults.UseCornerRounding);
    }

    [Fact]
    public void UseRntMacro_IsTrue()
    {
        Assert.True(Defaults.UseRntMacro);
    }

    [Fact]
    public void DefaultGrooveTech_Is_E010()
    {
        Assert.Equal("E010", Defaults.DefaultGrooveTech);
    }

    // --- CutSpec ---

    [Fact]
    public void CutSpec_Defaults_Match_Parser()
    {
        Assert.True(LayerRegex.TryParseCut("CUT_E010", out var spec));
        Assert.Equal("E010", spec!.Tech);
        Assert.Equal(19.0, spec.Depth);
        Assert.Null(spec.Stepdown);
        Assert.Equal(9.5, spec.ToolDiameter);
    }

    [Fact]
    public void CutSpec_FullParse()
    {
        Assert.True(LayerRegex.TryParseCut("CUT_E015_Z12_S3_D8", out var spec));
        Assert.Equal("E015", spec!.Tech);
        Assert.Equal(12.0, spec.Depth);
        Assert.Equal(3.0, spec.Stepdown);
        Assert.Equal(8.0, spec.ToolDiameter);
    }

    // --- PocketSpec ---

    [Fact]
    public void PocketSpec_Defaults()
    {
        Assert.True(LayerRegex.TryParsePocket("POCKET_E010", out var spec));
        Assert.Equal("E010", spec!.Tech);
        Assert.Equal(19.0, spec.Depth);
        Assert.Null(spec.Stepdown);
        Assert.Equal(9.5, spec.ToolDiameter);
        Assert.Null(spec.OffsetStep);
    }

    [Fact]
    public void PocketSpec_ComputedOffset()
    {
        // When OffsetStep is null, ExportService uses ToolDiameter * DefaultPocketStepover
        Assert.True(LayerRegex.TryParsePocket("POCKET_E010", out var spec));
        double expectedOffset = spec!.ToolDiameter * Defaults.DefaultPocketStepover;
        Assert.Equal(9.5 * 0.7, expectedOffset, 3);
    }

    // --- DrillSpec ---

    [Fact]
    public void DrillSpec_DefaultSide_Is_P()
    {
        Assert.True(LayerRegex.TryParseDrill("DRILL_D5", out var spec));
        Assert.Equal('P', spec!.Side);
        Assert.Equal(19.0, spec.Depth);
    }

    [Fact]
    public void DrillSpec_LeftSide()
    {
        Assert.True(LayerRegex.TryParseDrill("DRILL_D5_Z17_CL", out var spec));
        Assert.Equal('L', spec!.Side);
    }

    // --- DrillRowSpec ---

    [Fact]
    public void DrillRowSpec_Defaults()
    {
        Assert.True(LayerRegex.TryParseRow("DRILLROW_D5_Z17_P32", out var spec));
        Assert.Equal(5.0, spec!.Diameter);
        Assert.Equal(17.0, spec.Depth);
        Assert.Equal(32.0, spec.Pitch);
        Assert.Null(spec.Count);
    }

    // --- GrooveChannelSpec ---

    [Fact]
    public void GrooveChannelSpec_DefaultPlace_IsCenter()
    {
        Assert.True(LayerRegex.TryParseGrooveChannel("RBNUT_CH_X_W6", out var spec));
        Assert.Equal(Place.Center, spec!.Place);
    }

    [Fact]
    public void GrooveChannelSpec_DefaultTech_IsNull()
    {
        Assert.True(LayerRegex.TryParseGrooveChannel("RBNUT_CH_X_W6", out var spec));
        Assert.Null(spec!.Tech);
        // ExportService should use Defaults.DefaultGrooveTech when null
    }

    // --- GrooveRntSpec ---

    [Fact]
    public void GrooveRntSpec_Code_Parsed()
    {
        Assert.True(LayerRegex.TryParseGrooveRnt("RBNUT_RNT_X_W6_Z8_C066_M", out var spec));
        Assert.Equal("066", spec!.Code);
        Assert.Equal(8.0, spec.Depth);
        Assert.Equal(Place.Center, spec.Place);
    }

    // --- Enum values ---

    [Fact]
    public void Axis_HasExpectedValues()
    {
        Assert.Equal(0, (int)Axis.X);
        Assert.Equal(1, (int)Axis.Y);
    }

    [Fact]
    public void Place_HasExpectedValues()
    {
        Assert.Equal(0, (int)Place.Center);
        Assert.Equal(1, (int)Place.Positive);
    }
}
