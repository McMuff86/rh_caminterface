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
}
