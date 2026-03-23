using System;
using System.Collections.Generic;
using System.Linq;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using RhinoCNCExporter.Core.Models;
using RhinoCNCExporter.Core.PlateDetection;

namespace RhinoCNCExporter.PlateDetection;

/// <summary>
/// Detects plates from 3D geometry in the Rhino document.
/// A plate is the largest closed Solid/Extrusion/Surface on a sub-layer.
///
/// Detection strategy:
/// 1. For each leaf layer (sub-layer with geometry):
///    a. Find all Breps/Extrusions/Surfaces
///    b. Find the largest one (by bounding box volume or face area) → that's the plate
///    c. Compute: thickness (thinnest bounding box dimension)
///    d. Compute: LengthX, WidthY (other two dimensions, largest first)
///    e. Compute: PlateOrigin (position + orientation in world space)
/// 2. Skip layers that match legacy patterns (CUT_, DRILL_, etc.)
/// 3. Fallback: If no solids found, look for WK_PIECE layer
///
/// DEPENDS ON: RhinoCommon (Brep, BoundingBox, Surface analysis)
/// </summary>
public class PlateDetector
{
    // Layer name prefixes to skip (these are machining layers, not plate layers)
    private static readonly string[] SkipPrefixes = {
        "CUT_", "POCKET_", "DRILL_", "DRILLROW_", "DRILLPAT_",
        "RBNUT_", "HDRILL_", "WK_PIECE"
    };

    /// <summary>
    /// Scan document for plates.
    /// Returns detected plates without machinings (those are added later).
    /// </summary>
    public IReadOnlyList<Plate> DetectPlates(RhinoDoc doc)
    {
        if (doc == null) return Array.Empty<Plate>();

        var plates = new List<Plate>();

        // Strategy 1: Find solids on each sub-layer
        var layerPlates = DetectFromLayers(doc);
        plates.AddRange(layerPlates);

        // Strategy 2 (Fallback): If no plates found, try WK_PIECE layer
        if (plates.Count == 0)
        {
            var wkPiece = DetectFromWkPiece(doc);
            if (wkPiece != null)
                plates.Add(wkPiece);
        }

        return plates;
    }

    /// <summary>
    /// Detect plates from each layer's geometry.
    /// For each leaf layer with geometry, find the largest solid → that's the plate.
    /// </summary>
    private IReadOnlyList<Plate> DetectFromLayers(RhinoDoc doc)
    {
        var plates = new List<Plate>();
        var processedLayers = new HashSet<int>();

        foreach (var layer in doc.Layers)
        {
            if (layer == null || layer.IsDeleted || !layer.IsVisible)
                continue;

            // Skip machining/legacy layers
            var layerName = layer.Name;
            if (SkipPrefixes.Any(p => layerName.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                continue;

            // Skip already processed
            if (!processedLayers.Add(layer.Index))
                continue;

            // Find all geometry on this layer
            var objects = doc.Objects.FindByLayer(layer);
            if (objects == null || objects.Length == 0)
                continue;

            // Find the largest solid/brep/extrusion
            var plate = FindLargestSolid(objects, layer.FullPath, layerName);
            if (plate != null)
                plates.Add(plate);
        }

        return plates;
    }

    /// <summary>
    /// Find the largest solid object among the given objects.
    /// Computes plate dimensions and origin from its bounding box and face analysis.
    /// </summary>
    private Plate? FindLargestSolid(RhinoObject[] objects, string layerPath, string layerName)
    {
        Brep? largestBrep = null;
        double largestVolume = 0;
        BoundingBox? largestBBox = null;

        foreach (var obj in objects)
        {
            if (obj == null || obj.IsDeleted) continue;

            Brep? brep = null;

            switch (obj.Geometry)
            {
                case Brep b when b.IsSolid:
                    brep = b;
                    break;
                case Extrusion ext:
                    brep = ext.ToBrep();
                    break;
                case Surface srf:
                    brep = srf.ToBrep();
                    break;
            }

            if (brep == null) continue;

            var bbox = brep.GetBoundingBox(true);
            if (!bbox.IsValid) continue;

            var volume = bbox.Volume;
            if (volume > largestVolume)
            {
                largestVolume = volume;
                largestBrep = brep;
                largestBBox = bbox;
            }
        }

        if (largestBrep == null || largestBBox == null || !largestBBox.Value.IsValid)
            return null;

        var bbox2 = largestBBox.Value;

        // Compute plate dimensions from bounding box
        // The thinnest dimension = thickness, other two = LPX, LPY
        double dimX = bbox2.Max.X - bbox2.Min.X;
        double dimY = bbox2.Max.Y - bbox2.Min.Y;
        double dimZ = bbox2.Max.Z - bbox2.Min.Z;

        var dims = new[] { dimX, dimY, dimZ }.OrderBy(d => d).ToArray();
        double thickness = dims[0];  // Thinnest = thickness
        double widthY = dims[1];     // Middle = width
        double lengthX = dims[2];    // Largest = length

        // Determine orientation based on which axis is thinnest
        PlateOrigin origin;
        if (thickness == dimZ || Math.Abs(thickness - dimZ) < 0.01)
        {
            // Plate lies flat (Z is thinnest) — most common for Boden, Deckel
            origin = CoordinateTransformer.CreateFlatOrigin(bbox2.Min.X, bbox2.Min.Y, bbox2.Min.Z);
            lengthX = dimX;
            widthY = dimY;
        }
        else if (thickness == dimY || Math.Abs(thickness - dimY) < 0.01)
        {
            // Plate stands in XZ plane (Y is thinnest) — side panels
            origin = CoordinateTransformer.CreateUprightXZOrigin(bbox2.Min.X, bbox2.Min.Y, bbox2.Min.Z);
            lengthX = dimX;
            widthY = dimZ;
        }
        else
        {
            // Plate stands in YZ plane (X is thinnest)
            origin = CoordinateTransformer.CreateUprightYZOrigin(bbox2.Min.X, bbox2.Min.Y, bbox2.Min.Z);
            lengthX = dimY;
            widthY = dimZ;
        }

        // Create Plate DTO
        return new Plate
        {
            Name = layerName,
            LengthX = Math.Round(lengthX, 2),
            WidthY = Math.Round(widthY, 2),
            Thickness = Math.Round(thickness, 2),
            LayerPath = layerPath,
            Origin = origin,
            Source = PlateSource.SolidDetection
        };
    }

    /// <summary>
    /// Fallback: Detect a single plate from the WK_PIECE layer (legacy compatibility).
    /// </summary>
    private Plate? DetectFromWkPiece(RhinoDoc doc)
    {
        var wkLayer = doc.Layers.FirstOrDefault(l =>
            l != null && !l.IsDeleted &&
            l.Name.Equals("WK_PIECE", StringComparison.OrdinalIgnoreCase));

        if (wkLayer == null) return null;

        var objects = doc.Objects.FindByLayer(wkLayer);
        if (objects == null || objects.Length == 0) return null;

        // WK_PIECE layer: find the bounding box of all geometry
        var allBbox = BoundingBox.Empty;
        foreach (var obj in objects)
        {
            if (obj?.Geometry == null) continue;
            allBbox.Union(obj.Geometry.GetBoundingBox(true));
        }

        if (!allBbox.IsValid) return null;

        double dimX = allBbox.Max.X - allBbox.Min.X;
        double dimY = allBbox.Max.Y - allBbox.Min.Y;
        double dimZ = allBbox.Max.Z - allBbox.Min.Z;

        // For WK_PIECE, assume flat plate at Z=0
        // DZ = Z dimension if > 0, else use default 19mm
        double thickness = dimZ > 0.1 ? dimZ : 19;

        return new Plate
        {
            Name = "WK_PIECE",
            LengthX = Math.Round(dimX, 2),
            WidthY = Math.Round(dimY, 2),
            Thickness = Math.Round(thickness, 2),
            LayerPath = "WK_PIECE",
            Origin = CoordinateTransformer.CreateFlatOrigin(allBbox.Min.X, allBbox.Min.Y, allBbox.Min.Z),
            Source = PlateSource.LegacyLayer
        };
    }
}
