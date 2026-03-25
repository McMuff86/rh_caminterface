using System.Globalization;
using RhinoCNCExporter.Core.Models;

namespace RhinoCNCExporter.Core.Blocks;

/// <summary>
/// Converts a FittingBlock (parsed block with CNC_* attributes)
/// into one or more Machining operations in plate-local coordinates.
/// Pure logic — no RhinoCommon.
/// </summary>
public static class MachiningFactory
{
    /// <summary>
    /// Create Machining operations from a FittingBlock.
    /// Coordinates must already be in plate-local space.
    /// </summary>
    /// <param name="block">The parsed fitting block.</param>
    /// <param name="plateLocalX">Block X in plate-local coords.</param>
    /// <param name="plateLocalY">Block Y in plate-local coords.</param>
    /// <param name="plateLocalZ">Block Z in plate-local coords.</param>
    /// <param name="plateThickness">Plate thickness for {DZ} replacement.</param>
    public static IReadOnlyList<Machining> CreateFromBlock(
        FittingBlock block,
        double plateLocalX, double plateLocalY, double plateLocalZ,
        double plateThickness)
    {
        return block.CncType.ToUpperInvariant() switch
        {
            "DRILL" => CreateDrill(block, plateLocalX, plateLocalY, plateThickness),
            "DRILLPATTERN" => CreateDrillPattern(block, plateLocalX, plateLocalY, plateThickness),
            "MACRO" => CreateMacro(block, plateLocalX, plateLocalY, plateThickness),
            "HDRILL" => CreateHorizontalDrill(block, plateLocalX, plateLocalY, plateThickness),
            "BLADECUT" => CreateBladeCut(block, plateLocalX, plateLocalY, plateThickness),
            "CUT" => CreateCut(block, plateLocalX, plateLocalY, plateThickness),
            "POCKET" => CreatePocket(block, plateLocalX, plateLocalY, plateThickness),
            "GROOVE" => CreateGroove(block, plateLocalX, plateLocalY, plateThickness),
            _ => Array.Empty<Machining>()
        };
    }

    private static IReadOnlyList<Machining> CreateDrill(FittingBlock b, double x, double y, double dz)
    {
        var diameter = b.Diameter ?? 5.0;
        var depth = ResolveDepth(b, dz);
        var side = b.CncSide ?? MachiningSide.Top;

        var results = new List<Machining>
        {
            new DrillMachining
            {
                Name = b.BlockName,
                X = x,
                Y = y,
                Depth = depth,
                Diameter = diameter,
                Side = side,
                TechCode = b.CncAttributes.GetValueOrDefault(BlockUserTextSchema.CNC_TECHCODE),
                Source = MachiningSource.BlockDetection
            }
        };

        return results;
    }

    private static IReadOnlyList<Machining> CreateDrillPattern(FittingBlock b, double x, double y, double dz)
    {
        var diameter = b.Diameter ?? 5.0;
        var depth = ResolveDepth(b, dz);
        var side = b.CncSide ?? MachiningSide.Top;

        var countX = GetInt(b.CncAttributes, BlockUserTextSchema.CNC_PATTERN_X, 1);
        var countY = GetInt(b.CncAttributes, BlockUserTextSchema.CNC_PATTERN_Y, 1);
        var spacingX = GetDouble(b.CncAttributes, BlockUserTextSchema.CNC_SPACING_X, 0);
        var spacingY = GetDouble(b.CncAttributes, BlockUserTextSchema.CNC_SPACING_Y, 32);

        // Apply rotation: 90° swaps X/Y pattern directions
        var rotation = (int)b.Rotation;
        if (rotation == 90 || rotation == 270)
        {
            (countX, countY) = (countY, countX);
            (spacingX, spacingY) = (spacingY, spacingX);
        }

        return new Machining[]
        {
            new DrillPatternMachining
            {
                Name = b.BlockName,
                X = x,
                Y = y,
                Depth = depth,
                Diameter = diameter,
                CountX = countX,
                CountY = countY,
                SpacingX = spacingX,
                SpacingY = spacingY,
                Side = side,
                TechCode = b.CncAttributes.GetValueOrDefault(BlockUserTextSchema.CNC_TECHCODE),
                Source = MachiningSource.BlockDetection
            }
        };
    }

    private static IReadOnlyList<Machining> CreateMacro(FittingBlock b, double x, double y, double dz)
    {
        var macroName = b.MacroName;
        if (string.IsNullOrEmpty(macroName))
            return Array.Empty<Machining>();

        var rawParams = b.MacroParams ?? "";
        var expandedParams = ExpandTemplateParams(rawParams, dz, x, y);
        var paramList = ParseMacroParams(expandedParams);

        return new Machining[]
        {
            new MacroMachining
            {
                Name = b.BlockName,
                MacroName = macroName,
                Parameters = paramList,
                Side = b.CncSide ?? MachiningSide.Top,
                TechCode = b.CncAttributes.GetValueOrDefault(BlockUserTextSchema.CNC_TECHCODE),
                Source = MachiningSource.BlockDetection
            }
        };
    }

    private static IReadOnlyList<Machining> CreateHorizontalDrill(FittingBlock b, double x, double y, double dz)
    {
        var diameter = b.Diameter ?? 8.0;
        var depth = ResolveDepth(b, dz, defaultDepth: 30);
        var drillSide = b.CncAttributes.GetValueOrDefault(BlockUserTextSchema.CNC_SIDE)?.ToUpperInvariant() switch
        {
            "LEFT" => 'L',
            "RIGHT" => 'R',
            "FRONT" => 'V',
            "BACK" => 'H',
            _ => 'L' // default
        };

        return new Machining[]
        {
            new HorizontalDrillMachining
            {
                Name = b.BlockName,
                X = x,
                Y = y,
                Depth = depth,
                Diameter = diameter,
                DrillSide = drillSide,
                Side = b.CncSide ?? MachiningSide.Left,
                TechCode = b.CncAttributes.GetValueOrDefault(BlockUserTextSchema.CNC_TECHCODE),
                Source = MachiningSource.BlockDetection
            }
        };
    }

    private static IReadOnlyList<Machining> CreateBladeCut(FittingBlock b, double x, double y, double dz)
    {
        var angle = GetDouble(b.CncAttributes, BlockUserTextSchema.CNC_ANGLE, 45.0);
        var depth = ResolveDepth(b, dz, defaultDepth: 15.0);
        var side = b.CncSide ?? MachiningSide.Top;

        // Parse segments from CNC_Segments attribute
        var segments = ParseBladeCutSegments(b.CncAttributes.GetValueOrDefault(BlockUserTextSchema.CNC_SEGMENTS) ?? "", x, y);
        
        if (segments.Count == 0)
        {
            // Default segments based on block position - simple cross pattern
            segments = new BladeCutSegment[]
            {
                new("Cut segment_1", x - 10, y - 10, x + 10, y - 10),
                new("Cut segment_2", x + 10, y - 10, x + 10, y + 10),
                new("Cut segment_3", x + 10, y + 10, x - 10, y + 10),
                new("Cut segment_4", x - 10, y + 10, x - 10, y - 10)
            };
        }

        return new Machining[]
        {
            new BladeCutMachining
            {
                Name = b.BlockName,
                Angle = angle,
                Segments = segments,
                Depth = depth,
                Side = side,
                TechCode = b.CncAttributes.GetValueOrDefault(BlockUserTextSchema.CNC_TECHCODE) ?? "E015",
                Source = MachiningSource.BlockDetection
            }
        };
    }

    private static IReadOnlyList<Machining> CreateCut(FittingBlock b, double x, double y, double dz)
    {
        // CUT blocks generate routing machinings — placeholder for Phase 3
        // For now, return empty (geometry comes from Rhino curves, not block attributes)
        return Array.Empty<Machining>();
    }

    private static IReadOnlyList<Machining> CreatePocket(FittingBlock b, double x, double y, double dz)
    {
        // POCKET blocks generate pocket machinings — placeholder for Phase 3
        return Array.Empty<Machining>();
    }

    private static IReadOnlyList<Machining> CreateGroove(FittingBlock b, double x, double y, double dz)
    {
        // GROOVE blocks generate groove machinings — placeholder for Phase 3
        return Array.Empty<Machining>();
    }

    // --- Template expansion ---

    /// <summary>
    /// Expand template placeholders in macro parameters.
    /// {DZ} → plate thickness, {X} → position X, {Y} → position Y,
    /// {LPX}/{LPY} left as-is (resolved later with plate dimensions).
    /// Supports expressions like {DZ}-2 → "17" (for DZ=19).
    /// </summary>
    internal static string ExpandTemplateParams(string template, double dz, double x, double y)
    {
        if (string.IsNullOrEmpty(template)) return template;

        // Simple replacements first
        var result = template
            .Replace("{X}", Format(x))
            .Replace("{Y}", Format(y));

        // {DZ} with optional arithmetic: {DZ}-2, {DZ}+1, {DZ}*0.5
        result = System.Text.RegularExpressions.Regex.Replace(result,
            @"\{DZ\}([+\-*/]\d+\.?\d*)?",
            m =>
            {
                if (m.Groups[1].Success)
                {
                    var expr = m.Groups[1].Value;
                    var op = expr[0];
                    var val = double.Parse(expr[1..], CultureInfo.InvariantCulture);
                    var computed = op switch
                    {
                        '+' => dz + val,
                        '-' => dz - val,
                        '*' => dz * val,
                        '/' => dz / val,
                        _ => dz
                    };
                    return Format(computed);
                }
                return Format(dz);
            });

        return result;
    }

    private static IReadOnlyList<string?> ParseMacroParams(string paramStr)
    {
        if (string.IsNullOrWhiteSpace(paramStr))
            return Array.Empty<string?>();

        return paramStr.Split(',')
            .Select(p =>
            {
                var trimmed = p.Trim();
                return string.IsNullOrEmpty(trimmed) || trimmed.Equals("null", StringComparison.OrdinalIgnoreCase)
                    ? (string?)null
                    : trimmed;
            })
            .ToArray();
    }

    // --- Helpers ---

    private static double ResolveDepth(FittingBlock b, double plateThickness, double defaultDepth = 13)
    {
        // If through-drilling, depth = plate thickness
        if (b.Through)
            return plateThickness;

        // Check for CNC_Depth with possible placeholder
        if (b.CncAttributes.TryGetValue(BlockUserTextSchema.CNC_DEPTH, out var depthStr))
        {
            if (depthStr.Contains('{'))
            {
                // Expand {DZ} placeholder
                var expanded = ExpandTemplateParams(depthStr, plateThickness, 0, 0);
                if (double.TryParse(expanded, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                    return d;
            }
            if (double.TryParse(depthStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var depth))
                return depth;
        }

        return defaultDepth;
    }

    private static double GetDouble(IReadOnlyDictionary<string, string> attrs, string key, double defaultValue)
        => attrs.TryGetValue(key, out var v) && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : defaultValue;

    private static int GetInt(IReadOnlyDictionary<string, string> attrs, string key, int defaultValue)
        => attrs.TryGetValue(key, out var v) && int.TryParse(v, out var i) ? i : defaultValue;

    /// <summary>
    /// Parse BladeCut segments from a string representation.
    /// Format: "name1,startX,startY,endX,endY;name2,startX,startY,endX,endY;..."
    /// or JSON: [{"Name":"name1","StartX":10,"StartY":20,"EndX":30,"EndY":40}]
    /// </summary>
    private static IReadOnlyList<BladeCutSegment> ParseBladeCutSegments(string segmentsStr, double centerX, double centerY)
    {
        if (string.IsNullOrWhiteSpace(segmentsStr))
            return Array.Empty<BladeCutSegment>();

        var segments = new List<BladeCutSegment>();

        try
        {
            // Try JSON format first
            if (segmentsStr.TrimStart().StartsWith('['))
            {
                // JSON format - placeholder for future JSON parsing
                return Array.Empty<BladeCutSegment>();
            }

            // Comma-separated format
            var parts = segmentsStr.Split(';', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                var segmentData = parts[i].Split(',');
                if (segmentData.Length >= 5)
                {
                    var name = segmentData[0].Trim();
                    if (string.IsNullOrEmpty(name))
                        name = $"Cut segment_{i + 1}";

                    if (double.TryParse(segmentData[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var sx) &&
                        double.TryParse(segmentData[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var sy) &&
                        double.TryParse(segmentData[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var ex) &&
                        double.TryParse(segmentData[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var ey))
                    {
                        segments.Add(new BladeCutSegment(name, sx, sy, ex, ey));
                    }
                }
            }
        }
        catch
        {
            // Fall back to empty if parsing fails
            return Array.Empty<BladeCutSegment>();
        }

        return segments;
    }

    private static string Format(double v)
        => v.ToString("G", CultureInfo.InvariantCulture);
}
