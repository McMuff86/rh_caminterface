using RhinoCNCExporter.Core.Models;
using RhinoCNCExporter.Core.Pipeline;
using Xunit;

namespace RhinoCNCExporter.Tests;

/// <summary>
/// Tests for ExportModeDetector — determines export mode from document content.
/// </summary>
public class ExportModeDetectorTests
{
    // === Legacy Mode Detection ===

    [Fact]
    public void Detect_OnlyCncLayers_ReturnsLegacy()
    {
        var layers = new[] { "WK_PIECE", "CUT_E010_Z19", "DRILL_D5_Z13", "POCKET_E015_Z8" };
        var mode = ExportModeDetector.Detect(layers, hasSolidsOrExtrusions: false, hasBlockInserts: false);
        Assert.Equal(ExportMode.Legacy, mode);
    }

    [Fact]
    public void Detect_OnlyWkPiece_ReturnsLegacy()
    {
        var layers = new[] { "WK_PIECE" };
        var mode = ExportModeDetector.Detect(layers, hasSolidsOrExtrusions: false, hasBlockInserts: false);
        Assert.Equal(ExportMode.Legacy, mode);
    }

    [Fact]
    public void Detect_DrillRowAndDrillPatLayers_ReturnsLegacy()
    {
        var layers = new[] { "DRILLROW_D5_Z17_P32", "DRILLPAT_D5_X3_Y4_P32", "WK_PIECE" };
        var mode = ExportModeDetector.Detect(layers, hasSolidsOrExtrusions: false, hasBlockInserts: false);
        Assert.Equal(ExportMode.Legacy, mode);
    }

    [Fact]
    public void Detect_HdrillAndRbnutLayers_ReturnsLegacy()
    {
        var layers = new[] { "HDRILL_D8_Z30_SIDE_L", "RBNUT_CH_X_W6_Z8_P", "WK_PIECE" };
        var mode = ExportModeDetector.Detect(layers, hasSolidsOrExtrusions: false, hasBlockInserts: false);
        Assert.Equal(ExportMode.Legacy, mode);
    }

    // === 3D Mode Detection ===

    [Fact]
    public void Detect_SolidsNoLegacyLayers_ReturnsThreeD()
    {
        var layers = new[] { "Korpus_1", "Seite_links", "Seite_rechts", "Boden" };
        var mode = ExportModeDetector.Detect(layers, hasSolidsOrExtrusions: true, hasBlockInserts: false);
        Assert.Equal(ExportMode.ThreeD, mode);
    }

    [Fact]
    public void Detect_BlocksNoLegacyLayers_ReturnsThreeD()
    {
        var layers = new[] { "Korpus_1", "Seite_links" };
        var mode = ExportModeDetector.Detect(layers, hasSolidsOrExtrusions: false, hasBlockInserts: true);
        Assert.Equal(ExportMode.ThreeD, mode);
    }

    [Fact]
    public void Detect_SolidsAndBlocks_ReturnsThreeD()
    {
        var layers = new[] { "Korpus_1", "Seite_links" };
        var mode = ExportModeDetector.Detect(layers, hasSolidsOrExtrusions: true, hasBlockInserts: true);
        Assert.Equal(ExportMode.ThreeD, mode);
    }

    // === Auto Mode Detection ===

    [Fact]
    public void Detect_SolidsAndLegacyLayers_ReturnsAuto()
    {
        var layers = new[] { "WK_PIECE", "CUT_E010_Z19", "Korpus_1", "Seite_links" };
        var mode = ExportModeDetector.Detect(layers, hasSolidsOrExtrusions: true, hasBlockInserts: false);
        Assert.Equal(ExportMode.Auto, mode);
    }

    [Fact]
    public void Detect_BlocksAndLegacyLayers_ReturnsAuto()
    {
        var layers = new[] { "DRILL_D5_Z13", "Seite_links" };
        var mode = ExportModeDetector.Detect(layers, hasSolidsOrExtrusions: false, hasBlockInserts: true);
        Assert.Equal(ExportMode.Auto, mode);
    }

    [Fact]
    public void Detect_EverythingPresent_ReturnsAuto()
    {
        var layers = new[] { "WK_PIECE", "CUT_E010_Z19", "Korpus_1" };
        var mode = ExportModeDetector.Detect(layers, hasSolidsOrExtrusions: true, hasBlockInserts: true);
        Assert.Equal(ExportMode.Auto, mode);
    }

    // === Edge Cases ===

    [Fact]
    public void Detect_NoContent_ReturnsLegacy()
    {
        var layers = Array.Empty<string>();
        var mode = ExportModeDetector.Detect(layers, hasSolidsOrExtrusions: false, hasBlockInserts: false);
        Assert.Equal(ExportMode.Legacy, mode);
    }

    [Fact]
    public void Detect_RandomLayerNames_NoSolids_ReturnsLegacy()
    {
        var layers = new[] { "Layer1", "Gruppe_A", "Notizen" };
        var mode = ExportModeDetector.Detect(layers, hasSolidsOrExtrusions: false, hasBlockInserts: false);
        Assert.Equal(ExportMode.Legacy, mode);
    }

    // === IsLegacyCncLayer ===

    [Theory]
    [InlineData("CUT_E010_Z19", true)]
    [InlineData("POCKET_E015_Z8", true)]
    [InlineData("DRILL_D5_Z13", true)]
    [InlineData("DRILLROW_D5_Z17_P32", true)]
    [InlineData("DRILLPAT_D5_X3_Y4_P32", true)]
    [InlineData("HDRILL_D8_Z30_SIDE_L", true)]
    [InlineData("RBNUT_CH_X_W6_Z8_P", true)]
    [InlineData("WK_PIECE", true)]
    [InlineData("Korpus_1", false)]
    [InlineData("Seite_links", false)]
    [InlineData("Layer1", false)]
    [InlineData("CUT_OFF", true)] // starts with CUT_ — matches pattern
    [InlineData("", false)]
    public void IsLegacyCncLayer_ClassifiesCorrectly(string layerName, bool expected)
    {
        Assert.Equal(expected, ExportModeDetector.IsLegacyCncLayer(layerName));
    }
}
