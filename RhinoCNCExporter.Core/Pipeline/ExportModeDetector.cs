using System.Text.RegularExpressions;

namespace RhinoCNCExporter.Core.Pipeline;

using RhinoCNCExporter.Core.Models;

/// <summary>
/// Detects the appropriate export mode based on document content.
/// Pure logic — receives layer names and geometry info, no RhinoCommon.
/// </summary>
public static class ExportModeDetector
{
    /// <summary>Regex patterns for legacy CNC layer names.</summary>
    private static readonly Regex LegacyCncLayerPattern = new(
        @"^(CUT_|POCKET_|DRILL_|DRILLROW_|DRILLPAT_|HDRILL_|RBNUT_|WK_PIECE)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Detect the recommended export mode based on what's in the document.
    /// </summary>
    /// <param name="layerNames">All layer names in the document.</param>
    /// <param name="hasSolidsOrExtrusions">Whether the document contains Solids/Extrusions on non-CNC layers.</param>
    /// <param name="hasBlockInserts">Whether CNC_* block inserts were found.</param>
    /// <returns>Recommended export mode.</returns>
    public static ExportMode Detect(
        IReadOnlyList<string> layerNames,
        bool hasSolidsOrExtrusions,
        bool hasBlockInserts)
    {
        bool hasLegacyLayers = layerNames.Any(n => LegacyCncLayerPattern.IsMatch(n));
        bool has3DContent = hasSolidsOrExtrusions || hasBlockInserts;

        if (has3DContent && hasLegacyLayers)
            return ExportMode.Auto;

        if (has3DContent)
            return ExportMode.ThreeD;

        if (hasLegacyLayers)
            return ExportMode.Legacy;

        // Nothing found — default to Legacy (existing behavior)
        return ExportMode.Legacy;
    }

    /// <summary>
    /// Check if a layer name matches a legacy CNC pattern.
    /// </summary>
    public static bool IsLegacyCncLayer(string layerName)
    {
        return LegacyCncLayerPattern.IsMatch(layerName);
    }
}
