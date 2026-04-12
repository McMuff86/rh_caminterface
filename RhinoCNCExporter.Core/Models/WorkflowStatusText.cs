using System.Globalization;

namespace RhinoCNCExporter.Core.Models;

public static class WorkflowStatusText
{
    public static string FormatOpenVsTotal(int openCount, int totalCount)
    {
        var normalizedTotal = Math.Max(0, totalCount);
        var normalizedOpen = Math.Clamp(openCount, 0, normalizedTotal);
        return string.Create(CultureInfo.InvariantCulture, $"{normalizedOpen} offen / {normalizedTotal} gesamt");
    }
}
