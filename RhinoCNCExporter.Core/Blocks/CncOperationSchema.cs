using System.Globalization;
using RhinoCNCExporter.Core.Models;

namespace RhinoCNCExporter.Core.Blocks;

/// <summary>
/// Schema for CNC operations stored as UserText on Rhino objects.
/// Used by interactive CAM commands (CNCAddContour, CNCAddDrill, etc.)
/// to tag geometry with machining operations.
/// Core version without RhinoCommon dependencies.
/// </summary>
public static class CncOperationSchema
{
    // --- UserText Keys ---
    public const string CNC_TYPE = "CNC_Type";
    public const string CNC_TOOL = "CNC_Tool";
    public const string CNC_DEPTH = "CNC_Depth";
    public const string CNC_DIAMETER = "CNC_Diameter";
    public const string CNC_WIDTH = "CNC_Width";
    public const string CNC_STRATEGY = "CNC_Strategy";
    public const string CNC_FEEDRATE = "CNC_Feedrate";
    public const string CNC_STEPOVER = "CNC_Stepover";
    public const string CNC_PECK = "CNC_Peck";
    public const string CNC_PECK_DEPTH = "CNC_PeckDepth";
    public const string CNC_RAMP_ENTRY = "CNC_RampEntry";
    public const string CNC_GROUP_INDEX = "CNC_GroupIndex";

    // --- Operation Types ---
    public const string TYPE_CONTOUR = "Contour";
    public const string TYPE_POCKET = "Pocket";
    public const string TYPE_DRILL = "Drill";
    public const string TYPE_GROOVE = "Groove";

    // --- Strategies ---
    public const string STRATEGY_ROUGH = "Rough";
    public const string STRATEGY_FINISH = "Finish";
    public const string STRATEGY_BOTH = "Both";

    // --- Ramp Entry Types ---
    public const string RAMP_STRAIGHT = "Straight";
    public const string RAMP_SPIRAL = "Spiral";
    public const string RAMP_PROFILE = "Profile";

    /// <summary>
    /// Validates that required parameters are present for the given operation type.
    /// </summary>
    public static (bool IsValid, string? Error) ValidateOperation(string type, Dictionary<string, string> parameters)
    {
        switch (type?.ToUpperInvariant())
        {
            case "CONTOUR":
                if (!parameters.ContainsKey(CNC_TOOL))
                    return (false, "Contour operation requires CNC_Tool");
                if (!parameters.ContainsKey(CNC_DEPTH))
                    return (false, "Contour operation requires CNC_Depth");
                break;

            case "POCKET":
                if (!parameters.ContainsKey(CNC_TOOL))
                    return (false, "Pocket operation requires CNC_Tool");
                if (!parameters.ContainsKey(CNC_DEPTH))
                    return (false, "Pocket operation requires CNC_Depth");
                break;

            case "DRILL":
                if (!parameters.ContainsKey(CNC_DIAMETER))
                    return (false, "Drill operation requires CNC_Diameter");
                if (!parameters.ContainsKey(CNC_DEPTH))
                    return (false, "Drill operation requires CNC_Depth");
                break;

            case "GROOVE":
                if (!parameters.ContainsKey(CNC_TOOL))
                    return (false, "Groove operation requires CNC_Tool");
                if (!parameters.ContainsKey(CNC_WIDTH))
                    return (false, "Groove operation requires CNC_Width");
                if (!parameters.ContainsKey(CNC_DEPTH))
                    return (false, "Groove operation requires CNC_Depth");
                break;

            default:
                return (false, $"Unknown operation type: {type}");
        }

        return (true, null);
    }

    /// <summary>
    /// Converts various parameter types to string for UserText storage.
    /// </summary>
    private static string? ConvertToString(object value)
    {
        return value switch
        {
            null => null,
            string s => s,
            double d => d.ToString("F3", CultureInfo.InvariantCulture),
            float f => f.ToString("F3", CultureInfo.InvariantCulture),
            int i => i.ToString(CultureInfo.InvariantCulture),
            bool b => b.ToString().ToLowerInvariant(),
            _ => value.ToString()
        };
    }
}

/// <summary>
/// Represents a CNC machining operation parsed from UserText.
/// </summary>
public record MachiningOperation(string Type, IReadOnlyDictionary<string, string> Parameters)
{
    public string? Tool => Parameters.GetValueOrDefault(CncOperationSchema.CNC_TOOL);
    public double? Depth => TryGetDouble(CncOperationSchema.CNC_DEPTH);
    public double? Diameter => TryGetDouble(CncOperationSchema.CNC_DIAMETER);
    public double? Width => TryGetDouble(CncOperationSchema.CNC_WIDTH);
    public string? Strategy => Parameters.GetValueOrDefault(CncOperationSchema.CNC_STRATEGY);
    public double? Feedrate => TryGetDouble(CncOperationSchema.CNC_FEEDRATE);
    public double? Stepover => TryGetDouble(CncOperationSchema.CNC_STEPOVER);
    public bool? Peck => TryGetBool(CncOperationSchema.CNC_PECK);
    public double? PeckDepth => TryGetDouble(CncOperationSchema.CNC_PECK_DEPTH);
    public string? RampEntry => Parameters.GetValueOrDefault(CncOperationSchema.CNC_RAMP_ENTRY);

    private double? TryGetDouble(string key)
    {
        if (Parameters.TryGetValue(key, out var value) &&
            double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }
        return null;
    }

    private bool? TryGetBool(string key)
    {
        if (Parameters.TryGetValue(key, out var value) &&
            bool.TryParse(value, out var result))
        {
            return result;
        }
        return null;
    }
}