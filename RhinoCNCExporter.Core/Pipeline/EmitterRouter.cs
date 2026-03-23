using System.Globalization;
using System.Text;
using RhinoCNCExporter.Core.Emitters;
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

        // Sort machinings by type and emit each
        foreach (var machining in OrderMachinings(plate.Machinings))
        {
            var emitted = EmitMachining(machining);
            if (!string.IsNullOrEmpty(emitted))
                parts.Add(emitted);
        }

        // Footer
        parts.Add(_emitter.EmitFooter());

        return string.Join("\n", parts);
    }

    private string EmitMachining(Machining m) => m switch
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

        HorizontalDrillMachining h => EmitHorizontalDrill(h),

        MacroMachining macro => EmitMacroRaw(macro),

        PocketMachining p => EmitPocket(p),

        _ => $"// UNSUPPORTED: {m.GetType().Name}"
    };

    private string EmitHorizontalDrill(HorizontalDrillMachining h)
    {
        var wpName = _nameService.CreateUnique($"WP_{h.DrillSide}_{h.Name}");
        var sb = new StringBuilder();

        // Create workplane based on drill side
        var (rotX, rotY) = h.DrillSide switch
        {
            'L' => (0.0, -90.0),  // Left side (-X)
            'R' => (0.0, 90.0),   // Right side (+X)
            'V' => (90.0, 0.0),   // Front side (-Y)
            'H' => (-90.0, 0.0),  // Back side (+Y)
            _ => (0.0, -90.0)
        };

        sb.AppendLine(_emitter.EmitWorkplane(wpName, h.X, h.Y, 0, rotX, rotY));
        sb.AppendLine(_emitter.EmitSelectWorkplane(wpName));
        sb.Append(_emitter.EmitDrill(_nameService.CreateUnique(h.Name), 0, 0, h.Depth, h.Diameter));
        return sb.ToString();
    }

    private string EmitMacroRaw(MacroMachining macro)
    {
        // For SawCut_Lamello macros, emit the full CreateMacro line directly
        if (macro.MacroName.Equals("SawCut_Lamello", StringComparison.OrdinalIgnoreCase)
            && macro.Parameters.Count > 0)
        {
            // Reconstruct CreateMacro from stored parameters
            var sb = new StringBuilder();
            sb.Append($"CreateMacro(\"{_nameService.CreateUnique(macro.Name)}\",\"SawCut_Lamello\",");
            for (int i = 0; i < macro.Parameters.Count; i++)
            {
                var p = macro.Parameters[i];
                if (i > 0) sb.Append(',');
                if (p == null)
                    sb.Append("null");
                else if (p == "true" || p == "false" || p == "null"
                    || p.StartsWith("DZ") // DZ-9.5 expression
                    || (double.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out _)
                        && !p.StartsWith("E")))
                    sb.Append(p);
                else
                    sb.Append($"\"{p}\"");
            }
            sb.Append(");");
            return sb.ToString();
        }

        // Generic macro: emit as comment placeholder
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

    /// <summary>Standard machining order: Contours → Drills → Patterns → Grooves → Macros.</summary>
    internal static IEnumerable<Machining> OrderMachinings(IReadOnlyList<Machining> machinings)
    {
        return machinings
            .OrderBy(m => m switch
            {
                RoutingMachining { IsClosed: true } => 0,
                RoutingWithArcsMachining { IsClosed: true } => 0,
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
