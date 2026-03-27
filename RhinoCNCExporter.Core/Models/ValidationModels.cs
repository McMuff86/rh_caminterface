namespace RhinoCNCExporter.Core.Models;

/// <summary>
/// Severity levels for validation issues found during pre-export checks.
/// </summary>
public enum Severity
{
    /// <summary>Informational message, does not block export.</summary>
    Info,
    /// <summary>Warning that should be reviewed but does not block export.</summary>
    Warning,
    /// <summary>Error that blocks export until resolved.</summary>
    Error
}

/// <summary>
/// A single validation issue found during pre-export checks.
/// </summary>
public class ValidationIssue
{
    /// <summary>Severity level of this issue.</summary>
    public Severity Severity { get; set; }

    /// <summary>Human-readable description of the issue.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Optional GUID of the Rhino object that caused this issue.</summary>
    public Guid? ObjectId { get; set; }

    /// <summary>Category for grouping issues (e.g., "Werkzeug", "Geometrie", "Tiefe").</summary>
    public string Category { get; set; } = string.Empty;
}

/// <summary>
/// Result of a validation run containing all discovered issues.
/// Provides convenience properties for checking error/warning status
/// and a formatted summary string.
/// </summary>
public class ValidationResult
{
    /// <summary>All issues discovered during validation.</summary>
    public List<ValidationIssue> Issues { get; } = new();

    /// <summary>True if any issue has Error severity.</summary>
    public bool HasErrors => Issues.Any(i => i.Severity == Severity.Error);

    /// <summary>True if any issue has Warning severity.</summary>
    public bool HasWarnings => Issues.Any(i => i.Severity == Severity.Warning);

    /// <summary>True if no issues were found at all.</summary>
    public bool IsClean => Issues.Count == 0;

    /// <summary>Count of Error-severity issues.</summary>
    public int ErrorCount => Issues.Count(i => i.Severity == Severity.Error);

    /// <summary>Count of Warning-severity issues.</summary>
    public int WarningCount => Issues.Count(i => i.Severity == Severity.Warning);

    /// <summary>Count of Info-severity issues.</summary>
    public int InfoCount => Issues.Count(i => i.Severity == Severity.Info);

    /// <summary>
    /// Returns a short German summary string like "2 Fehler, 3 Warnungen".
    /// </summary>
    public string FormatSummary()
    {
        var parts = new List<string>();
        if (ErrorCount > 0)
            parts.Add($"{ErrorCount} Fehler");
        if (WarningCount > 0)
            parts.Add($"{WarningCount} Warnung{(WarningCount != 1 ? "en" : "")}");
        if (InfoCount > 0)
            parts.Add($"{InfoCount} Info{(InfoCount != 1 ? "s" : "")}");
        return parts.Count > 0 ? string.Join(", ", parts) : "Keine Probleme gefunden";
    }
}
