using System;
using System.Collections.Generic;
using System.Globalization;
using Rhino;
using RhinoCNCExporter.Core.Blocks;
using RhinoCNCExporter.Core.Models;

namespace RhinoCNCExporter.Services;

/// <summary>
/// Provides and manages default operation values per operation type and machine profile.
/// Delegates to <see cref="OperationDefaultsBase"/> for built-in defaults and adds
/// document UserText persistence for user overrides.
/// When a new operation is created, these defaults pre-fill the dialog fields
/// instead of leaving them blank.
/// </summary>
public static class OperationDefaults
{
    // Document UserText prefix for stored defaults
    private const string PREFIX = "CNC_Defaults_";

    /// <summary>
    /// Gets the default values for an operation type, considering the machine profile.
    /// First checks document-stored overrides, then falls back to machine-profile defaults.
    /// </summary>
    /// <param name="operationType">Operation type (Contour, Pocket, Drill, Groove).</param>
    /// <param name="machineKey">Machine profile key (xilog, biesse, maestrocadt).</param>
    /// <returns>Default values with any user overrides applied.</returns>
    public static OperationDefaultValues GetDefaults(string operationType, string machineKey)
    {
        var doc = RhinoDoc.ActiveDoc;
        var defaults = OperationDefaultsBase.GetMachineProfileDefaults(operationType, machineKey);

        if (doc == null) return defaults;

        // Try to load user overrides from document UserText
        var typeKey = operationType.ToUpperInvariant();

        defaults.Depth = LoadDouble(doc, $"{typeKey}_Depth", defaults.Depth);
        defaults.Feedrate = LoadDouble(doc, $"{typeKey}_Feedrate", defaults.Feedrate);
        defaults.Strategy = LoadString(doc, $"{typeKey}_Strategy", defaults.Strategy);
        defaults.ToolName = LoadString(doc, $"{typeKey}_Tool", defaults.ToolName);
        defaults.Stepover = LoadDouble(doc, $"{typeKey}_Stepover", defaults.Stepover);
        defaults.Width = LoadDouble(doc, $"{typeKey}_Width", defaults.Width);
        defaults.Diameter = LoadDouble(doc, $"{typeKey}_Diameter", defaults.Diameter);
        defaults.PeckDepth = LoadDouble(doc, $"{typeKey}_PeckDepth", defaults.PeckDepth);
        defaults.Peck = LoadBool(doc, $"{typeKey}_Peck", defaults.Peck);
        defaults.RampEntry = LoadString(doc, $"{typeKey}_RampEntry", defaults.RampEntry);

        return defaults;
    }

    /// <summary>
    /// Saves user-defined default values to document UserText.
    /// </summary>
    public static void SaveDefaults(string operationType, OperationDefaultValues values)
    {
        var doc = RhinoDoc.ActiveDoc;
        if (doc == null) return;

        var typeKey = operationType.ToUpperInvariant();

        SaveDouble(doc, $"{typeKey}_Depth", values.Depth);
        SaveDouble(doc, $"{typeKey}_Feedrate", values.Feedrate);
        SaveString(doc, $"{typeKey}_Strategy", values.Strategy);
        SaveString(doc, $"{typeKey}_Tool", values.ToolName);
        SaveDouble(doc, $"{typeKey}_Stepover", values.Stepover);
        SaveDouble(doc, $"{typeKey}_Width", values.Width);
        SaveDouble(doc, $"{typeKey}_Diameter", values.Diameter);
        SaveDouble(doc, $"{typeKey}_PeckDepth", values.PeckDepth);
        SaveBool(doc, $"{typeKey}_Peck", values.Peck);
        SaveString(doc, $"{typeKey}_RampEntry", values.RampEntry);
    }

    /// <summary>
    /// Applies default values to a parameters dictionary (only fills empty/missing values).
    /// </summary>
    public static void ApplyDefaults(
        Dictionary<string, object> parameters,
        string operationType,
        string machineKey)
    {
        var defaults = GetDefaults(operationType, machineKey);

        SetIfMissing(parameters, CncOperationSchema.CNC_DEPTH, defaults.Depth);
        SetIfMissing(parameters, CncOperationSchema.CNC_FEEDRATE, defaults.Feedrate);

        switch (operationType.ToUpperInvariant())
        {
            case "CONTOUR":
                SetIfMissing(parameters, CncOperationSchema.CNC_STRATEGY, defaults.Strategy);
                break;
            case "POCKET":
                SetIfMissing(parameters, CncOperationSchema.CNC_STRATEGY, defaults.Strategy);
                SetIfMissing(parameters, CncOperationSchema.CNC_STEPOVER, defaults.Stepover);
                SetIfMissing(parameters, CncOperationSchema.CNC_RAMP_ENTRY, defaults.RampEntry);
                break;
            case "DRILL":
                SetIfMissing(parameters, CncOperationSchema.CNC_DIAMETER, defaults.Diameter);
                SetIfMissing(parameters, CncOperationSchema.CNC_PECK, defaults.Peck);
                SetIfMissing(parameters, CncOperationSchema.CNC_PECK_DEPTH, defaults.PeckDepth);
                break;
            case "GROOVE":
                SetIfMissing(parameters, CncOperationSchema.CNC_STRATEGY, defaults.Strategy);
                SetIfMissing(parameters, CncOperationSchema.CNC_WIDTH, defaults.Width);
                break;
        }
    }

    private static void SetIfMissing(Dictionary<string, object> parameters, string key, object value)
    {
        if (!parameters.ContainsKey(key))
            parameters[key] = value;
    }

    #region Document UserText Helpers

    private static double LoadDouble(RhinoDoc doc, string suffix, double fallback)
    {
        var value = doc.Strings.GetValue($"{PREFIX}{suffix}");
        if (!string.IsNullOrEmpty(value) &&
            double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }
        return fallback;
    }

    private static string LoadString(RhinoDoc doc, string suffix, string? fallback)
    {
        var value = doc.Strings.GetValue($"{PREFIX}{suffix}");
        return !string.IsNullOrEmpty(value) ? value : (fallback ?? string.Empty);
    }

    private static bool LoadBool(RhinoDoc doc, string suffix, bool fallback)
    {
        var value = doc.Strings.GetValue($"{PREFIX}{suffix}");
        if (!string.IsNullOrEmpty(value) && bool.TryParse(value, out var result))
            return result;
        return fallback;
    }

    private static void SaveDouble(RhinoDoc doc, string suffix, double value)
    {
        doc.Strings.SetString($"{PREFIX}{suffix}", value.ToString("F3", CultureInfo.InvariantCulture));
    }

    private static void SaveString(RhinoDoc doc, string suffix, string? value)
    {
        if (!string.IsNullOrEmpty(value))
            doc.Strings.SetString($"{PREFIX}{suffix}", value);
    }

    private static void SaveBool(RhinoDoc doc, string suffix, bool value)
    {
        doc.Strings.SetString($"{PREFIX}{suffix}", value.ToString().ToLowerInvariant());
    }

    #endregion
}
