using System.Globalization;

namespace RhinoCNCExporter.Core.Models;

public static class WorkflowSummaryText
{
    public static string Format(
        int blockMachiningCount,
        int faceFeatureCount,
        int manualMachiningCount,
        int openGroupCount,
        int readyGroupCount,
        bool hasKnownAssignmentStatus)
    {
        var normalizedBlockMachinings = Math.Max(0, blockMachiningCount);
        var normalizedFaceFeatures = Math.Max(0, faceFeatureCount);
        var normalizedManualMachinings = Math.Max(0, manualMachiningCount);
        var normalizedOpenGroups = Math.Max(0, openGroupCount);
        var normalizedReadyGroups = Math.Max(0, readyGroupCount);
        var sourceSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"Block-Ops={normalizedBlockMachinings} · Face-Features={normalizedFaceFeatures} · Manuell={normalizedManualMachinings}");

        if (!hasKnownAssignmentStatus)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"Workflow: {sourceSummary} · Werkzeugstatus nach Maschinenwahl");
        }

        if (normalizedOpenGroups > 0)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"Workflow: {FormatGroupCount(normalizedOpenGroups)} offen · {FormatGroupCount(normalizedReadyGroups)} bereit · {sourceSummary}");
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"Workflow: Alle {FormatGroupCount(normalizedReadyGroups)} bereit · {sourceSummary}");
    }

    private static string FormatGroupCount(int count)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{count} {(count == 1 ? "Gruppe" : "Gruppen")}");
    }
}
