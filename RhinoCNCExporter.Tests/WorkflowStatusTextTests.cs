using Xunit;
using RhinoCNCExporter.Core.Models;

namespace RhinoCNCExporter.Tests;

public class WorkflowStatusTextTests
{
    [Fact]
    public void FormatOpenVsTotal_FormatsExpectedText()
    {
        var text = WorkflowStatusText.FormatOpenVsTotal(2, 6);

        Assert.Equal("2 offen / 6 gesamt", text);
    }

    [Fact]
    public void FormatOpenVsTotal_ClampsOpenCountToTotal()
    {
        var text = WorkflowStatusText.FormatOpenVsTotal(9, 6);

        Assert.Equal("6 offen / 6 gesamt", text);
    }

    [Fact]
    public void FormatOpenVsTotal_NormalizesNegativeValues()
    {
        var text = WorkflowStatusText.FormatOpenVsTotal(-3, -1);

        Assert.Equal("0 offen / 0 gesamt", text);
    }
}
