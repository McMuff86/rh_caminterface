using RhinoCNCExporter.Core.Models;

namespace RhinoCNCExporter.Core.Blocks;

/// <summary>
/// Parses a Dictionary&lt;string,string&gt; (UserText from Rhino block instance)
/// into a FittingBlock record. Validates required fields.
/// Pure logic — no RhinoCommon dependency.
/// </summary>
public static class CncUserTextParser
{
    /// <summary>
    /// Parse UserText attributes into a FittingBlock.
    /// Returns null if CNC_Type is missing or invalid.
    /// </summary>
    /// <param name="blockName">Block definition name.</param>
    /// <param name="allUserText">All UserText key-value pairs from the block instance.</param>
    /// <param name="insertionPoint">Block insertion point in world coordinates.</param>
    /// <param name="rotation">Block rotation in degrees.</param>
    /// <param name="layerName">Layer the block instance lives on (optional).</param>
    /// <param name="error">Error message if parsing fails.</param>
    public static FittingBlock? Parse(
        string blockName,
        IReadOnlyDictionary<string, string> allUserText,
        (double X, double Y, double Z) insertionPoint,
        double rotation,
        string? layerName,
        out string? error)
    {
        // Extract only CNC_* keys
        var cncAttrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in allUserText)
        {
            if (kv.Key.StartsWith("CNC_", StringComparison.OrdinalIgnoreCase))
                cncAttrs[kv.Key] = kv.Value;
        }

        // Must have at least CNC_Type
        if (cncAttrs.Count == 0 || !cncAttrs.ContainsKey(BlockUserTextSchema.CNC_TYPE))
        {
            error = "No CNC_Type attribute found";
            return null;
        }

        // Validate
        var (isValid, validationError) = BlockUserTextSchema.Validate(cncAttrs);
        if (!isValid)
        {
            error = validationError;
            return null;
        }

        error = null;
        return new FittingBlock
        {
            BlockName = blockName,
            CncType = cncAttrs[BlockUserTextSchema.CNC_TYPE],
            InsertionPoint = insertionPoint,
            Rotation = rotation,
            CncAttributes = cncAttrs,
            LayerName = layerName
        };
    }
}
