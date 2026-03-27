using System;
using System.Collections.Generic;
using System.Linq;
using Rhino;
using Rhino.DocObjects;
using RhinoCNCExporter.Core.Blocks;

namespace RhinoCNCExporter.Services;

/// <summary>
/// Monitors Brep deletions and automatically removes orphaned edge curves
/// (curves on the CNC_EdgeCurves layer whose source Brep no longer exists).
/// Also provides a manual "clean all" method for the CamPanel button.
/// </summary>
public sealed class OrphanCurveCleanup : IDisposable
{
    private bool _subscribed;
    private bool _disposed;

    /// <summary>
    /// Subscribe to RhinoDoc.DeleteRhinoObject to auto-detect orphans when a Brep is deleted.
    /// Call once (e.g. from CamPanel constructor or plugin OnLoad).
    /// </summary>
    public void Subscribe()
    {
        if (_subscribed || _disposed) return;
        RhinoDoc.DeleteRhinoObject += OnDeleteRhinoObject;
        _subscribed = true;
    }

    /// <summary>
    /// Unsubscribe from events. Safe to call multiple times.
    /// </summary>
    public void Unsubscribe()
    {
        if (!_subscribed) return;
        RhinoDoc.DeleteRhinoObject -= OnDeleteRhinoObject;
        _subscribed = false;
    }

    /// <summary>
    /// Event handler: when any object is deleted, check if it's a Brep.
    /// If so, find and remove all orphaned edge curves that reference it.
    /// </summary>
    private void OnDeleteRhinoObject(object? sender, RhinoObjectEventArgs e)
    {
        if (e?.TheObject == null) return;

        // Only care about Brep deletions
        if (e.TheObject.Geometry is not Rhino.Geometry.Brep) return;

        var doc = e.TheObject.Document;
        if (doc == null) return;

        var deletedBrepId = e.TheObject.Id.ToString();

        // Use AsyncInvoke to defer the scan until after the delete transaction completes,
        // otherwise the objects table may still be mid-mutation.
        Eto.Forms.Application.Instance.AsyncInvoke(() =>
        {
            CleanupOrphansForBrep(doc, deletedBrepId);
        });
    }

    /// <summary>
    /// Removes orphaned edge curves (and their toolpath geometry) whose CNC_SourceBrep
    /// matches the given Brep GUID string.
    /// </summary>
    private static void CleanupOrphansForBrep(RhinoDoc doc, string brepGuidStr)
    {
        var orphans = FindOrphansBySourceBrep(doc, brepGuidStr);
        if (orphans.Count == 0) return;

        RemoveOrphans(doc, orphans);
        RhinoApp.WriteLine($"{orphans.Count} verwaiste CNC-Operationen entfernt");
        doc.Views.Redraw();
    }

    /// <summary>
    /// Finds all edge curve objects whose CNC_SourceBrep matches the given GUID string.
    /// </summary>
    private static List<RhinoObject> FindOrphansBySourceBrep(RhinoDoc doc, string brepGuidStr)
    {
        var result = new List<RhinoObject>();
        foreach (var obj in doc.Objects)
        {
            if (obj == null || obj.IsDeleted) continue;
            var sourceBrep = obj.Attributes.GetUserString(CncOperationSchema.CNC_SOURCE_BREP);
            if (string.IsNullOrEmpty(sourceBrep)) continue;

            if (sourceBrep.Equals(brepGuidStr, StringComparison.OrdinalIgnoreCase))
            {
                result.Add(obj);
            }
        }
        return result;
    }

    /// <summary>
    /// Manual cleanup: scans ALL edge curves in the document and removes any
    /// whose source Brep no longer exists. Returns the count of removed orphans.
    /// Call from the "🧹 Bereinigen" button in CamPanel.
    /// </summary>
    public static int CleanupAllOrphans(RhinoDoc doc)
    {
        if (doc == null) return 0;

        var orphans = new List<RhinoObject>();

        foreach (var obj in doc.Objects)
        {
            if (obj == null || obj.IsDeleted) continue;
            var sourceBrepStr = obj.Attributes.GetUserString(CncOperationSchema.CNC_SOURCE_BREP);
            if (string.IsNullOrEmpty(sourceBrepStr)) continue;

            // Check if the source Brep still exists
            if (!Guid.TryParse(sourceBrepStr, out var sourceBrepGuid))
            {
                // Malformed GUID → treat as orphan
                orphans.Add(obj);
                continue;
            }

            var sourceObj = doc.Objects.FindId(sourceBrepGuid);
            if (sourceObj == null || sourceObj.IsDeleted)
            {
                orphans.Add(obj);
            }
        }

        if (orphans.Count == 0) return 0;

        RemoveOrphans(doc, orphans);
        doc.Views.Redraw();
        return orphans.Count;
    }

    /// <summary>
    /// Removes a list of orphaned edge curve objects: deletes their toolpath geometry
    /// (2D + 3D), then deletes the edge curve object itself.
    /// </summary>
    private static void RemoveOrphans(RhinoDoc doc, List<RhinoObject> orphans)
    {
        foreach (var orphan in orphans)
        {
            try
            {
                // Remove 2D toolpath geometry
                ToolpathVisualizer.RemoveToolpathGeometry(doc, orphan);
                // Remove 3D toolpath geometry
                ToolpathVisualizer.RemoveToolpath3DGeometry(doc, orphan);
                // Delete the edge curve itself
                doc.Objects.Delete(orphan.Id, true);
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"[OrphanCleanup] Fehler beim Entfernen: {ex.Message}");
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        Unsubscribe();
        _disposed = true;
    }
}
