using RhinoCNCExporter.Core.Models;

namespace RhinoCNCExporter.Core.Pipeline;

/// <summary>
/// Builds machinings from various sources (Blocks, Legacy Layers, UserText Operations).
/// Merges and deduplicates results.
/// </summary>
public interface IMachiningBuilder
{
    /// <summary>
    /// Merge legacy layer-based machinings with block-based machinings.
    /// Block-sourced machinings take priority over legacy.
    /// </summary>
    IReadOnlyList<Machining> MergeAndDeduplicate(
        IReadOnlyList<Machining> legacyMachinings,
        IReadOnlyList<Machining> blockMachinings,
        double positionTolerance = 0.5);

    /// <summary>
    /// Merge machinings from all sources: legacy layers, blocks, and UserText operations.
    /// Priority order: UserText (highest) > Blocks > Legacy Layers (lowest).
    /// </summary>
    IReadOnlyList<Machining> MergeAllSources(
        IReadOnlyList<Machining> legacyMachinings,
        IReadOnlyList<Machining> blockMachinings,
        IReadOnlyList<Machining> userTextMachinings,
        double positionTolerance = 0.5);
}
