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
        IReadOnlySet<string>? selectedPlateKeys = null)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
            throw new ArgumentException("Output directory is required.", nameof(outputDirectory));

        if (string.IsNullOrWhiteSpace(fileExtension))
            throw new ArgumentException("File extension is required.", nameof(fileExtension));

        var normalizedExtension = fileExtension.StartsWith(".", StringComparison.Ordinal)
            ? fileExtension
            : "." + fileExtension;

        var filteredPreviews = selectedPlateKeys is { Count: > 0 }
            ? previews.Where(p => IsSelected(p, selectedPlateKeys)).ToList()
            : previews.ToList();

        var usedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var items = filteredPreviews
            .Select(preview =>
            {
                var fileName = CreateUniqueFileName(preview.Plate.Name, normalizedExtension, usedFileNames);
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

    private static bool IsSelected(PlatePreview preview, IReadOnlySet<string> selectedPlateKeys)
    {
        var selectionKey = GetSelectionKey(preview.Plate);
        return selectedPlateKeys.Contains(selectionKey)
            || selectedPlateKeys.Contains(preview.Plate.Name);
    }

    internal static string GetSelectionKey(Plate plate)
    {
        return string.IsNullOrWhiteSpace(plate.LayerPath)
            ? plate.Name
            : plate.LayerPath;
    }

    private static string CreateUniqueFileName(
        string plateName,
        string normalizedExtension,
        ISet<string> usedFileNames)
    {
        var baseName = SanitizeFileName(plateName);
        var candidate = baseName + normalizedExtension;
        var suffix = 2;

        while (!usedFileNames.Add(candidate))
        {
            candidate = $"{baseName}_{suffix}{normalizedExtension}";
            suffix++;
        }

        return candidate;
    }
}
