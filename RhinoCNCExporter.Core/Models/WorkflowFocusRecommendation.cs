using System.Globalization;

namespace RhinoCNCExporter.Core.Models;

public sealed record WorkflowFocusCandidate(
    string PlateKey,
    string PlateName,
    string GroupKey,
    string GroupDisplayName,
    int Priority,
    int OpenCount,
    int TotalCount,
    bool HasAssignmentStatus);

public static class WorkflowFocusRecommendation
{
    public static WorkflowFocusCandidate? SelectNext(IEnumerable<WorkflowFocusCandidate>? candidates)
    {
        if (candidates == null)
        {
            return null;
        }

        return candidates
            .Where(candidate => candidate.HasAssignmentStatus && candidate.OpenCount > 0)
            .OrderByDescending(candidate => candidate.OpenCount)
            .ThenBy(candidate => candidate.Priority)
            .ThenByDescending(candidate => candidate.TotalCount)
            .ThenBy(candidate => candidate.PlateName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.GroupDisplayName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    public static string FormatLabel(WorkflowFocusCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"Workflow-Fokus: {candidate.PlateName} · {candidate.GroupDisplayName} · {WorkflowStatusText.FormatOpenVsTotal(candidate.OpenCount, candidate.TotalCount)}");
    }

    public static string FormatActionLabel(WorkflowFocusCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{candidate.GroupDisplayName} öffnen ({candidate.OpenCount} offen)");
    }
}
