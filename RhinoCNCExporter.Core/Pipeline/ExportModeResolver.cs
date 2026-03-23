using RhinoCNCExporter.Core.Models;

namespace RhinoCNCExporter.Core.Pipeline;

/// <summary>
/// Resolves the effective export mode from the requested mode and detected document capabilities.
/// </summary>
public static class ExportModeResolver
{
    public static ExportModeDecision Decide(DocumentCapabilities capabilities, ExportMode requestedMode)
    {
        return requestedMode switch
        {
            ExportMode.Automatic => DecideAutomatic(capabilities),
            ExportMode.LegacyOnly => CreateLegacyDecision(capabilities, requestedMode),
            ExportMode.MultiPlate3D => CreateMultiPlateDecision(capabilities, requestedMode),
            _ => DecideAutomatic(capabilities)
        };
    }

    private static ExportModeDecision DecideAutomatic(DocumentCapabilities capabilities)
    {
        if (capabilities.Has3DPlates && capabilities.PlateCount > 0)
        {
            return new ExportModeDecision
            {
                RequestedMode = ExportMode.Automatic,
                ResolvedMode = ExportMode.MultiPlate3D,
                IsExecutable = true,
                Reason = capabilities.HasBlocks
                    ? "3D-Platten und CNC-Blöcke erkannt. Multi-Platte-Export ist der passende Pfad."
                    : "3D-Platten erkannt. Multi-Platte-Export ist der passende Pfad."
            };
        }

        return CreateLegacyDecision(capabilities, ExportMode.Automatic);
    }

    private static ExportModeDecision CreateLegacyDecision(
        DocumentCapabilities capabilities,
        ExportMode requestedMode)
    {
        if (capabilities.HasLegacyPiece)
        {
            return new ExportModeDecision
            {
                RequestedMode = requestedMode,
                ResolvedMode = ExportMode.LegacyOnly,
                IsExecutable = true,
                Reason = capabilities.HasBlocks
                    ? "WK_PIECE erkannt. Legacy-Export mit optionaler Block-Detection ist möglich."
                    : "WK_PIECE erkannt. Legacy-Export ist möglich."
            };
        }

        return new ExportModeDecision
        {
            RequestedMode = requestedMode,
            ResolvedMode = ExportMode.LegacyOnly,
            IsExecutable = false,
            Reason = capabilities.HasBlocks
                ? "CNC-Blöcke wurden erkannt, aber kein WK_PIECE. Der Legacy-Pfad kann nicht sauber exportieren."
                : "Kein WK_PIECE erkannt. Der Legacy-Pfad ist nicht ausführbar."
        };
    }

    private static ExportModeDecision CreateMultiPlateDecision(
        DocumentCapabilities capabilities,
        ExportMode requestedMode)
    {
        if (capabilities.Has3DPlates && capabilities.PlateCount > 0)
        {
            return new ExportModeDecision
            {
                RequestedMode = requestedMode,
                ResolvedMode = ExportMode.MultiPlate3D,
                IsExecutable = true,
                Reason = $"3D-Pipeline ausführbar: {capabilities.PlateCount} Platte(n) erkannt."
            };
        }

        return new ExportModeDecision
        {
            RequestedMode = requestedMode,
            ResolvedMode = ExportMode.MultiPlate3D,
            IsExecutable = false,
            Reason = "Keine 3D-Platten erkannt. Der Multi-Platte-Modus ist nicht ausführbar."
        };
    }
}
