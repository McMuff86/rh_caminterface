using RhinoCNCExporter.Core.LayerParser;
using Xunit;

namespace RhinoCNCExporter.Tests;

public class LayerRegexTests
{
    [Theory]
    [InlineData("CUT_E010", "E010", 19.0, null, 9.5)]
    [InlineData("CUT_E010_Z20", "E010", 20.0, null, 9.5)]
    [InlineData("CUT_E010_Z20_S5", "E010", 20.0, 5.0, 9.5)]
    [InlineData("CUT_E010_Z20_S5_D12", "E010", 20.0, 5.0, 12.0)]
    [InlineData("CUT_E15_D8", "E015", 19.0, null, 8.0)]
    public void TryParseCut_Valid(string layer, string tech, double depth, double? sd, double dia)
    {
        Assert.True(LayerRegex.TryParseCut(layer, out var spec));
        Assert.Equal(tech, spec!.Tech);
        Assert.Equal(depth, spec.Depth);
        Assert.Equal(sd, spec.Stepdown);
        Assert.Equal(dia, spec.ToolDiameter);
    }

    [Theory]
    [InlineData("DRILL_D4.5", 4.5, 19.0, 'P')]
    [InlineData("DRILL_D5_Z17", 5.0, 17.0, 'P')]
    [InlineData("DRILL_D4.5_Z17_CL", 4.5, 17.0, 'L')]
    public void TryParseDrill_Valid(string layer, double dia, double depth, char side)
    {
        Assert.True(LayerRegex.TryParseDrill(layer, out var spec));
        Assert.Equal(dia, spec!.Diameter);
        Assert.Equal(depth, spec.Depth);
        Assert.Equal(side, spec.Side);
    }

    [Theory]
    [InlineData("DRILLROW_D5_Z17_P32", 5.0, 17.0, 32.0, null)]
    [InlineData("DRILLROW_D5_Z17_P32_N10", 5.0, 17.0, 32.0, 10)]
    [InlineData("DRILLROW_D5_P32", 5.0, 19.0, 32.0, null)]
    public void TryParseRow_Valid(string layer, double dia, double depth, double pitch, int? count)
    {
        Assert.True(LayerRegex.TryParseRow(layer, out var spec));
        Assert.Equal(dia, spec!.Diameter);
        Assert.Equal(depth, spec.Depth);
        Assert.Equal(pitch, spec.Pitch);
        Assert.Equal(count, spec.Count);
    }

    [Theory]
    [InlineData("POCKET_E010", "E010", 19.0, null, 9.5, null)]
    [InlineData("POCKET_E010_Z12_S3_D8_O5", "E010", 12.0, 3.0, 8.0, 5.0)]
    public void TryParsePocket_Valid(string layer, string tech, double depth, double? sd, double dia, double? off)
    {
        Assert.True(LayerRegex.TryParsePocket(layer, out var spec));
        Assert.Equal(tech, spec!.Tech);
        Assert.Equal(depth, spec.Depth);
        Assert.Equal(sd, spec.Stepdown);
        Assert.Equal(dia, spec.ToolDiameter);
        Assert.Equal(off, spec.OffsetStep);
    }

    [Theory]
    [InlineData("RBNUT_CH_X_W6_M", Axis.X, 6.0, 19.0, null, null, Place.Center)]
    [InlineData("RBNUT_CH_Y_W6_Z8_S2_E015_P", Axis.Y, 6.0, 8.0, 2.0, "E015", Place.Positive)]
    [InlineData("RBNUT_CH_X_W6", Axis.X, 6.0, 19.0, null, null, Place.Center)]  // No place → default M
    public void TryParseGrooveChannel_Valid(string layer, Axis axis, double w, double depth,
        double? sd, string? tech, Place place)
    {
        Assert.True(LayerRegex.TryParseGrooveChannel(layer, out var spec));
        Assert.Equal(axis, spec!.Axis);
        Assert.Equal(w, spec.Width);
        Assert.Equal(depth, spec.Depth);
        Assert.Equal(sd, spec.Stepdown);
        Assert.Equal(tech, spec.Tech);
        Assert.Equal(place, spec.Place);
    }

    [Theory]
    [InlineData("RBNUT_RNT_X_W6_Z8_C066_M", Axis.X, 6.0, 8.0, "066", Place.Center)]
    [InlineData("RBNUT_RNT_Y_W6_C066_P", Axis.Y, 6.0, 19.0, "066", Place.Positive)]
    [InlineData("RBNUT_RNT_X_W6_C066", Axis.X, 6.0, 19.0, "066", Place.Center)]  // No place → default M
    public void TryParseGrooveRnt_Valid(string layer, Axis axis, double w, double depth,
        string code, Place place)
    {
        Assert.True(LayerRegex.TryParseGrooveRnt(layer, out var spec));
        Assert.Equal(axis, spec!.Axis);
        Assert.Equal(w, spec.Width);
        Assert.Equal(depth, spec.Depth);
        Assert.Equal(code, spec.Code);
        Assert.Equal(place, spec.Place);
    }

    [Theory]
    [InlineData("WK_PIECE")]
    [InlineData("INVALID")]
    [InlineData("")]
    public void TryParseCut_Invalid(string layer)
    {
        Assert.False(LayerRegex.TryParseCut(layer, out _));
    }

    #region DrillPattern

    [Theory]
    [InlineData("DRILLPAT_D15_Z14_X1_Y4_SX0_SY64", 15.0, 14.0, 'P', 1, 4, 0, 64)]
    [InlineData("DRILLPAT_D5_X3_Y2_SX32_SY64", 5.0, 19.0, 'P', 3, 2, 32, 64)]
    [InlineData("DRILLPAT_D8_Z13_X2_Y3_SX32.5_SY64_CP", 8.0, 13.0, 'P', 2, 3, 32.5, 64)]
    [InlineData("DRILLPAT_D5_Z13_X1_Y4_SX0_SY64_CL", 5.0, 13.0, 'L', 1, 4, 0, 64)]
    public void TryParseDrillPattern_Valid(string layer, double dia, double depth, char side,
        int xn, int yn, double sx, double sy)
    {
        Assert.True(LayerRegex.TryParseDrillPattern(layer, out var spec));
        Assert.Equal(dia, spec!.Diameter);
        Assert.Equal(depth, spec.Depth);
        Assert.Equal(side, spec.Side);
        Assert.Equal(xn, spec.XCount);
        Assert.Equal(yn, spec.YCount);
        Assert.Equal(sx, spec.XSpacing);
        Assert.Equal(sy, spec.YSpacing);
    }

    [Theory]
    [InlineData("DRILL_D5")]
    [InlineData("DRILLPAT_D5")]
    [InlineData("DRILLROW_D5_P32")]
    public void TryParseDrillPattern_Invalid(string layer)
    {
        Assert.False(LayerRegex.TryParseDrillPattern(layer, out _));
    }

    #endregion

    #region HorizontalDrill

    [Theory]
    [InlineData("HDRILL_D8_Z30_SL", 8.0, 30.0, 'L')]
    [InlineData("HDRILL_D5_Z25_SR", 5.0, 25.0, 'R')]
    [InlineData("HDRILL_D8_SV", 8.0, 30.0, 'V')]  // Default depth 30
    [InlineData("HDRILL_D10_Z40_SH", 10.0, 40.0, 'H')]
    public void TryParseHorizontalDrill_Valid(string layer, double dia, double depth, char side)
    {
        Assert.True(LayerRegex.TryParseHorizontalDrill(layer, out var spec));
        Assert.Equal(dia, spec!.Diameter);
        Assert.Equal(depth, spec.Depth);
        Assert.Equal(side, spec.DrillSide);
    }

    [Theory]
    [InlineData("DRILL_D8")]
    [InlineData("HDRILL_D8")]
    [InlineData("HDRILL_D8_ST")]  // T is not valid side
    public void TryParseHorizontalDrill_Invalid(string layer)
    {
        Assert.False(LayerRegex.TryParseHorizontalDrill(layer, out _));
    }

    #endregion
}
