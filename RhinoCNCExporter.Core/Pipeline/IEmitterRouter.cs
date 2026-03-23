using RhinoCNCExporter.Core.Models;

namespace RhinoCNCExporter.Core.Pipeline;

/// <summary>
/// Routes an ExportJob to the correct emitter and generates CNC programs.
/// </summary>
public interface IEmitterRouter
{
    /// <summary>
    /// Generate the complete CNC program string for one plate.
    /// </summary>
    string GenerateProgram(Plate plate);
}
