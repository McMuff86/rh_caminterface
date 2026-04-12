using Rhino;
using Rhino.DocObjects;
using RhinoCNCExporter.BlockScanning;
using RhinoCNCExporter.Core.Blocks;
using RhinoCNCExporter.Core.Models;
using RhinoCNCExporter.Core.PlateDetection;

namespace RhinoCNCExporter.Services;

/// <summary>
/// Builds a document-aware workflow snapshot for one plate.
/// This is the shared read model between feature detection, manual/UserText CAM,
/// block-derived machinings, preview planning, and later workflow-driven UI stages.
/// </summary>
public sealed class WorkflowSnapshotService
{
    private readonly UserTextMachiningReader _userTextMachiningReader = new();

    public IReadOnlyList<PlateWorkflowSnapshot> BuildSnapshots(
        RhinoDoc? doc,
        IReadOnlyList<PlatePreview> previews)
    {
        ArgumentNullException.ThrowIfNull(previews);

        return previews
            .Select(preview => BuildSnapshot(doc, preview.Plate, preview.Blocks))
            .ToList();
    }

    public PlateWorkflowSnapshot BuildSnapshot(
        RhinoDoc? doc,
        Plate plate,
        IReadOnlyList<FittingBlock> blocks)
    {
        ArgumentNullException.ThrowIfNull(plate);
        ArgumentNullException.ThrowIfNull(blocks);

        var blockMachinings = BuildBlockMachinings(plate, blocks);
        var faceTaggedMachinings = doc != null
            ? ExtractFaceTaggedFeatures(doc, plate)
            : new List<Machining>();
        var userTextMachinings = doc != null
            ? _userTextMachiningReader.GetMachiningsForPlate(doc, plate).ToList()
            : new List<Machining>();

        var combinedMachinings = new List<Machining>(
            blockMachinings.Count + faceTaggedMachinings.Count + userTextMachinings.Count);
        combinedMachinings.AddRange(blockMachinings);
        combinedMachinings.AddRange(faceTaggedMachinings);
        combinedMachinings.AddRange(userTextMachinings);

        return new PlateWorkflowSnapshot
        {
            Plate = plate,
            Blocks = blocks.ToList(),
            BlockMachinings = blockMachinings,
            FaceTaggedMachinings = faceTaggedMachinings,
            UserTextMachinings = userTextMachinings,
            CombinedMachinings = combinedMachinings
        };
    }

    private static List<Machining> BuildBlockMachinings(Plate plate, IReadOnlyList<FittingBlock> blocks)
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

        return machinings;
    }

    private static List<Machining> ExtractFaceTaggedFeatures(RhinoDoc doc, Plate plate)
    {
        var faceTaggedMachinings = new List<Machining>();

        try
        {
            var plateObjects = ExportService3D.FindPlateObjects(doc, plate);
            foreach (var obj in plateObjects)
            {
                var features = FeatureReader.ReadTaggedFeatures(obj);
                faceTaggedMachinings.AddRange(features);
            }
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"[WorkflowSnapshot] Error extracting face-tagged features: {ex.Message}");
        }

        return faceTaggedMachinings;
    }

    private static bool IsClamexMacro(FittingBlock block)
    {
        return block.CncType.Equals("MACRO", StringComparison.OrdinalIgnoreCase)
            && block.MacroName != null
            && block.MacroName.Equals("SawCut_Lamello", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record PlateWorkflowSnapshot
{
    public required Plate Plate { get; init; }
    public IReadOnlyList<FittingBlock> Blocks { get; init; } = Array.Empty<FittingBlock>();
    public IReadOnlyList<Machining> BlockMachinings { get; init; } = Array.Empty<Machining>();
    public IReadOnlyList<Machining> FaceTaggedMachinings { get; init; } = Array.Empty<Machining>();
    public IReadOnlyList<Machining> UserTextMachinings { get; init; } = Array.Empty<Machining>();
    public IReadOnlyList<Machining> CombinedMachinings { get; init; } = Array.Empty<Machining>();
}
