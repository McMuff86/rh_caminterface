using System.Globalization;
using Rhino;
using Rhino.DocObjects;
using RhinoCNCExporter.Core.Blocks;

namespace RhinoCNCExporter.Services;

/// <summary>
/// Service for managing CNC operations on Rhino objects using UserText.
/// Plugin-specific implementation with RhinoCommon dependencies.
/// </summary>
public static class CncOperationService
{
    /// <summary>
    /// Sets CNC operation data on a Rhino object as UserText.
    /// </summary>
    public static void SetOperation(RhinoObject rhinoObject, string type, Dictionary<string, object> parameters)
    {
        if (rhinoObject == null) throw new ArgumentNullException(nameof(rhinoObject));
        if (string.IsNullOrEmpty(type)) throw new ArgumentException("Type cannot be empty", nameof(type));

        rhinoObject.Attributes.SetUserString(CncOperationSchema.CNC_TYPE, type);

        foreach (var param in parameters)
        {
            var value = ConvertToString(param.Value);
            if (value != null)
            {
                rhinoObject.Attributes.SetUserString(param.Key, value);
            }
        }

        rhinoObject.CommitChanges();
    }

    /// <summary>
    /// Gets CNC operation data from a Rhino object's UserText.
    /// </summary>
    public static MachiningOperation? GetOperation(RhinoObject rhinoObject)
    {
        if (rhinoObject == null) return null;

        var type = rhinoObject.Attributes.GetUserString(CncOperationSchema.CNC_TYPE);
        if (string.IsNullOrEmpty(type)) return null;

        var parameters = new Dictionary<string, string>();
        var userStrings = rhinoObject.Attributes.GetUserStrings();
        
        foreach (string key in userStrings.AllKeys)
        {
            if (key.StartsWith("CNC_", StringComparison.OrdinalIgnoreCase))
            {
                parameters[key] = userStrings[key];
            }
        }

        return new MachiningOperation(type, parameters);
    }

    /// <summary>
    /// Removes all CNC operation UserText from a Rhino object.
    /// </summary>
    public static void RemoveOperation(RhinoObject rhinoObject)
    {
        if (rhinoObject == null) return;

        var userStrings = rhinoObject.Attributes.GetUserStrings();
        var keysToRemove = new List<string>();

        foreach (string key in userStrings.AllKeys)
        {
            if (key.StartsWith("CNC_", StringComparison.OrdinalIgnoreCase))
            {
                keysToRemove.Add(key);
            }
        }

        foreach (var key in keysToRemove)
        {
            rhinoObject.Attributes.DeleteUserString(key);
        }

        if (keysToRemove.Count > 0)
        {
            rhinoObject.CommitChanges();
        }
    }

    /// <summary>
    /// Gets all objects in the document that have CNC operations.
    /// </summary>
    public static IEnumerable<RhinoObject> GetAllOperationsInDocument(RhinoDoc doc)
    {
        return doc.Objects
            .Where(obj => !string.IsNullOrEmpty(obj.Attributes.GetUserString(CncOperationSchema.CNC_TYPE)));
    }

    /// <summary>
    /// Sets visual feedback color for an operation type.
    /// </summary>
    public static void SetOperationColor(RhinoObject rhinoObject, string operationType)
    {
        var color = operationType?.ToUpperInvariant() switch
        {
            CncOperationSchema.TYPE_CONTOUR => System.Drawing.Color.Red,
            CncOperationSchema.TYPE_POCKET => System.Drawing.Color.Blue,
            CncOperationSchema.TYPE_DRILL => System.Drawing.Color.Yellow,
            CncOperationSchema.TYPE_GROOVE => System.Drawing.Color.Green,
            _ => rhinoObject.Attributes.ObjectColor
        };

        rhinoObject.Attributes.ObjectColor = color;
        rhinoObject.Attributes.ColorSource = ObjectColorSource.ColorFromObject;
        rhinoObject.CommitChanges();
    }

    /// <summary>
    /// Restores the default layer color for an object.
    /// </summary>
    public static void RestoreDefaultColor(RhinoObject rhinoObject)
    {
        rhinoObject.Attributes.ColorSource = ObjectColorSource.ColorFromLayer;
        rhinoObject.CommitChanges();
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