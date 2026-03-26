using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using RhinoCNCExporter.BlockScanning;
using RhinoCNCExporter.Core.Blocks;
using RhinoCNCExporter.Core.Emitters;
using RhinoCNCExporter.Core.LayerParser;
using RhinoCNCExporter.Core.Models;
using RhinoCNCExporter.Core.Naming;
using RhinoCNCExporter.Core.Pipeline;
using RhinoCNCExporter.Core.PlateDetection;
using RhinoCNCExporter.Core.Profiles;
using RhinoCNCExporter.PlateDetection;

namespace RhinoCNCExporter.Services;

/// <summary>
/// Sprint 4 service for document analysis, export mode resolution and 3D multi-plate export.
/// </summary>
public static class ExportService3D
{
    public static DocumentExportAnalysis AnalyzeDocument(RhinoDoc doc)
    {
        ArgumentNullException.ThrowIfNull(doc);

        var detector = new PlateDetector();
        var plates = detector.DetectPlates(doc);

        var scanner = new BlockScanner();
        var blocks = scanner.ScanDocument(doc);

        var previews = BuildPlatePreviews(plates, blocks);
        var capabilities = DetectCapabilities(doc, plates, blocks);
        var decision = ExportModeResolver.Decide(capabilities, ExportMode.Automatic);

        return new DocumentExportAnalysis
        {
            Capabilities = capabilities,
            RecommendedMode = decision.ResolvedMode,
            RecommendationReason = decision.Reason,
            Plates = previews,
            TotalBlockCount = blocks.Count
        };
    }

    public static DocumentExportResult ExportDocument(
        RhinoDoc doc,
        string targetPath,
        MachineFormat format,
        ExportMode mode,
        bool enableBlockDetection,
        bool onlySelection,
        bool layerStepdown,
        double setupOffsetX,
        double setupOffsetY,
        IReadOnlySet<string>? selectedPlateKeys = null)
    {
        ArgumentNullException.ThrowIfNull(doc);

        var analysis = AnalyzeDocument(doc);
        var decision = ExportModeResolver.Decide(analysis.Capabilities, mode);
        if (!decision.IsExecutable)
        {
            return new DocumentExportResult
            {
                Success = false,
                RequestedMode = mode,
                ResolvedMode = decision.ResolvedMode,
                Analysis = analysis,
                Error = decision.Reason
            };
        }

        if (!TryCreateExportComponents(format, setupOffsetX, setupOffsetY,
                out var emitter, out var nameService, out var profile, out var creationError))
        {
            return new DocumentExportResult
            {
                Success = false,
                RequestedMode = mode,
                ResolvedMode = decision.ResolvedMode,
                Analysis = analysis,
                Error = creationError
            };
        }

        return decision.ResolvedMode == ExportMode.MultiPlate3D
            ? ExportMultiPlateDocument(doc, targetPath, mode, analysis, emitter!, profile!, selectedPlateKeys)
            : ExportLegacyDocument(doc, targetPath, mode, analysis, emitter!, nameService!, profile!,
                enableBlockDetection, onlySelection, layerStepdown);
    }

    public static MultiPlateExportResult ExportMultiPlate(
        RhinoDoc doc,
        string outputDirectory,
        IEmitter emitter,
        IMachineProfile profile,
        IReadOnlySet<string>? selectedPlateKeys = null)
    {
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentNullException.ThrowIfNull(emitter);
        ArgumentNullException.ThrowIfNull(profile);

        var result = new MultiPlateExportResult();

        try
        {
            var analysis = AnalyzeDocument(doc);
            result.DetectedPlates = analysis.Plates.Select(p => p.Plate).ToList();

            if (analysis.Plates.Count == 0)
            {
                result.Error = "No plates detected in document";
                return result;
            }

            Directory.CreateDirectory(outputDirectory);

            var plan = BatchExportPlanner.BuildPlan(
                analysis.Plates,
                outputDirectory,
                profile.FileExtension,
                selectedPlateKeys);

            if (plan.PlateCount == 0)
            {
                result.Error = "No plates selected for export";
                return result;
            }

            var exportedFiles = new List<string>();
            foreach (var item in plan.Plates)
            {
                var machinings = BuildMachiningsForPlate(doc, item.Preview.Plate, item.Preview.Blocks);
                var plateWithMachinings = item.Preview.Plate with { Machinings = machinings };

                var names = new NameService(profile.MaxNameLength);
                var plateEmitter = CreateFreshEmitter(emitter, names);
                var router = new EmitterRouter(plateEmitter, names, profile);
                var program = router.GenerateProgram(plateWithMachinings);

                File.WriteAllText(item.FilePath, program);
                exportedFiles.Add(item.FilePath);
            }

            result.Success = true;
            result.PlateCount = plan.PlateCount;
            result.TotalBlockCount = plan.TotalBlocks;
            result.TotalMachinings = plan.TotalMachinings;
            result.ExportedFiles = exportedFiles;
            result.Report = BatchExportPlanner.BuildReport(
                ExportMode.MultiPlate3D,
                plan,
                exportedFiles);
            return result;
        }
        catch (Exception ex)
        {
            result.Error = $"Multi-plate export failed: {ex.Message}";
            return result;
        }
    }

    private static DocumentExportResult ExportMultiPlateDocument(
        RhinoDoc doc,
        string targetPath,
        ExportMode requestedMode,
        DocumentExportAnalysis analysis,
        IEmitter emitter,
        IMachineProfile profile,
        IReadOnlySet<string>? selectedPlateKeys)
    {
        var outputDirectory = NormalizeOutputDirectory(targetPath, doc.Name);
        var result = ExportMultiPlate(doc, outputDirectory, emitter, profile, selectedPlateKeys);

        return new DocumentExportResult
        {
            Success = result.Success,
            RequestedMode = requestedMode,
            ResolvedMode = ExportMode.MultiPlate3D,
            Analysis = analysis,
            Error = result.Error,
            ExportedFiles = result.ExportedFiles,
            Report = result.Report
        };
    }

    private static DocumentExportResult ExportLegacyDocument(
        RhinoDoc doc,
        string targetPath,
        ExportMode requestedMode,
        DocumentExportAnalysis analysis,
        IEmitter emitter,
        NameService nameService,
        IMachineProfile profile,
        bool enableBlockDetection,
        bool onlySelection,
        bool layerStepdown)
    {
        var filePath = NormalizeSingleFilePath(targetPath, doc.Name, profile.FileExtension);

        if (enableBlockDetection)
        {
            var blockResult = BlockAwareExportService.ExportWithBlocks(
                doc,
                onlySelection,
                filePath,
                emitter,
                nameService,
                profile,
                enableBlockDetection: true,
                layerStepdown: layerStepdown);

            return new DocumentExportResult
            {
                Success = blockResult.Success,
                RequestedMode = requestedMode,
                ResolvedMode = ExportMode.LegacyOnly,
                Analysis = analysis,
                Error = blockResult.Error,
                ExportedFiles = blockResult.Success ? new[] { filePath } : Array.Empty<string>(),
                Report = blockResult.Success
                    ? new ExportSummaryReport
                    {
                        Mode = ExportMode.LegacyOnly,
                        PlateCount = 1,
                        TotalBlocks = blockResult.BlockCount,
                        TotalMachinings = blockResult.BlockMachinings.Count,
                        ExportedFiles = new[] { filePath }
                    }
                    : null
            };
        }

        var ok = ExportService.ExportCNC(
            doc,
            onlySelection,
            filePath,
            emitter,
            nameService,
            profile,
            layerStepdown);

        return new DocumentExportResult
        {
            Success = ok,
            RequestedMode = requestedMode,
            ResolvedMode = ExportMode.LegacyOnly,
            Analysis = analysis,
            Error = ok ? null : "Legacy export failed",
            ExportedFiles = ok ? new[] { filePath } : Array.Empty<string>(),
            Report = ok
                ? new ExportSummaryReport
                {
                    Mode = ExportMode.LegacyOnly,
                    PlateCount = 1,
                    TotalBlocks = 0,
                    TotalMachinings = 0,
                    ExportedFiles = new[] { filePath }
                }
                : null
        };
    }

    private static IReadOnlyList<PlatePreview> BuildPlatePreviews(
        IReadOnlyList<Plate> plates,
        IReadOnlyList<FittingBlock> blocks)
    {
        if (plates.Count == 0)
            return Array.Empty<PlatePreview>();

        var resolver = new AssignmentResolver();
        var assignments = resolver.ResolveWithProximity(plates, blocks);

        return assignments
            .Select(assignment => new PlatePreview
            {
                Plate = assignment.Plate,
                Blocks = assignment.Blocks.ToList(),
                MachiningCount = BuildMachiningsForPlate(null, assignment.Plate, assignment.Blocks).Count
            })
            .ToList();
    }

    internal static List<Machining> BuildMachiningsForPlate(
        RhinoDoc? doc,
        Plate plate,
        IReadOnlyList<FittingBlock> blocks)
    {
        var machinings = new List<Machining>();
        var clamexCounter = 0;

        foreach (var block in blocks)
        {
            var (localX, localY, localZ) = CoordinateTransformer.WorldToPlateLocal(
                plate.Origin,
                block.InsertionPoint.X,
                block.InsertionPoint.Y,
                block.InsertionPoint.Z);

            if (IsClamexMacro(block))
            {
                clamexCounter++;
                var machining = ClamexMacroBuilder.CreateMachining(
                    block,
                    localX,
                    localY,
                    plate.Thickness,
                    clamexCounter);

                if (machining != null)
                    machinings.Add(machining);

                continue;
            }

            machinings.AddRange(MachiningFactory.CreateFromBlock(
                block,
                localX,
                localY,
                localZ,
                plate.Thickness));
        }

        // Add face-tagged features (if document is available)
        if (doc != null)
        {
            var faceTaggedMachinings = ExtractFaceTaggedFeatures(doc, plate);
            machinings.AddRange(faceTaggedMachinings);

            // Add UserText-based operations from interactive CAM commands
            var userTextMachinings = ExtractUserTextOperations(doc, plate);
            machinings.AddRange(userTextMachinings);
        }

        return machinings;
    }

    /// <summary>
    /// Extract face-tagged CNC features from objects on the plate's layer.
    /// Finds objects that match the plate and reads their face-tagged features.
    /// </summary>
    private static List<Machining> ExtractFaceTaggedFeatures(RhinoDoc doc, Plate plate)
    {
        var faceTaggedMachinings = new List<Machining>();

        try
        {
            // Find objects on the plate's layer that might contain face-tagged features
            var plateObjects = FindPlateObjects(doc, plate);

            foreach (var obj in plateObjects)
            {
                var features = FeatureReader.ReadTaggedFeatures(obj);
                faceTaggedMachinings.AddRange(features);
            }

            RhinoApp.WriteLine($"[ExportService3D] Found {faceTaggedMachinings.Count} face-tagged features for plate '{plate.Name}'");
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"[ExportService3D] Error extracting face-tagged features: {ex.Message}");
        }

        return faceTaggedMachinings;
    }

    /// <summary>
    /// Extract UserText-based CNC operations from objects on the plate's layer.
    /// These are created by interactive CAM commands (CNCAddContour, CNCAddDrill, etc.).
    /// </summary>
    private static List<Machining> ExtractUserTextOperations(RhinoDoc doc, Plate plate)
    {
        var userTextMachinings = new List<Machining>();

        try
        {
            var reader = new UserTextMachiningReader();
            
            // Get UserText operations from the plate's layer
            if (!string.IsNullOrEmpty(plate.LayerPath))
            {
                // Try to extract layer name from full path (e.g., "Korpus::Seite_links" -> "Seite_links")
                var layerName = plate.LayerPath.Split(new[] { "::" }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? plate.LayerPath;
                var layerMachinings = reader.GetMachiningsByLayer(doc, layerName);
                userTextMachinings.AddRange(layerMachinings);
            }
            else if (!string.IsNullOrEmpty(plate.Name))
            {
                // Fallback: use plate name as layer name
                var layerMachinings = reader.GetMachiningsByLayer(doc, plate.Name);
                userTextMachinings.AddRange(layerMachinings);
            }

            if (userTextMachinings.Count > 0)
            {
                RhinoApp.WriteLine($"[ExportService3D] Found {userTextMachinings.Count} UserText operations for plate '{plate.Name}'");
            }
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"[ExportService3D] Error extracting UserText operations: {ex.Message}");
        }

        return userTextMachinings;
    }

    /// <summary>
    /// Find Rhino objects that likely belong to the given plate.
    /// Uses layer path and geometric criteria to identify the plate objects.
    /// </summary>
    private static IEnumerable<RhinoObject> FindPlateObjects(RhinoDoc doc, Plate plate)
    {
        var objects = new List<RhinoObject>();

        try
        {
            // Strategy 1: Find by layer path
            if (!string.IsNullOrEmpty(plate.LayerPath))
            {
                // Get layer by full path
                var layerIndex = doc.Layers.FindByFullPath(plate.LayerPath, -1);
                if (layerIndex >= 0)
                {
                    // Get all objects on this layer
                    var layer = doc.Layers[layerIndex];
                    var layerObjects = doc.Objects.FindByLayer(layer);
                    objects.AddRange(layerObjects.Where(obj => obj?.Geometry is Brep));
                }
            }

            // Strategy 2: If no layer path or no objects found, try by plate name
            if (objects.Count == 0)
            {
                var allObjects = doc.Objects.GetObjectList(Rhino.DocObjects.ObjectType.Brep);
                foreach (var obj in allObjects)
                {
                    if (obj.Name == plate.Name || 
                        (obj.Attributes.LayerIndex >= 0 && 
                         doc.Layers[obj.Attributes.LayerIndex]?.Name == plate.Name))
                    {
                        objects.Add(obj);
                    }
                }
            }

            // Strategy 3: If still no objects, look for objects with similar dimensions
            if (objects.Count == 0)
            {
                var allBreps = doc.Objects.GetObjectList(Rhino.DocObjects.ObjectType.Brep);
                foreach (var obj in allBreps)
                {
                    if (obj?.Geometry is Brep brep && IsGeometrySimilarToPlate(brep, plate))
                    {
                        objects.Add(obj);
                    }
                }
            }

            RhinoApp.WriteLine($"[ExportService3D] Found {objects.Count} candidate objects for plate '{plate.Name}'");
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"[ExportService3D] Error finding plate objects: {ex.Message}");
        }

        return objects;
    }

    /// <summary>
    /// Check if a Brep geometry has similar dimensions to the given plate.
    /// Used as a fallback when layer-based matching fails.
    /// </summary>
    private static bool IsGeometrySimilarToPlate(Brep brep, Plate plate)
    {
        try
        {
            var bbox = brep.GetBoundingBox(false);
            if (!bbox.IsValid) return false;

            var dimensions = new[] 
            { 
                bbox.Max.X - bbox.Min.X, 
                bbox.Max.Y - bbox.Min.Y, 
                bbox.Max.Z - bbox.Min.Z 
            };

            Array.Sort(dimensions);

            // Check if the largest two dimensions match LengthX and WidthY (within tolerance)
            var tolerance = 1.0; // 1mm tolerance
            var expectedDims = new[] { plate.LengthX, plate.WidthY, plate.Thickness };
            Array.Sort(expectedDims);

            return Math.Abs(dimensions[0] - expectedDims[0]) < tolerance &&
                   Math.Abs(dimensions[1] - expectedDims[1]) < tolerance &&
                   Math.Abs(dimensions[2] - expectedDims[2]) < tolerance;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsClamexMacro(FittingBlock block)
    {
        return block.CncType.Equals("MACRO", StringComparison.OrdinalIgnoreCase)
            && block.MacroName != null
            && block.MacroName.Equals("SawCut_Lamello", StringComparison.OrdinalIgnoreCase);
    }

    private static DocumentCapabilities DetectCapabilities(
        RhinoDoc doc,
        IReadOnlyList<Plate> plates,
        IReadOnlyList<FittingBlock> blocks)
    {
        var hasLegacyPiece = doc.Layers.Any(layer =>
            layer != null
            && !layer.IsDeleted
            && layer.Name.Equals("WK_PIECE", StringComparison.OrdinalIgnoreCase));

        var hasLegacyMachiningLayers = doc.Layers.Any(layer =>
        {
            if (layer == null || layer.IsDeleted)
                return false;

            var name = layer.Name ?? string.Empty;
            return LayerRegex.TryParseCut(name, out _)
                || LayerRegex.TryParsePocket(name, out _)
                || LayerRegex.TryParseDrill(name, out _)
                || LayerRegex.TryParseDrillPattern(name, out _)
                || LayerRegex.TryParseHorizontalDrill(name, out _)
                || LayerRegex.TryParseRow(name, out _)
                || LayerRegex.TryParseGrooveChannel(name, out _)
                || LayerRegex.TryParseGrooveRnt(name, out _);
        });

        return new DocumentCapabilities
        {
            HasLegacyPiece = hasLegacyPiece,
            HasLegacyMachiningLayers = hasLegacyMachiningLayers,
            HasBlocks = blocks.Count > 0,
            Has3DPlates = plates.Any(p => p.Source == PlateSource.SolidDetection),
            PlateCount = plates.Count
        };
    }

    private static bool TryCreateExportComponents(
        MachineFormat format,
        double setupOffsetX,
        double setupOffsetY,
        out IEmitter? emitter,
        out NameService? nameService,
        out IMachineProfile? profile,
        out string? error)
    {
        error = null;
        emitter = null;
        nameService = null;
        profile = null;

        IMachineProfile? baseProfile = format switch
        {
            MachineFormat.Xilog => new MaestroCadTProfile(),
            MachineFormat.Biesse => new BiesseProfile(),
            _ => null
        };

        if (baseProfile == null)
        {
            error = $"Machine format {format} is not implemented yet.";
            return false;
        }

        profile = new ConfigurableMachineProfile(baseProfile, setupOffsetX, setupOffsetY);
        nameService = new NameService(profile.MaxNameLength);
        emitter = format switch
        {
            MachineFormat.Xilog => new XilogEmitter(nameService),
            MachineFormat.Biesse => new BiesseEmitter(nameService),
            _ => null
        };

        if (emitter == null)
        {
            error = $"Machine format {format} is not implemented yet.";
            return false;
        }

        return true;
    }

    private static IEmitter CreateFreshEmitter(IEmitter prototype, NameService nameService)
    {
        return prototype switch
        {
            XilogEmitter => new XilogEmitter(nameService),
            BiesseEmitter => new BiesseEmitter(nameService),
            _ => throw new NotSupportedException($"Emitter {prototype.GetType().Name} is not supported.")
        };
    }

    private static string NormalizeOutputDirectory(string targetPath, string? documentName)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            var fallbackName = Path.GetFileNameWithoutExtension(documentName);
            fallbackName = string.IsNullOrWhiteSpace(fallbackName) ? "CNC-Export" : fallbackName;
            return Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), fallbackName);
        }

        if (Path.HasExtension(targetPath))
        {
            var directory = Path.GetDirectoryName(targetPath);
            var folderName = Path.GetFileNameWithoutExtension(targetPath);
            if (string.IsNullOrWhiteSpace(directory))
                directory = System.Environment.CurrentDirectory;
            return Path.Combine(directory, folderName);
        }

        return targetPath;
    }

    private static string NormalizeSingleFilePath(string targetPath, string? documentName, string fileExtension)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            var baseName = string.IsNullOrWhiteSpace(documentName)
                ? "program"
                : Path.GetFileNameWithoutExtension(documentName);
            return Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
                baseName + fileExtension);
        }

        return Path.HasExtension(targetPath)
            ? targetPath
            : targetPath + fileExtension;
    }
}

/// <summary>
/// Unified result for the export panel.
/// </summary>
public sealed class DocumentExportResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public ExportMode RequestedMode { get; set; }
    public ExportMode ResolvedMode { get; set; }
    public DocumentExportAnalysis? Analysis { get; set; }
    public IReadOnlyList<string> ExportedFiles { get; set; } = Array.Empty<string>();
    public ExportSummaryReport? Report { get; set; }
}
