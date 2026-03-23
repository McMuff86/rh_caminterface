using System;
using System.Collections.Generic;
using System.Linq;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using RhinoCNCExporter.Core.Blocks;
using RhinoCNCExporter.Core.Models;

namespace RhinoCNCExporter.BlockScanning;

/// <summary>
/// Scans a Rhino document for Block-Inserts that have CNC_* UserText attributes.
/// Converts them to FittingBlock DTOs (pure data, no Rhino references retained).
/// DEPENDS ON: RhinoCommon (InstanceReferenceGeometry, UserText)
/// </summary>
public class BlockScanner
{
    /// <summary>
    /// Scan the entire document for block instances with CNC_Type UserText.
    /// Returns FittingBlock DTOs for all CNC-bearing block inserts.
    /// </summary>
    public IReadOnlyList<FittingBlock> ScanDocument(RhinoDoc doc)
    {
        if (doc == null) return Array.Empty<FittingBlock>();

        var results = new List<FittingBlock>();

        foreach (var rhinoObj in doc.Objects)
        {
            if (rhinoObj == null || rhinoObj.IsDeleted) continue;
            var block = TryParseBlockInsert(doc, rhinoObj);
            if (block != null)
                results.Add(block);
        }

        return results;
    }

    /// <summary>
    /// Scan only selected objects for CNC blocks.
    /// </summary>
    public IReadOnlyList<FittingBlock> ScanSelection(RhinoDoc doc)
    {
        if (doc == null) return Array.Empty<FittingBlock>();

        var results = new List<FittingBlock>();
        var selected = doc.Objects.GetSelectedObjects(includeLights: false, includeGrips: false);

        foreach (var rhinoObj in selected)
        {
            if (rhinoObj == null) continue;
            var block = TryParseBlockInsert(doc, rhinoObj);
            if (block != null)
                results.Add(block);
        }

        return results;
    }

    /// <summary>
    /// Try to parse a RhinoObject as a CNC block insert.
    /// Returns null if the object is not a block instance or has no CNC_Type.
    /// </summary>
    private static FittingBlock? TryParseBlockInsert(RhinoDoc doc, RhinoObject rhinoObj)
    {
        // Must be an instance reference (block insert)
        if (rhinoObj.Geometry is not InstanceReferenceGeometry instanceRef)
            return null;

        // Get block definition
        var idefIndex = instanceRef.ParentIdefIndex;
        if (idefIndex < 0 || idefIndex >= doc.InstanceDefinitions.Count)
            return null;

        var idef = doc.InstanceDefinitions[idefIndex];
        if (idef == null || idef.IsDeleted)
            return null;

        // Read all UserText from the instance (not the definition)
        var userText = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var keys = rhinoObj.Attributes.GetUserStrings();
        if (keys != null)
        {
            foreach (string? key in keys.AllKeys)
            {
                if (key == null) continue;
                var val = rhinoObj.Attributes.GetUserString(key);
                if (val != null)
                    userText[key] = val;
            }
        }

        // Also check definition-level UserText as fallback
        if (!userText.ContainsKey(BlockUserTextSchema.CNC_TYPE))
        {
            var defKeys = idef.GetUserStrings();
            if (defKeys != null)
            {
                foreach (string? key in defKeys.AllKeys)
                {
                    if (key == null) continue;
                    if (!userText.ContainsKey(key))
                    {
                        var val = idef.GetUserString(key);
                        if (val != null)
                            userText[key] = val;
                    }
                }
            }
        }

        // Must have CNC_Type
        if (!userText.ContainsKey(BlockUserTextSchema.CNC_TYPE))
            return null;

        // Extract transform info
        var xform = instanceRef.Xform;
        var insertionPoint = xform * Point3d.Origin;
        var rotation = ExtractRotationDegrees(xform);

        // Get layer name
        var layer = doc.Layers.FindIndex(rhinoObj.Attributes.LayerIndex);
        var layerName = layer?.FullPath ?? layer?.Name;

        // Use CncUserTextParser to create the FittingBlock
        var block = CncUserTextParser.Parse(
            idef.Name,
            userText,
            (insertionPoint.X, insertionPoint.Y, insertionPoint.Z),
            rotation,
            layerName,
            out _);

        return block;
    }

    /// <summary>
    /// Extract rotation angle in degrees from a transformation matrix.
    /// Only handles planar (Z-axis) rotation for Phase 2.
    /// </summary>
    private static double ExtractRotationDegrees(Transform xform)
    {
        // Extract rotation from the 2D components of the transform
        // Rotation around Z: atan2(M10, M00)
        double angleRad = Math.Atan2(xform.M10, xform.M00);
        double angleDeg = angleRad * (180.0 / Math.PI);

        // Normalize to 0-360
        if (angleDeg < 0) angleDeg += 360;

        // Snap to nearest valid orientation (0, 90, 180, 270)
        if (angleDeg < 45) return 0;
        if (angleDeg < 135) return 90;
        if (angleDeg < 225) return 180;
        if (angleDeg < 315) return 270;
        return 0;
    }
}
