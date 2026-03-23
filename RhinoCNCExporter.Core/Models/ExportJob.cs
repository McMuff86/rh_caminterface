namespace RhinoCNCExporter.Core.Models;

/// <summary>
/// Represents a complete export job: one or more plates to be exported.
/// </summary>
public sealed record ExportJob
{
    /// <summary>Plates to export (each plate → one CNC file).</summary>
    public required IReadOnlyList<Plate> Plates { get; init; }

    /// <summary>Target machine format.</summary>
    public required MachineFormat Format { get; init; }

    /// <summary>Output directory for generated CNC files.</summary>
    public required string OutputDirectory { get; init; }

    /// <summary>Machine profile name to use.</summary>
    public required string ProfileName { get; init; }

    /// <summary>Whether to use legacy layer scanning (Phase 1 compat).</summary>
    public bool UseLegacyLayers { get; init; } = true;

    /// <summary>Whether to use block detection (Phase 2+).</summary>
    public bool UseBlockDetection { get; init; } = false;
}
