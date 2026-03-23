using RhinoCNCExporter.Core.Models;
using Xunit;

namespace RhinoCNCExporter.Tests;

public class ExportReportTests
{
    [Fact]
    public void DocumentCapabilities_HasAnyGeometry_WhenAnySignalPresent()
    {
        var capabilities = new DocumentCapabilities
        {
            HasBlocks = true
        };

        Assert.True(capabilities.HasAnyGeometry);
    }

    [Fact]
    public void DocumentCapabilities_HasAnyGeometry_FalseWhenEmpty()
    {
        var capabilities = new DocumentCapabilities();

        Assert.False(capabilities.HasAnyGeometry);
    }

    [Fact]
    public void ExportSummaryReport_SummaryLine_UsesCurrentFormat()
    {
        var report = new ExportSummaryReport
        {
            Mode = ExportMode.MultiPlate3D,
            PlateCount = 5,
            TotalBlocks = 12,
            TotalMachinings = 48,
            ExportedFiles = new[]
            {
                @"C:\temp\Seite_links.xcs",
                @"C:\temp\Seite_rechts.xcs"
            }
        };

        Assert.Equal("5 Platten, 48 Bearbeitungen exportiert", report.SummaryLine);
        Assert.Equal(2, report.ExportedFiles.Count);
    }

    [Fact]
    public void ExportMode_HasCurrentValues()
    {
        Assert.Equal(0, (int)ExportMode.Automatic);
        Assert.Equal(1, (int)ExportMode.LegacyOnly);
        Assert.Equal(2, (int)ExportMode.MultiPlate3D);
    }
}
