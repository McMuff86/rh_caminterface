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

    /// <summary>
    /// Phase 3: Proximity-based assignment.
    /// Assigns blocks to the plate whose bounding box contains the block's insertion point.
    /// Falls back to layer-based matching if proximity doesn't find a match.
    /// </summary>
    /// <param name="plates">Detected plates with origin and dimensions.</param>
    /// <param name="allBlocks">All detected fitting blocks.</param>
    /// <param name="tolerance">Distance tolerance in mm for proximity check.</param>
    /// <returns>Plates paired with their assigned blocks.</returns>
    public IReadOnlyList<(Plate Plate, IReadOnlyList<FittingBlock> Blocks)> ResolveWithProximity(
        IReadOnlyList<Plate> plates,
        IReadOnlyList<FittingBlock> allBlocks,
        double tolerance = 5.0)
    {
        var results = new List<(Plate, IReadOnlyList<FittingBlock>)>();
        var assignedBlocks = new HashSet<FittingBlock>(ReferenceEqualityComparer.Instance);
        var blocksByLayer = GroupByLayer(allBlocks);

        foreach (var plate in plates)
        {
            var assigned = new List<FittingBlock>();

            // Strategy 1: Layer match (same as Phase 2)
            if (plate.LayerPath != null && blocksByLayer.TryGetValue(plate.LayerPath, out var byPath))
            {
                foreach (var b in byPath)
                {
                    if (assignedBlocks.Add(b))
                        assigned.Add(b);
                }
            }
            else if (blocksByLayer.TryGetValue(plate.Name, out var byName))
            {
                foreach (var b in byName)
                {
                    if (assignedBlocks.Add(b))
                        assigned.Add(b);
                }
            }

            // Strategy 2: Explicit CNC_Plate attribute
            foreach (var block in allBlocks)
            {
                if (assignedBlocks.Contains(block)) continue;
                if (block.CncAttributes.TryGetValue("CNC_Plate", out var plateName)
                    && plateName.Equals(plate.Name, StringComparison.OrdinalIgnoreCase))
                {
                    if (assignedBlocks.Add(block))
                        assigned.Add(block);
                }
            }

            // Strategy 3: Proximity — block insertion point within plate bounds
            foreach (var block in allBlocks)
            {
                if (assignedBlocks.Contains(block)) continue;

                if (IsBlockNearPlate(block, plate, tolerance))
                {
                    if (assignedBlocks.Add(block))
                        assigned.Add(block);
                }
            }

            results.Add((plate, assigned));
        }

        return results;
    }

    /// <summary>
    /// Check if a block's insertion point is near/within a plate's bounding box.
    /// Uses the plate's origin and dimensions to define the plate volume in world space.
    /// </summary>
    private static bool IsBlockNearPlate(FittingBlock block, Plate plate, double tolerance)
    {
        var origin = plate.Origin;
        var (bx, by, bz) = block.InsertionPoint;

        // Transform block position to plate-local coordinates
        double dx = bx - origin.OriginX;
        double dy = by - origin.OriginY;
        double dz = bz - origin.OriginZ;

        double localX = dx * origin.XAxis.X + dy * origin.XAxis.Y + dz * origin.XAxis.Z;
        double localY = dx * origin.YAxis.X + dy * origin.YAxis.Y + dz * origin.YAxis.Z;
        double localZ = dx * origin.Normal.X + dy * origin.Normal.Y + dz * origin.Normal.Z;

        // Check if within plate bounds (with tolerance)
        return localX >= -tolerance && localX <= plate.LengthX + tolerance
            && localY >= -tolerance && localY <= plate.WidthY + tolerance
            && localZ >= -tolerance && localZ <= plate.Thickness + tolerance;
    }
}
