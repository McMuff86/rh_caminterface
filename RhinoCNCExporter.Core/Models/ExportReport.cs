namespace RhinoCNCExporter.Core.Models;

/// <summary>
/// Report generated after a multi-plate export.
/// Contains statistics, warnings, and exported file paths.
/// </summary>
public sealed class ExportReport
{
    public bool Success { get; set; }
    public string? Error { get; set; }

    /// <summary>Total plates detected in the document.</summary>
    public int TotalPlatesDetected { get; set; }

    /// <summary>Number of plates actually exported (after user selection).</summary>
    public int PlatesExported { get; set; }

    /// <summary>Total machining operations across all exported plates.</summary>
    public int TotalMachinings { get; set; }

    /// <summary>Total blocks detected in the document.</summary>
    public int TotalBlocksDetected { get; set; }

    /// <summary>Export mode used.</summary>
    public ExportMode Mode { get; set; }

    /// <summary>List of exported file paths.</summary>
    public List<string> ExportedFiles { get; set; } = new();

    /// <summary>Warnings generated during export.</summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>Per-plate details.</summary>
    public List<PlateExportDetail> PlateDetails { get; set; } = new();

    /// <summary>
    /// Generate a human-readable summary string.
    /// </summary>
    public string GetSummary()
    {
        var parts = new List<string>();
        parts.Add($"{PlatesExported} Platte(n) exportiert");
        parts.Add($"{TotalMachinings} Bearbeitung(en)");
        parts.Add($"{Warnings.Count} Warnung(en)");
        return string.Join(", ", parts);
    }
}

/// <summary>
/// Export details for a single plate.
/// </summary>
public sealed class PlateExportDetail
{
    public required string PlateName { get; init; }
    public required string FilePath { get; init; }
    public int MachiningCount { get; init; }
    public double LengthX { get; init; }
    public double WidthY { get; init; }
    public double Thickness { get; init; }
}
