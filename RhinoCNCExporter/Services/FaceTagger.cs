using System;
using System.Collections.Generic;
using System.Linq;
using Rhino;
using Rhino.Geometry;
using Rhino.DocObjects;

namespace RhinoCNCExporter.Services;

/// <summary>
/// Utility for tagging BrepFaces with CNC_* UserText using object-level storage with face index prefixes.
/// Since BrepFace has no UserText, we store tags on the RhinoObject with keys like "CNC_Face_{faceIndex}_Type".
/// </summary>
public static class FaceTagger
{
    /// <summary>
    /// Set CNC_* tags for specific faces of a RhinoObject.
    /// Uses face indices as key prefixes since BrepFace has no UserText capability.
    /// </summary>
    /// <param name="doc">Rhino document</param>
    /// <param name="objectId">Object containing the faces</param>
    /// <param name="faceIndices">Face indices to tag (from Brep.Faces array)</param>
    /// <param name="tags">Dictionary of CNC_* key-value pairs</param>
    /// <returns>True if successful</returns>
    public static bool TagFaces(RhinoDoc doc, Guid objectId, IEnumerable<int> faceIndices, Dictionary<string, string> tags)
    {
        var obj = doc.Objects.FindId(objectId);
        if (obj == null)
        {
            RhinoApp.WriteLine($"[FaceTagger] Object {objectId} not found");
            return false;
        }

        try
        {
            foreach (int faceIndex in faceIndices)
            {
                foreach (var kvp in tags)
                {
                    string prefixedKey = $"CNC_Face_{faceIndex}_{kvp.Key}";
                    obj.Attributes.SetUserString(prefixedKey, kvp.Value);
                }
            }

            // Commit attribute changes
            obj.CommitChanges();
            doc.Views.Redraw();
            
            RhinoApp.WriteLine($"[FaceTagger] Tagged {faceIndices.Count()} faces with {tags.Count} attributes");
            return true;
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"[FaceTagger] Error tagging faces: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Read CNC_* tags for a specific face of a RhinoObject.
    /// </summary>
    /// <param name="obj">Rhino object</param>
    /// <param name="faceIndex">Face index</param>
    /// <returns>Dictionary of CNC_* tags (without the CNC_Face_{index}_ prefix)</returns>
    public static Dictionary<string, string> ReadTags(RhinoObject obj, int faceIndex)
    {
        var tags = new Dictionary<string, string>();
        string prefix = $"CNC_Face_{faceIndex}_";

        try
        {
            var allUserStrings = obj.Attributes.GetUserStrings();
            if (allUserStrings == null) return tags;

            foreach (string? key in allUserStrings.AllKeys)
            {
                if (key == null) continue;
                if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    string cncKey = key.Substring(prefix.Length);
                    string? value = allUserStrings[key];
                    if (!string.IsNullOrEmpty(value))
                    {
                        tags[cncKey] = value;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"[FaceTagger] Error reading tags for face {faceIndex}: {ex.Message}");
        }

        return tags;
    }

    /// <summary>
    /// Find new faces created after a Boolean operation by comparing face counts and geometries.
    /// This is a heuristic approach since Rhino doesn't provide direct face tracking through Boolean operations.
    /// </summary>
    /// <param name="before">Original Brep before Boolean</param>
    /// <param name="after">Result Brep after Boolean</param>
    /// <param name="tolerance">Geometric tolerance for face comparison</param>
    /// <returns>List of face indices in the 'after' Brep that are likely new</returns>
    public static List<int> FindNewFaces(Brep before, Brep after, double tolerance = 0.01)
    {
        var newFaceIndices = new List<int>();

        if (before == null || after == null) 
            return newFaceIndices;

        try
        {
            // Simple heuristic: faces in 'after' that don't have a similar face in 'before'
            for (int i = 0; i < after.Faces.Count; i++)
            {
                var afterFace = after.Faces[i];
                bool foundSimilar = false;

                // Get surface area and centroid as comparison criteria
                var afterArea = AreaMassProperties.Compute(afterFace.ToBrep());
                if (afterArea == null) continue;

                var afterCentroid = afterArea.Centroid;
                var afterAreaValue = afterArea.Area;

                // Compare against all faces in the original Brep
                for (int j = 0; j < before.Faces.Count; j++)
                {
                    var beforeFace = before.Faces[j];
                    var beforeArea = AreaMassProperties.Compute(beforeFace.ToBrep());
                    if (beforeArea == null) continue;

                    var beforeCentroid = beforeArea.Centroid;
                    var beforeAreaValue = beforeArea.Area;

                    // Check if centroids are close and areas are similar
                    double centroidDistance = afterCentroid.DistanceTo(beforeCentroid);
                    double areaDifference = Math.Abs(afterAreaValue - beforeAreaValue) / Math.Max(afterAreaValue, beforeAreaValue);

                    if (centroidDistance < tolerance && areaDifference < 0.1) // 10% area tolerance
                    {
                        foundSimilar = true;
                        break;
                    }
                }

                if (!foundSimilar)
                {
                    newFaceIndices.Add(i);
                }
            }

            RhinoApp.WriteLine($"[FaceTagger] Found {newFaceIndices.Count} new faces after Boolean operation");
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"[FaceTagger] Error finding new faces: {ex.Message}");
        }

        return newFaceIndices;
    }

    /// <summary>
    /// Remove all CNC_* tags for a specific face.
    /// </summary>
    /// <param name="obj">Rhino object</param>
    /// <param name="faceIndex">Face index</param>
    /// <returns>True if successful</returns>
    public static bool ClearTags(RhinoObject obj, int faceIndex)
    {
        try
        {
            string prefix = $"CNC_Face_{faceIndex}_";
            var userStrings = obj.Attributes.GetUserStrings();
            if (userStrings == null) return true;

            var keysToRemove = userStrings.AllKeys
                .Where(key => key != null && key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(key => key!)
                .ToList();

            foreach (string key in keysToRemove)
            {
                obj.Attributes.DeleteUserString(key);
            }

            if (keysToRemove.Count > 0)
            {
                obj.CommitChanges();
                RhinoApp.WriteLine($"[FaceTagger] Cleared {keysToRemove.Count} tags from face {faceIndex}");
            }

            return true;
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"[FaceTagger] Error clearing tags for face {faceIndex}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Get all tagged face indices for a RhinoObject.
    /// </summary>
    /// <param name="obj">Rhino object</param>
    /// <returns>List of face indices that have CNC_* tags</returns>
    public static List<int> GetTaggedFaceIndices(RhinoObject obj)
    {
        var faceIndices = new HashSet<int>();

        try
        {
            var userStrings = obj.Attributes.GetUserStrings();
            if (userStrings == null) return faceIndices.ToList();

            foreach (string? key in userStrings.AllKeys)
            {
                if (key != null && key.StartsWith("CNC_Face_", StringComparison.OrdinalIgnoreCase))
                {
                    // Extract face index from key like "CNC_Face_3_Type"
                    var parts = key.Split('_');
                    if (parts.Length >= 3 && int.TryParse(parts[2], out int faceIndex))
                    {
                        faceIndices.Add(faceIndex);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"[FaceTagger] Error getting tagged face indices: {ex.Message}");
        }

        return faceIndices.ToList();
    }
}