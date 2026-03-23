using System.Linq;

namespace RhinoCNCExporter.Core.Models;

/// <summary>
/// Pure capability snapshot of a Rhino document for export mode selection.
/// </summary>
public sealed record DocumentCapabilities
{
    public bool HasLegacyPiece { get; init; }
    public bool HasLegacyMachiningLayers { get; init; }
    public bool HasBlocks { get; init; }
    public bool Has3DPlates { get; init; }
    public int PlateCount { get; init; }

    public bool HasAnyGeometry =>
        HasLegacyPiece || HasLegacyMachiningLayers || HasBlocks || PlateCount > 0;
}

/// <summary>
/// Full analysis result used by the UI to preview exportable content.
/// </summary>
public sealed record DocumentExportAnalysis
{
    public required DocumentCapabilities Capabilities { get; init; }
    public required ExportMode RecommendedMode { get; init; }
    public string RecommendationReason { get; init; } = string.Empty;
    public IReadOnlyList<PlatePreview> Plates { get; init; } = Array.Empty<PlatePreview>();
    public int TotalBlockCount { get; init; }
}

/// <summary>
/// Preview data for one detected plate and its assigned blocks.
/// </summary>
public sealed record PlatePreview
{
    public required Plate Plate { get; init; }
    public IReadOnlyList<FittingBlock> Blocks { get; init; } = Array.Empty<FittingBlock>();
    public int MachiningCount { get; init; }
}

/// <summary>
/// Result of export mode resolution, including whether the requested mode can run.
/// </summary>
public sealed record ExportModeDecision
{
    public required ExportMode RequestedMode { get; init; }
    public required ExportMode ResolvedMode { get; init; }
    public bool IsExecutable { get; init; }
    public string Reason { get; init; } = string.Empty;
}

/// <summary>
/// Planned output for one exported plate.
/// </summary>
public sealed record PlateExportPlanItem
{
    public required PlatePreview Preview { get; init; }
    public required string FileName { get; init; }
    public required string FilePath { get; init; }
}

/// <summary>
/// Batch export plan for the 3D pipeline.
/// </summary>
public sealed record ExportBatchPlan
{
    public required string OutputDirectory { get; init; }
    public required string FileExtension { get; init; }
    public IReadOnlyList<PlateExportPlanItem> Plates { get; init; } = Array.Empty<PlateExportPlanItem>();

    public int PlateCount => Plates.Count;
    public int TotalBlocks => Plates.Sum(p => p.Preview.Blocks.Count);
    public int TotalMachinings => Plates.Sum(p => p.Preview.MachiningCount);
}

/// <summary>
/// Summary report shown in the UI after export.
/// </summary>
public sealed record ExportSummaryReport
{
    public required ExportMode Mode { get; init; }
    public int PlateCount { get; init; }
    public int TotalBlocks { get; init; }
    public int TotalMachinings { get; init; }
    public IReadOnlyList<string> ExportedFiles { get; init; } = Array.Empty<string>();

    public string SummaryLine => $"{PlateCount} Platten, {TotalMachinings} Bearbeitungen exportiert";
}
