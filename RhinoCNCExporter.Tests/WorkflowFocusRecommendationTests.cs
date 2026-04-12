using RhinoCNCExporter.Core.Models;
using Xunit;

namespace RhinoCNCExporter.Tests;

public class WorkflowFocusRecommendationTests
{
    [Fact]
    public void SelectNext_ReturnsNull_WhenNoOpenCandidatesAreKnown()
    {
        var candidates = new[]
        {
            new WorkflowFocusCandidate("plate-a", "Plate A", "Drill", "Bohrungen", 0, 0, 4, true),
            new WorkflowFocusCandidate("plate-b", "Plate B", "InsideContour", "Innenkonturen", 1, 3, 5, false)
        };

        var result = WorkflowFocusRecommendation.SelectNext(candidates);

        Assert.Null(result);
    }

    [Fact]
    public void SelectNext_PrefersLargestOpenGap_ThenPriority()
    {
        var candidates = new[]
        {
            new WorkflowFocusCandidate("plate-a", "Plate A", "InsideContour", "Innenkonturen", 1, 3, 6, true),
            new WorkflowFocusCandidate("plate-a", "Plate A", "Drill", "Bohrungen", 0, 5, 8, true),
            new WorkflowFocusCandidate("plate-b", "Plate B", "OutsideContour", "Außenkontur", 2, 5, 5, true)
        };

        var result = WorkflowFocusRecommendation.SelectNext(candidates);

        Assert.NotNull(result);
        Assert.Equal("Plate A", result!.PlateName);
        Assert.Equal("Bohrungen", result.GroupDisplayName);
    }

    [Fact]
    public void FormatLabel_UsesOpenVsTotalSummary()
    {
        var candidate = new WorkflowFocusCandidate("plate-a", "Seite links", "Drill", "Bohrungen", 0, 2, 6, true);

        var text = WorkflowFocusRecommendation.FormatLabel(candidate);

        Assert.Equal("Workflow-Fokus: Seite links · Bohrungen · 2 offen / 6 gesamt", text);
    }

    [Fact]
    public void FormatActionLabel_UsesGroupAndOpenCount()
    {
        var candidate = new WorkflowFocusCandidate("plate-a", "Seite links", "OutsideContour", "Außenkontur", 2, 1, 1, true);

        var text = WorkflowFocusRecommendation.FormatActionLabel(candidate);

        Assert.Equal("Außenkontur öffnen (1 offen)", text);
    }
}
