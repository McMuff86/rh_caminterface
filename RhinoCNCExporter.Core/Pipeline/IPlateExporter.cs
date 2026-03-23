using RhinoCNCExporter.Core.Models;

namespace RhinoCNCExporter.Core.Pipeline;

/// <summary>
/// Orchestrates the export of a single plate.
/// </summary>
public interface IPlateExporter
{
    /// <summary>
    /// Export a single plate to a CNC file. Returns the generated file path.
    /// </summary>
    string ExportPlate(Plate plate, string outputDirectory, MachineFormat format);
}
