using RhinoCNCExporter.Core.Models;
using Xunit;

namespace RhinoCNCExporter.Tests;

/// <summary>
/// Tests for ExportReport and PlateInfo models.
/// </summary>
public class ExportReportTests
{
    // === ExportReport ===

    [Fact]
    public void GetSummary_NoData_ShowsZeros()
    {
        var report = new ExportReport();
        var summary = report.GetSummary();
        Assert.Contains("0 Platte(n) exportiert", summary);
        Assert.Contains("0 Bearbeitung(en)", summary);
        Assert.Contains("0 Warnung(en)", summary);
    }

    [Fact]
    public void GetSummary_WithData_ShowsCounts()
    {
        var report = new ExportReport
        {
            PlatesExported = 7,
            TotalMachinings = 48,
            Warnings = { "Test warning" }
        };
        var summary = report.GetSummary();
        Assert.Contains("7 Platte(n) exportiert", summary);
        Assert.Contains("48 Bearbeitung(en)", summary);
        Assert.Contains("1 Warnung(en)", summary);
    }

    [Fact]
    public void ExportReport_DefaultsAreCorrect()
    {
        var report = new ExportReport();
        Assert.False(report.Success);
        Assert.Null(report.Error);
        Assert.Empty(report.ExportedFiles);
        Assert.Empty(report.Warnings);
        Assert.Empty(report.PlateDetails);
        Assert.Equal(0, report.TotalPlatesDetected);
        Assert.Equal(0, report.PlatesExported);
        Assert.Equal(0, report.TotalMachinings);
    }

    [Fact]
    public void PlateExportDetail_HasRequiredProperties()
    {
        var detail = new PlateExportDetail
        {
            PlateName = "Seite_links",
            FilePath = "/tmp/Seite_links.xcs",
            MachiningCount = 12,
            LengthX = 813,
            WidthY = 2100,
            Thickness = 19
        };
        Assert.Equal("Seite_links", detail.PlateName);
        Assert.Equal(12, detail.MachiningCount);
        Assert.Equal(813, detail.LengthX);
    }

    // === PlateInfo ===

    [Fact]
    public void PlateInfo_DisplayText_FormattedCorrectly()
    {
        var info = new PlateInfo
        {
            Name = "Seite_links",
            LengthX = 813,
            WidthY = 2100,
            Thickness = 19,
            MachiningCount = 12
        };
        Assert.Equal("Seite_links (813 × 2100 × 19mm) — 12 Bearbeitung(en)", info.DisplayText);
    }

    [Fact]
    public void PlateInfo_DefaultSelectedTrue()
    {
        var info = new PlateInfo
        {
            Name = "Test",
            LengthX = 100,
            WidthY = 200,
            Thickness = 19
        };
        Assert.True(info.IsSelected);
    }

    [Fact]
    public void PlateInfo_GroupNameOptional()
    {
        var info = new PlateInfo
        {
            Name = "Boden",
            LengthX = 760,
            WidthY = 380,
            Thickness = 19,
            GroupName = "Korpus_1"
        };
        Assert.Equal("Korpus_1", info.GroupName);
    }

    // === ExportMode Enum ===

    [Fact]
    public void ExportMode_HasAllValues()
    {
        Assert.Equal(0, (int)ExportMode.Auto);
        Assert.Equal(1, (int)ExportMode.Legacy);
        Assert.Equal(2, (int)ExportMode.ThreeD);
    }
}
