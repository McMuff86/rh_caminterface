using System.Globalization;
using System.Text;
using RhinoCNCExporter.Core.Emitters;
using RhinoCNCExporter.Core.LayerParser;
using RhinoCNCExporter.Core.Models;
using RhinoCNCExporter.Core.Naming;
using RhinoCNCExporter.Core.Profiles;

namespace RhinoCNCExporter.Core.Pipeline;

/// <summary>
/// Builds the final CNC output string for a single Plate.
/// Takes a Plate with its Machinings and routes each to the correct Emitter method.
/// Bridge between new data model and existing Emitter code.
/// Pure logic — no RhinoCommon.
/// </summary>
public class EmitterRouter : IEmitterRouter
{
    private readonly IEmitter _emitter;
    private readonly NameService _nameService;
    private readonly IMachineProfile _profile;

    public EmitterRouter(IEmitter emitter, NameService nameService, IMachineProfile profile)
    {
        _emitter = emitter;
        _nameService = nameService;
        _profile = profile;
    }

    /// <summary>
    /// Generate the complete CNC program string for one plate.
    /// </summary>
    public string GenerateProgram(Plate plate)
    {
        var parts = new List<string>();

        // Header
        parts.Add(_emitter.EmitHeader(
            plate.Name, plate.LengthX, plate.WidthY, plate.Thickness,
            _profile.SetupOffsetX, _profile.SetupOffsetY,
            _profile.SetupOffsetZ, _profile.SetupOffsetRot));

        var sequence = plate.PreserveMachiningOrder
            ? plate.Machinings
            : OrderMachinings(plate.Machinings);

        foreach (var machining in sequence)
        {
            var emitted = EmitMachining(plate, machining);
            if (!string.IsNullOrEmpty(emitted))
                parts.Add(emitted);
        }

        // Footer
        parts.Add(_emitter.EmitFooter());

        return string.Join("\n", parts);
    }

    private string EmitMachining(Plate plate, Machining m) => m switch
    {
        DrillMachining d => _emitter.EmitDrill(
            _nameService.CreateUnique(d.Name),
            d.X, d.Y, d.Depth, d.Diameter,
            SideToPlane(d.Side)),

        DrillPatternMachining dp => _emitter.EmitDrillPattern(
            _nameService.CreateUnique(dp.Name),
            dp.X, dp.Y, dp.Depth, dp.Diameter,
            dp.CountX, dp.CountY, dp.SpacingX, dp.SpacingY,
            SideToPlane(dp.Side)),

        RoutingMachining r => _emitter.EmitPolylinePass(
            _nameService.CreateUnique(r.Name + "_poly"),
            _nameService.CreateUnique(r.Name + "_op"),
            r.Points, r.TechCode ?? _profile.DefaultTech, r.Depth, r.ToolDiameter),

        RoutingWithArcsMachining ra => _emitter.EmitPolylinePassWithArcs(
            _nameService.CreateUnique(ra.Name + "_poly"),
            _nameService.CreateUnique(ra.Name + "_op"),
            ra.StartX, ra.StartY, ra.Segments,
            ra.TechCode ?? _profile.DefaultTech, ra.Depth, ra.ToolDiameter),

        GrooveRntMachining g => g.Axis == LayerParser.Axis.X
            ? _emitter.EmitRntX(_nameService.CreateUnique(g.Name),
                g.XStart, g.YStart, g.Width, g.Length, g.Depth, g.RntCode)
            : _emitter.EmitRntY(_nameService.CreateUnique(g.Name),
                g.XStart, g.YStart, g.Width, g.Length, g.Depth, g.RntCode),

        HorizontalDrillMachining h => EmitHorizontalDrillMachining(plate, h),

        BladeCutMachining b => _emitter.EmitBladeCut(
            b.Name,
            b.Angle, b.Segments,
            b.TechCode ?? _profile.DefaultTech, b.Depth, b.Strategy,
            SideToPlane(b.Side)),

        MacroMachining macro => EmitMacroRaw(macro),

        PocketMachining p => EmitPocket(p),

        _ => $"// UNSUPPORTED: {m.GetType().Name}"
    };

    private string EmitHorizontalDrillMachining(Plate plate, HorizontalDrillMachining h)
    {
        var spec = new HorizontalDrillSpec(h.Diameter, h.Depth, h.DrillSide);
        return EmitHorizontalDrill.Emit(
            _emitter,
            _nameService,
            h.Name,
            h.X,
            h.Y,
            plate.Thickness,
            plate.LengthX,
            plate.WidthY,
            spec);
    }

    /// <summary>
    /// Emit a raw macro call for MacroMachining operations.
    /// 
    /// SawCut_Lamello macros are special-cased because they have a complex parameter list
    /// with mixed types (numbers, booleans, nulls, strings, DZ-expressions). The parameter
    /// types are inferred heuristically:
    ///   - "null", "true", "false" → emit as bare keywords (no quotes)
    ///   - Starts with "DZ" → emit as expression (e.g., DZ-9.5)
    ///   - Parses as double AND doesn't start with "E" → emit as number
    ///   - Everything else → emit as quoted string
    /// 
    /// The "E" prefix exception prevents tech codes like "E010" from being emitted
    /// as the number 10 (they must remain quoted strings in the XCS output).
    /// 
    /// Generic (unrecognized) macros are emitted as comment placeholders until
    /// their format is reverse-engineered from production references.
    /// </summary>
    private string EmitMacroRaw(MacroMachining macro)
    {
        if (macro.MacroName.Equals("SawCut_Lamello", StringComparison.OrdinalIgnoreCase)
            && macro.Parameters.Count > 0)
        {
            var sb = new StringBuilder();
            sb.Append($"CreateMacro(\"{_nameService.CreateUnique(macro.Name)}\",\"SawCut_Lamello\",");
            for (int i = 0; i < macro.Parameters.Count; i++)
            {
                var p = macro.Parameters[i];
                if (i > 0) sb.Append(',');
                if (p == null)
                    sb.Append("null");
                else if (p == "true" || p == "false" || p == "null"
                    || p.StartsWith("DZ") // DZ-9.5 expression — must remain unquoted
                    || (double.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out _)
                        && !p.StartsWith("E"))) // "E010" is a tech code string, not a number
                    sb.Append(p);
                else
                    sb.Append($"\"{p}\"");
            }
            sb.Append(");");
            return sb.ToString();
        }

        // Generic macro: emit as comment placeholder.
        // Known unhandled macros: Rectangle (2 occurrences in production references)
        return $"// MACRO: {macro.MacroName} ({macro.Parameters.Count} params)";
    }

    private string EmitPocket(PocketMachining p)
    {
        // Pocket = multiple concentric routing passes
        var sb = new StringBuilder();
        for (int i = 0; i < p.Loops.Count; i++)
        {
            var loop = p.Loops[i];
            if (loop.Count < 2) continue;

            sb.Append(_emitter.EmitPolylinePass(
                _nameService.CreateUnique(p.Name + $"_pocket_poly_{i}"),
                _nameService.CreateUnique(p.Name + $"_pocket_op_{i}"),
                loop, p.TechCode ?? _profile.DefaultTech, p.Depth, p.ToolDiameter));

            if (i < p.Loops.Count - 1)
                sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string SideToPlane(MachiningSide side) => side switch
    {
        MachiningSide.Top => "Top",
        MachiningSide.Bottom => "Bottom",
        _ => "Top"
    };

    /// <summary>
    /// Standard machining order when PreserveMachiningOrder is false.
    /// Order: Closed contours/BladeCut (0) → Drills (1) → Drill Patterns (2)
    ///      → Horizontal Drills (3) → Pockets (4) → Open routing (5)
    ///      → RNT Grooves (6) → Macros (7) → Unknown (99).
    /// 
    /// This matches CNC best practices: outer contour first (establishes reference),
    /// then hole operations (faster tool changes when grouped), then grooves/macros last.
    /// </summary>
    internal static IEnumerable<Machining> OrderMachinings(IReadOnlyList<Machining> machinings)
    {
        return machinings
            .OrderBy(m => m switch
            {
                RoutingMachining { IsClosed: true } => 0,
                RoutingWithArcsMachining { IsClosed: true } => 0,
                BladeCutMachining => 0, // Blade cuts are typically contour operations
                DrillMachining => 1,
                DrillPatternMachining => 2,
                HorizontalDrillMachining => 3,
                PocketMachining => 4,
                RoutingMachining { IsClosed: false } => 5,
                RoutingWithArcsMachining { IsClosed: false } => 5,
                GrooveRntMachining => 6,
                MacroMachining => 7,
                _ => 99
            });
    }
}
