namespace RhinoCNCExporter.Core.Models;

/// <summary>
/// Statistics about CNC operations in a document.
/// Provides counts by operation type, tool changes, max depth,
/// and estimated machining time.
/// </summary>
public class OperationStatistics
{
    /// <summary>Total number of operations across all types.</summary>
    public int TotalOperations { get; set; }

    /// <summary>Number of contour operations.</summary>
    public int ContourCount { get; set; }

    /// <summary>Number of pocket operations.</summary>
    public int PocketCount { get; set; }

    /// <summary>Number of drill operations.</summary>
    public int DrillCount { get; set; }

    /// <summary>Number of groove operations.</summary>
    public int GrooveCount { get; set; }

    /// <summary>Estimated number of tool changes (unique tools - 1).</summary>
    public int ToolChanges { get; set; }

    /// <summary>Maximum depth across all operations in mm.</summary>
    public double MaxDepth { get; set; }

    /// <summary>Estimated total machining time in minutes.</summary>
    public double EstimatedTimeMinutes { get; set; }

    /// <summary>
    /// Returns a formatted summary string for display in the UI.
    /// Example: "3 Op. (2× Contour, 1× Drill) | 1 Werkzeugwechsel | Max. Tiefe: 19.0mm | Zeit: ~2.5 min"
    /// </summary>
    public string FormatSummary()
    {
        var parts = new List<string>();

        if (ContourCount > 0) parts.Add($"{ContourCount}× Contour");
        if (PocketCount > 0) parts.Add($"{PocketCount}× Pocket");
        if (DrillCount > 0) parts.Add($"{DrillCount}× Drill");
        if (GrooveCount > 0) parts.Add($"{GrooveCount}× Groove");

        var typeSummary = parts.Count > 0 ? string.Join(", ", parts) : "—";

        var timeParts = new List<string>();
        if (EstimatedTimeMinutes >= 1)
            timeParts.Add($"~{EstimatedTimeMinutes:F1} min");
        else if (EstimatedTimeMinutes > 0)
            timeParts.Add($"~{EstimatedTimeMinutes * 60:F0} sec");

        var timeSummary = timeParts.Count > 0 ? timeParts[0] : "—";

        return $"{TotalOperations} Op. ({typeSummary}) | {ToolChanges} Werkzeugwechsel | Max. Tiefe: {MaxDepth:F1}mm | Zeit: {timeSummary}";
    }
}
