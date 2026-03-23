namespace RhinoCNCExporter.Core.Models;

/// <summary>
/// Export pipeline mode.
/// </summary>
public enum ExportMode
{
    /// <summary>Automatic detection: 3D if solids found, else legacy.</summary>
    Auto,

    /// <summary>Legacy 2D layer-based export (CUT_*, DRILL_* layers).</summary>
    Legacy,

    /// <summary>Full 3D pipeline: plate detection → block scanning → per-plate export.</summary>
    ThreeD
}
