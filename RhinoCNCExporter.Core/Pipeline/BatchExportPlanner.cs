using System.IO;
using RhinoCNCExporter.Core.Models;

namespace RhinoCNCExporter.Core.Pipeline;

/// <summary>
/// Pure planning helpers for multi-plate batch export.
/// </summary>
public static class BatchExportPlanner
{
    public static ExportBatchPlan BuildPlan(
        IReadOnlyList<PlatePreview> previews,
        string outputDirectory,
        string fileExtension,
        IReadOnlySet<string>? selectedPlateNames = null)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
            throw new ArgumentException("Output directory is required.", nameof(outputDirectory));

        if (string.IsNullOrWhiteSpace(fileExtension))
            throw new ArgumentException("File extension is required.", nameof(fileExtension));

        var normalizedExtension = fileExtension.StartsWith(".", StringComparison.Ordinal)
            ? fileExtension
            : "." + fileExtension;

        var filteredPreviews = selectedPlateNames is { Count: > 0 }
            ? previews.Where(p => selectedPlateNames.Contains(p.Plate.Name)).ToList()
            : previews.ToList();

        var items = filteredPreviews
            .Select(preview =>
            {
                var fileName = SanitizeFileName(preview.Plate.Name) + normalizedExtension;
                return new PlateExportPlanItem
                {
                    Preview = preview,
                    FileName = fileName,
                    FilePath = Path.Combine(outputDirectory, fileName)
                };
            })
            .ToList();

        return new ExportBatchPlan
        {
            OutputDirectory = outputDirectory,
            FileExtension = normalizedExtension,
            Plates = items
        };
    }

    public static ExportSummaryReport BuildReport(
        ExportMode mode,
        ExportBatchPlan plan,
        IReadOnlyList<string> exportedFiles)
    {
        return new ExportSummaryReport
        {
            Mode = mode,
            PlateCount = plan.PlateCount,
            TotalBlocks = plan.TotalBlocks,
            TotalMachinings = plan.TotalMachinings,
            ExportedFiles = exportedFiles.ToList()
        };
    }

    public static string SanitizeFileName(string name)
    {
        var source = string.IsNullOrWhiteSpace(name) ? "plate" : name.Trim();
        var invalid = Path.GetInvalidFileNameChars();
        var chars = source
            .Select(c => invalid.Contains(c) ? '_' : c)
            .ToArray();

        return new string(chars);
    }
}
