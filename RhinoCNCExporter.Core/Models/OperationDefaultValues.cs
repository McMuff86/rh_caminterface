using RhinoCNCExporter.Core.Blocks;

namespace RhinoCNCExporter.Core.Models;

/// <summary>
/// Default values for a specific CNC operation type.
/// These can come from built-in machine profile defaults or be overridden
/// by user-saved values in document UserText.
/// </summary>
public class OperationDefaultValues
{
    /// <summary>Default machining depth in mm.</summary>
    public double Depth { get; set; }

    /// <summary>Default feedrate in mm/min.</summary>
    public double Feedrate { get; set; }

    /// <summary>Default strategy (Rough/Finish/Both).</summary>
    public string Strategy { get; set; } = CncOperationSchema.STRATEGY_FINISH;

    /// <summary>Default tool name from the tool library, or null if not set.</summary>
    public string? ToolName { get; set; }

    /// <summary>Default stepover percentage for pocket operations.</summary>
    public double Stepover { get; set; } = 50.0;

    /// <summary>Default groove width in mm.</summary>
    public double Width { get; set; }

    /// <summary>Default drill diameter in mm.</summary>
    public double Diameter { get; set; }

    /// <summary>Default peck depth for peck drilling in mm.</summary>
    public double PeckDepth { get; set; }

    /// <summary>Whether peck drilling is enabled by default.</summary>
    public bool Peck { get; set; }

    /// <summary>Default ramp entry type for pocket operations (Straight/Spiral/Profile).</summary>
    public string RampEntry { get; set; } = CncOperationSchema.RAMP_STRAIGHT;
}

/// <summary>
/// Pure-logic portion of operation defaults: machine-profile-specific default values.
/// This static class provides the built-in defaults without any RhinoCommon dependency.
/// The full <c>OperationDefaults</c> service in the plugin project adds document
/// UserText persistence on top of these base values.
/// </summary>
public static class OperationDefaultsBase
{
    /// <summary>
    /// Returns built-in defaults based on the machine profile key and operation type.
    /// SCM/xilog and Biesse have different typical feedrates, depths, and strategies.
    /// </summary>
    /// <param name="operationType">Operation type: Contour, Pocket, Drill, or Groove.</param>
    /// <param name="machineKey">Machine profile key (e.g., "xilog", "biesse", "maestrocadt").</param>
    /// <returns>Default values for the operation type on the given machine.</returns>
    public static OperationDefaultValues GetMachineProfileDefaults(string operationType, string machineKey)
    {
        var isScm = machineKey.Equals("xilog", StringComparison.OrdinalIgnoreCase)
                    || machineKey.Equals("maestrocadt", StringComparison.OrdinalIgnoreCase);

        return operationType.ToUpperInvariant() switch
        {
            "CONTOUR" => new OperationDefaultValues
            {
                Depth = isScm ? 19.0 : 18.0,
                Feedrate = isScm ? 3000.0 : 4000.0,
                Strategy = CncOperationSchema.STRATEGY_FINISH,
            },
            "POCKET" => new OperationDefaultValues
            {
                Depth = isScm ? 5.0 : 5.0,
                Feedrate = isScm ? 2000.0 : 2500.0,
                Strategy = CncOperationSchema.STRATEGY_ROUGH,
                Stepover = isScm ? 50.0 : 45.0,
                RampEntry = CncOperationSchema.RAMP_SPIRAL,
            },
            "DRILL" => new OperationDefaultValues
            {
                Depth = isScm ? 19.0 : 18.0,
                Feedrate = isScm ? 1500.0 : 1800.0,
                Diameter = 5.0,
                Peck = false,
                PeckDepth = 5.0,
            },
            "GROOVE" => new OperationDefaultValues
            {
                Depth = isScm ? 8.0 : 8.0,
                Feedrate = isScm ? 2500.0 : 3000.0,
                Strategy = CncOperationSchema.STRATEGY_FINISH,
                Width = 4.0,
            },
            _ => new OperationDefaultValues
            {
                Depth = 10.0,
                Feedrate = 2000.0,
            }
        };
    }

    /// <summary>
    /// Returns all known operation types.
    /// </summary>
    public static IReadOnlyList<string> AllOperationTypes => new[]
    {
        CncOperationSchema.TYPE_CONTOUR,
        CncOperationSchema.TYPE_POCKET,
        CncOperationSchema.TYPE_DRILL,
        CncOperationSchema.TYPE_GROOVE
    };
}
