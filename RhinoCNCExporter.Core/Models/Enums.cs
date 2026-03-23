namespace RhinoCNCExporter.Core.Models;

/// <summary>
/// CNC machining type — maps to CNC_Type UserText values.
/// </summary>
public enum MachiningType
{
    Drill,
    DrillPattern,
    Routing,
    RoutingWithArcs,
    Pocket,
    GrooveRnt,
    Macro,
    HorizontalDrill
}

/// <summary>
/// Side of the plate where the machining occurs.
/// </summary>
public enum MachiningSide
{
    Top,
    Bottom,
    Left,
    Right,
    Front,
    Back
}

/// <summary>
/// Target CNC machine format.
/// </summary>
public enum MachineFormat
{
    /// <summary>SCM Maestro (.xcs)</summary>
    Xilog,
    /// <summary>Biesse bSolid (.cix)</summary>
    Biesse,
    /// <summary>Homag woodWOP (.mpr)</summary>
    Homag
}

/// <summary>
/// How this machining was sourced.
/// </summary>
public enum MachiningSource
{
    /// <summary>From 2D layer conventions (Phase 1 legacy).</summary>
    LegacyLayer,
    /// <summary>From CNC_* block detection (Phase 2+).</summary>
    BlockDetection,
    /// <summary>Manually assigned by user.</summary>
    Manual
}

/// <summary>
/// How this plate was detected.
/// </summary>
public enum PlateSource
{
    /// <summary>Detected from WK_PIECE layer (Phase 1 legacy).</summary>
    LegacyLayer,
    /// <summary>Detected as a Solid/Extrusion on a named layer (Phase 3).</summary>
    SolidDetection,
    /// <summary>Manually assigned by user.</summary>
    Manual
}
