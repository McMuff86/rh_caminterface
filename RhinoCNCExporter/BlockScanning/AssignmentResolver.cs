using System;
using System.Collections.Generic;
using System.Linq;
using RhinoCNCExporter.Core.Models;

namespace RhinoCNCExporter.BlockScanning;

/// <summary>
/// Resolves which FittingBlocks belong to which Plates.
/// Phase 2: Pure layer-based assignment — block on layer X → belongs to plate on layer X.
/// Phase 3 (future): Proximity-based assignment for 3D models.
/// DEPENDS ON: Only Core Models (no RhinoCommon needed)
/// </summary>
public class AssignmentResolver
{
    /// <summary>
    /// Group fitting blocks by their layer name.
    /// Returns dictionary: layer name → list of blocks on that layer.
    /// </summary>
    public Dictionary<string, List<FittingBlock>> GroupByLayer(IReadOnlyList<FittingBlock> blocks)
    {
        var result = new Dictionary<string, List<FittingBlock>>(StringComparer.OrdinalIgnoreCase);

        foreach (var block in blocks)
        {
            var key = block.LayerName ?? "__unassigned__";

            if (!result.TryGetValue(key, out var list))
            {
                list = new List<FittingBlock>();
                result[key] = list;
            }

            list.Add(block);
        }

        return result;
    }

    /// <summary>
    /// Assign fitting blocks to plates based on matching layer names.
    /// A block on layer "Seite_links" → assigned to the plate with the same layer path.
    /// </summary>
    /// <param name="plates">Available plates.</param>
    /// <param name="allBlocks">All detected fitting blocks.</param>
    /// <returns>List of (Plate, assigned Blocks) tuples.</returns>
    public IReadOnlyList<(Plate Plate, IReadOnlyList<FittingBlock> Blocks)> Resolve(
        IReadOnlyList<Plate> plates,
        IReadOnlyList<FittingBlock> allBlocks)
    {
        var blocksByLayer = GroupByLayer(allBlocks);
        var results = new List<(Plate, IReadOnlyList<FittingBlock>)>();

        foreach (var plate in plates)
        {
            var assignedBlocks = new List<FittingBlock>();

            // Try matching by full layer path first, then by plate name
            if (plate.LayerPath != null && blocksByLayer.TryGetValue(plate.LayerPath, out var byPath))
            {
                assignedBlocks.AddRange(byPath);
            }
            else if (blocksByLayer.TryGetValue(plate.Name, out var byName))
            {
                assignedBlocks.AddRange(byName);
            }

            // Also check for blocks that explicitly reference this plate
            foreach (var block in allBlocks)
            {
                if (block.CncAttributes.TryGetValue("CNC_Plate", out var plateName)
                    && plateName.Equals(plate.Name, StringComparison.OrdinalIgnoreCase)
                    && !assignedBlocks.Contains(block))
                {
                    assignedBlocks.Add(block);
                }
            }

            results.Add((plate, assignedBlocks));
        }

        return results;
    }

    /// <summary>
    /// Simple assignment for Phase 2: returns blocks grouped by layer without plate matching.
    /// Useful when plates are not yet detected (legacy flat mode).
    /// </summary>
    public Dictionary<string, List<FittingBlock>> ResolveByLayer(IReadOnlyList<FittingBlock> allBlocks)
    {
        return GroupByLayer(allBlocks);
    }
}
