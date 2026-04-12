using System.Globalization;
using System.Linq;
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

        var hasEnabledFlag = parameters.Keys.Any(key =>
            key.Equals(CncOperationSchema.CNC_ENABLED, StringComparison.OrdinalIgnoreCase));

        foreach (var param in parameters)
        {
            var value = ConvertToString(param.Value);
            if (value != null)
            {
                rhinoObject.Attributes.SetUserString(param.Key, value);
            }
        }

        if (!hasEnabledFlag && string.IsNullOrEmpty(rhinoObject.Attributes.GetUserString(CncOperationSchema.CNC_ENABLED)))
        {
            rhinoObject.Attributes.SetUserString(CncOperationSchema.CNC_ENABLED, bool.TrueString.ToLowerInvariant());
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
        if (userStrings == null)
            return new MachiningOperation(type, parameters);

        foreach (var key in userStrings.AllKeys.OfType<string>())
        {
            if (key.StartsWith("CNC_", StringComparison.OrdinalIgnoreCase))
            {
                parameters[key] = userStrings[key] ?? string.Empty;
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
        if (userStrings == null)
            return;

        var keysToRemove = new List<string>();

        foreach (var key in userStrings.AllKeys.OfType<string>())
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
        if (doc == null) return Enumerable.Empty<RhinoObject>();

        return doc.Objects
            .Where(obj => obj != null && !string.IsNullOrEmpty(obj.Attributes.GetUserString(CncOperationSchema.CNC_TYPE)));
    }

    /// <summary>
    /// Gets all enabled CNC operation objects in the document.
    /// </summary>
    public static IEnumerable<RhinoObject> GetEnabledOperationsInDocument(RhinoDoc doc)
    {
        return GetAllOperationsInDocument(doc)
            .Where(IsOperationEnabled);
    }

    /// <summary>
    /// Gets the color associated with an operation type (for use in edge extraction etc.).
    /// </summary>
    public static System.Drawing.Color GetOperationColor(string operationType)
    {
        return (operationType ?? string.Empty).ToUpperInvariant() switch
        {
            "CONTOUR" => System.Drawing.Color.Red,
            "POCKET" => System.Drawing.Color.Blue,
            "DRILL" => System.Drawing.Color.Yellow,
            "GROOVE" => System.Drawing.Color.Green,
            _ => System.Drawing.Color.Gray
        };
    }

    /// <summary>
    /// Sets visual feedback color for an operation type on a Rhino object.
    /// </summary>
    public static void SetOperationColor(RhinoObject rhinoObject, string operationType)
    {
        rhinoObject.Attributes.ObjectColor = GetOperationColor(operationType);
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
    /// Returns whether the CNC operation on the object is enabled.
    /// Missing flags default to true for backwards compatibility.
    /// </summary>
    public static bool IsOperationEnabled(RhinoObject rhinoObject)
    {
        if (rhinoObject == null) return false;

        var value = rhinoObject.Attributes.GetUserString(CncOperationSchema.CNC_ENABLED);
        return string.IsNullOrWhiteSpace(value) || !bool.TryParse(value, out var enabled) || enabled;
    }

    /// <summary>
    /// Enables or disables the CNC operation on an object without removing it.
    /// </summary>
    public static void SetOperationEnabled(RhinoObject rhinoObject, bool enabled)
    {
        if (rhinoObject == null) throw new ArgumentNullException(nameof(rhinoObject));

        rhinoObject.Attributes.SetUserString(CncOperationSchema.CNC_ENABLED, enabled.ToString().ToLowerInvariant());
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

// MachiningOperation record is defined in RhinoCNCExporter.Core.Blocks.CncOperationSchema