using RhinoCNCExporter.Core.Models;

namespace RhinoCNCExporter.Core.Pipeline;

/// <summary>
/// Combines machinings from multiple sources (legacy layers + blocks + UserText operations)
/// into a unified list per plate.
/// Pure logic — receives already-parsed data.
/// </summary>
public class MachiningBuilder : IMachiningBuilder
{
    /// <summary>
    /// Merge legacy layer-based machinings with block-based machinings.
    /// Deduplicates by position (within tolerance) to avoid double-drilling.
    /// Block-sourced machinings take priority over legacy.
    /// </summary>
    public IReadOnlyList<Machining> MergeAndDeduplicate(
        IReadOnlyList<Machining> legacyMachinings,
        IReadOnlyList<Machining> blockMachinings,
        double positionTolerance = 0.5)
    {
        var result = new List<Machining>(blockMachinings);

        foreach (var legacy in legacyMachinings)
        {
            bool isDuplicate = blockMachinings.Any(block =>
                AreSamePosition(legacy, block, positionTolerance));

            if (!isDuplicate)
                result.Add(legacy);
        }

        return result;
    }

    /// <summary>
    /// Merge machinings from all sources: legacy layers, blocks, and UserText operations.
    /// Priority order: UserText (highest) > Blocks > Legacy Layers (lowest).
    /// Deduplicates by position to avoid conflicts.
    /// </summary>
    public IReadOnlyList<Machining> MergeAllSources(
        IReadOnlyList<Machining> legacyMachinings,
        IReadOnlyList<Machining> blockMachinings,
        IReadOnlyList<Machining> userTextMachinings,
        double positionTolerance = 0.5)
    {
        // Start with highest priority: UserText operations
        var result = new List<Machining>(userTextMachinings);

        // Add block-based machinings if they don't conflict with UserText
        foreach (var block in blockMachinings)
        {
            bool isDuplicate = userTextMachinings.Any(userText =>
                AreSamePosition(block, userText, positionTolerance));

            if (!isDuplicate)
                result.Add(block);
        }

        // Add legacy layer-based machinings if they don't conflict with higher priority sources
        foreach (var legacy in legacyMachinings)
        {
            bool isDuplicate = result.Any(existing =>
                AreSamePosition(legacy, existing, positionTolerance));

            if (!isDuplicate)
                result.Add(legacy);
        }

        return result;
    }

    private static bool AreSamePosition(Machining a, Machining b, double tol)
    {
        var (ax, ay) = GetPosition(a);
        var (bx, by) = GetPosition(b);
        if (ax is null || ay is null || bx is null || by is null) return false;

        // Also check same type class for meaningful dedup
        if (a.GetType() != b.GetType()) return false;

        return Math.Abs(ax.Value - bx.Value) < tol && Math.Abs(ay.Value - by.Value) < tol;
    }

    private static (double? X, double? Y) GetPosition(Machining m) => m switch
    {
        DrillMachining d => (d.X, d.Y),
        DrillPatternMachining dp => (dp.X, dp.Y),
        HorizontalDrillMachining h => (h.X, h.Y),
        _ => (null, null) // Non-positional machinings don't deduplicate
    };
}
