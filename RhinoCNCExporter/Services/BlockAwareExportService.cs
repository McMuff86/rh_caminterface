using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Rhino;
using RhinoCNCExporter.BlockScanning;
using RhinoCNCExporter.Core.Blocks;
using RhinoCNCExporter.Core.Emitters;
using RhinoCNCExporter.Core.Models;
using RhinoCNCExporter.Core.Naming;
using RhinoCNCExporter.Core.Pipeline;
using RhinoCNCExporter.Core.Profiles;
using RhinoCNCExporter.Core.PlateDetection;
using RhinoCNCExporter.PlateDetection;

namespace RhinoCNCExporter.Services;

/// <summary>
/// Extended export service that integrates block-based CNC detection
/// alongside the legacy layer-based pipeline.
///
/// Workflow:
///   1. Legacy ExportService scan (unchanged, writes file as before)
///   2. BlockScanner scan (NEW — finds CNC_* block inserts)
///   3. MachiningFactory: Blocks → Machinings
///   4. MachiningBuilder: Merge + Deduplicate
///   5. EmitterRouter: Machinings → CNC program
///   6. File write
///
/// The legacy ExportService is NOT modified. This service wraps it
/// and adds block-detection on top.
/// </summary>
public static class BlockAwareExportService
{
    /// <summary>
    /// Export CNC program with block detection enabled.
    /// Falls back to legacy ExportService if no blocks are found or on error.
    /// </summary>
    /// <param name="doc">Active Rhino document.</param>
    /// <param name="onlySelection">Export only selected objects.</param>
    /// <param name="filePath">Output file path.</param>
    /// <param name="emitter">CNC format emitter.</param>
    /// <param name="nameService">Name generation service.</param>
    /// <param name="profile">Machine profile.</param>
    /// <param name="enableBlockDetection">Feature flag: enable block scanning.</param>
    /// <param name="layerStepdown">Use layer-based stepdown.</param>
    /// <returns>Export result with details.</returns>
    public static BlockExportResult ExportWithBlocks(
        RhinoDoc doc,
        bool onlySelection,
        string filePath,
        IEmitter emitter,
        NameService nameService,
        IMachineProfile profile,
        bool enableBlockDetection = true,
        bool layerStepdown = false)
    {
        var result = new BlockExportResult();

        try
        {
            // Step 1: Scan for blocks (if enabled)
            List<FittingBlock> detectedBlocks = new();
            if (enableBlockDetection)
            {
                var scanner = new BlockScanner();
                detectedBlocks = onlySelection
                    ? scanner.ScanSelection(doc).ToList()
                    : scanner.ScanDocument(doc).ToList();

                result.DetectedBlocks = detectedBlocks;
                result.BlockCount = detectedBlocks.Count;
            }

            // Step 2: If blocks found, use enhanced pipeline
            if (detectedBlocks.Count > 0)
            {
                var blockMachinings = ConvertBlocksToMachinings(detectedBlocks, profile);
                result.BlockMachinings = blockMachinings;

                // For now, we still use legacy export as the base,
                // then the block machinings can be logged/displayed in UI.
                // Full integration (merge into file) requires plate detection (Sprint 3).
                // Phase 2: Write block info as comments at end of file.
            }

            // Step 3: Legacy export (always runs as the primary pipeline)
            bool legacyOk = ExportService.ExportCNC(doc, onlySelection, filePath,
                emitter, nameService, profile, layerStepdown);

            if (!legacyOk)
            {
                result.Success = false;
                result.Error = "Legacy export failed";
                return result;
            }

            // Step 4: If blocks were found, append block info as comments
            if (detectedBlocks.Count > 0 && result.BlockMachinings.Count > 0)
            {
                AppendBlockComments(filePath, detectedBlocks, result.BlockMachinings);
            }

            result.Success = true;
            return result;
        }
        catch (Exception ex)
        {
            // Fallback: try legacy export
            result.Error = $"Block-aware export error: {ex.Message}";
            try
            {
                result.Success = ExportService.ExportCNC(doc, onlySelection, filePath,
                    emitter, nameService, profile, layerStepdown);
            }
            catch
            {
                result.Success = false;
            }
            return result;
        }
    }

    /// <summary>
    /// Scan document for CNC blocks without exporting.
    /// Used for UI preview ("Blöcke scannen" button).
    /// </summary>
    public static IReadOnlyList<FittingBlock> ScanBlocks(RhinoDoc doc, bool onlySelection = false)
    {
        var scanner = new BlockScanner();
        return onlySelection
            ? scanner.ScanSelection(doc)
            : scanner.ScanDocument(doc);
    }

    /// <summary>
    /// Get a summary of detected blocks grouped by type.
    /// Returns lines like "3× Topfband_35", "2× CLAMEX_P14"
    /// </summary>
    public static IReadOnlyList<string> GetBlockSummary(IReadOnlyList<FittingBlock> blocks)
    {
        return blocks
            .GroupBy(b => b.BlockName)
            .OrderByDescending(g => g.Count())
            .Select(g => $"{g.Count()}× {g.Key} ({g.First().CncType})")
            .ToList();
    }

    /// <summary>
    /// Convert blocks to machinings using the MachiningFactory.
    /// Phase 2: Uses identity transform (flat plate at Z=0).
    /// </summary>
    private static List<Machining> ConvertBlocksToMachinings(
        List<FittingBlock> blocks, IMachineProfile profile)
    {
        var machinings = new List<Machining>();
        double defaultThickness = 19; // Default DZ, will be resolved from WK_PIECE in Sprint 3

        foreach (var block in blocks)
        {
            // Phase 2: identity transform (plate-local = world coords)
            var created = MachiningFactory.CreateFromBlock(
                block,
                block.InsertionPoint.X,
                block.InsertionPoint.Y,
                block.InsertionPoint.Z,
                defaultThickness);

            machinings.AddRange(created);
        }

        return machinings;
    }

    /// <summary>
    /// Append block detection info as comments to the exported CNC file.
    /// Non-destructive: only adds comments at the end.
    /// </summary>
    private static void AppendBlockComments(string filePath, List<FittingBlock> blocks, List<Machining> machinings)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("// *** Block-Detection Results ***");
            sb.AppendLine($"// Detected {blocks.Count} CNC block(s):");

            foreach (var group in blocks.GroupBy(b => b.BlockName))
            {
                sb.AppendLine($"//   {group.Count()}× {group.Key} (Type={group.First().CncType})");
            }

            sb.AppendLine($"// Generated {machinings.Count} machining operation(s) from blocks.");
            sb.AppendLine("// *** End Block-Detection ***");

            File.AppendAllText(filePath, sb.ToString(), Encoding.UTF8);
        }
        catch
        {
            // Non-critical: don't fail export if comments can't be appended
        }
    }
}

    /// <summary>
    /// Export multiple plates, each to a separate CNC file.
    /// This is the full Sprint 3 3D pipeline:
    ///   PlateDetector → BlockScanner → AssignmentResolver → CoordinateTransformer
    ///   → MachiningFactory → EmitterRouter → per-plate file
    /// </summary>
    /// <param name="doc">Active Rhino document.</param>
    /// <param name="outputDirectory">Directory to write .xcs files to.</param>
    /// <param name="emitter">CNC format emitter.</param>
    /// <param name="profile">Machine profile.</param>
    /// <param name="selectedPlateNames">If set, only export these plates. Null = export all.</param>
    /// <returns>Multi-plate export result.</returns>
    public static MultiPlateExportResult ExportMultiPlate(
        RhinoDoc doc,
        string outputDirectory,
        IEmitter emitter,
        IMachineProfile profile,
        IReadOnlySet<string>? selectedPlateNames = null)
    {
        var result = new MultiPlateExportResult();

        try
        {
            // Step 1: Detect plates
            var detector = new PlateDetector();
            var allPlates = detector.DetectPlates(doc);
            result.DetectedPlates = allPlates.ToList();

            if (allPlates.Count == 0)
            {
                result.Error = "No plates detected in document";
                return result;
            }

            // Filter plates if selection provided
            var platesToExport = selectedPlateNames != null
                ? allPlates.Where(p => selectedPlateNames.Contains(p.Name)).ToList()
                : allPlates.ToList();

            // Step 2: Scan blocks
            var scanner = new BlockScanner();
            var allBlocks = scanner.ScanDocument(doc);
            result.TotalBlockCount = allBlocks.Count;

            // Step 3: Assign blocks to plates (with proximity)
            var resolver = new AssignmentResolver();
            var assignments = resolver.ResolveWithProximity(platesToExport, allBlocks);

            // Step 4: For each plate, transform coordinates and generate CNC program
            var exportedFiles = new List<string>();
            var clamexCounter = 0;

            foreach (var (plate, blocks) in assignments)
            {
                var machinings = new List<Machining>();

                foreach (var block in blocks)
                {
                    // Transform block position to plate-local coordinates
                    var (localX, localY, localZ) = CoordinateTransformer.WorldToPlateLocal(
                        plate.Origin,
                        block.InsertionPoint.X,
                        block.InsertionPoint.Y,
                        block.InsertionPoint.Z);

                    // Special handling for CLAMEX blocks
                    if (block.CncType.Equals("MACRO", StringComparison.OrdinalIgnoreCase)
                        && block.MacroName != null
                        && block.MacroName.Equals("SawCut_Lamello", StringComparison.OrdinalIgnoreCase))
                    {
                        clamexCounter++;
                        var clamexMachining = ClamexMacroBuilder.CreateMachining(
                            block, localX, localY, plate.Thickness, clamexCounter);
                        if (clamexMachining != null)
                            machinings.Add(clamexMachining);
                    }
                    else
                    {
                        // Standard block → machining conversion
                        var created = MachiningFactory.CreateFromBlock(
                            block, localX, localY, localZ, plate.Thickness);
                        machinings.AddRange(created);
                    }
                }

                // Build plate with machinings
                var plateWithMachinings = plate with { Machinings = machinings };

                // Generate CNC program
                var nameService = new NameService();
                var router = new EmitterRouter(emitter, nameService, profile);
                var program = router.GenerateProgram(plateWithMachinings);

                // Write file
                var extension = emitter is XilogEmitter ? ".xcs" : ".cix";
                var fileName = SanitizeFileName(plate.Name) + extension;
                var filePath = Path.Combine(outputDirectory, fileName);

                File.WriteAllText(filePath, program, Encoding.UTF8);
                exportedFiles.Add(filePath);
            }

            result.ExportedFiles = exportedFiles;
            result.Success = true;
            result.PlateCount = platesToExport.Count;
            result.TotalMachinings = assignments.Sum(a =>
            {
                // Count machinings per plate (computed on-the-fly)
                var count = 0;
                foreach (var block in a.Blocks)
                {
                    if (block.CncType.Equals("MACRO", StringComparison.OrdinalIgnoreCase))
                        count++;
                    else
                        count += MachiningFactory.CreateFromBlock(block, 0, 0, 0, 19).Count;
                }
                return count;
            });

            return result;
        }
        catch (Exception ex)
        {
            result.Error = $"Multi-plate export failed: {ex.Message}";
            return result;
        }
    }

    /// <summary>
    /// Detect plates in the document (for UI preview).
    /// </summary>
    public static IReadOnlyList<Plate> DetectPlates(RhinoDoc doc)
    {
        var detector = new PlateDetector();
        return detector.DetectPlates(doc);
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            sb.Append(invalid.Contains(c) ? '_' : c);
        }
        return sb.ToString();
    }
}

/// <summary>
/// Result of a block-aware export operation.
/// </summary>
public class BlockExportResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int BlockCount { get; set; }
    public List<FittingBlock> DetectedBlocks { get; set; } = new();
    public List<Machining> BlockMachinings { get; set; } = new();
}

/// <summary>
/// Result of a multi-plate export operation.
/// </summary>
public class MultiPlateExportResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int PlateCount { get; set; }
    public int TotalBlockCount { get; set; }
    public int TotalMachinings { get; set; }
    public List<Plate> DetectedPlates { get; set; } = new();
    public List<string> ExportedFiles { get; set; } = new();
}
