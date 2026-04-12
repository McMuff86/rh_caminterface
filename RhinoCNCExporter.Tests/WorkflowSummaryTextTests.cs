using RhinoCNCExporter.Core.Models;
using Xunit;

namespace RhinoCNCExporter.Tests;

public class WorkflowSummaryTextTests
{
    [Fact]
    public void Format_IncludesOpenAndReadyGroups_WhenAssignmentStatusIsKnown()
    {
        var text = WorkflowSummaryText.Format(5, 2, 1, 2, 4, true);

        Assert.Equal("Workflow: 2 Gruppen offen · 4 Gruppen bereit · Block-Ops=5 · Face-Features=2 · Manuell=1", text);
    }

    [Fact]
    public void Format_UsesAllReadyText_WhenNoOpenGroupsRemain()
    {
        var text = WorkflowSummaryText.Format(3, 1, 0, 0, 3, true);

        Assert.Equal("Workflow: Alle 3 Gruppen bereit · Block-Ops=3 · Face-Features=1 · Manuell=0", text);
    }

    [Fact]
    public void Format_UsesMachineSelectionHint_WhenAssignmentStatusIsUnknown()
    {
        var text = WorkflowSummaryText.Format(3, 1, 0, 2, 4, false);

        Assert.Equal("Workflow: Block-Ops=3 · Face-Features=1 · Manuell=0 · Werkzeugstatus nach Maschinenwahl", text);
    }
}
