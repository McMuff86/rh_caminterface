# Technische Architektur: 3D-to-CNC Pipeline

**Datum:** 24. März 2026  
**Version:** 1.1  
**Status:** Architecture Baseline + Implemented Deltas  
**Autoren:** Sentinel (Architect Agent)

---

## 1. Übersicht

Dieses Dokument definiert die technische Architektur für den Übergang von der aktuellen 2D-Layer-basierten Pipeline zu einem Block-basierten 3D-to-CNC System. Die Architektur ist **inkrementell** — jede Phase baut auf der vorherigen auf, nichts bricht.

### Architektur-Prinzipien

1. **Core bleibt RhinoCommon-frei** — Alle Datenmodelle, Interfaces und reine Logik in `RhinoCNCExporter.Core/`
2. **Plugin enthält Rhino-Abhängigkeiten** — Block-Scanning, Geometrie-Erkennung, UI in `RhinoCNCExporter/`
3. **Rückwärtskompatibel** — Layer-Konventionen funktionieren weiterhin, Block-Detection ist additiv
4. **Single Responsibility** — Jedes Modul hat eine klare Aufgabe
5. **Testbar** — Core-Logik mit xUnit testbar, ohne Rhino-Runtime

---

## 2. Modul-Struktur

### 2.1 Neue Module in Core (ohne RhinoCommon)

```
RhinoCNCExporter.Core/
├── Emitters/              ✅ BESTEHT — IEmitter, XilogEmitter, BiesseEmitter
├── LayerParser/           ✅ BESTEHT — Specs, LayerRegex
├── Naming/                ✅ BESTEHT — NameService
├── Profiles/              ✅ BESTEHT — IMachineProfile, MachineProfile, ConfigurableMachineProfile
│
├── Models/                🆕 Zentrale Datenmodelle (DTOs)
│   ├── Plate.cs
│   ├── Machining.cs
│   ├── FittingBlock.cs
│   ├── ExportJob.cs
│   ├── ExportAnalysis.cs
│   └── Tooling.cs         # ToolHolderDefinition, ToolDefinition, ToolLibrary, MachiningStrategy, MachiningToolOverride, ToolpathPlan
│
├── Blocks/                🆕 Block-Logik (ohne Rhino)
│   ├── BlockUserTextSchema.cs
│   ├── CncUserTextParser.cs
│   ├── MachiningFactory.cs
│   ├── ClamexMacroBuilder.cs
│   └── StarterBlocks/
│       └── StarterBlockDefinitions.cs
│
└── Pipeline/              🆕 Export-Orchestrierung (abstrakt)
    ├── BatchExportPlanner.cs
    ├── ExportModeResolver.cs
    ├── IMachiningBuilder.cs
    ├── IEmitterRouter.cs
    ├── IPlateExporter.cs
    ├── MachiningBuilder.cs
    ├── EmitterRouter.cs
    └── ToolpathPlanner.cs
```

### 2.2 Neue Module im Plugin (mit RhinoCommon)

```
RhinoCNCExporter/
├── Commands/              ✅ BESTEHT
├── UI/                    ✅ BESTEHT — ExportPanel, ExportDialog, ToolLibraryManagerDialog, ToolStrategyDialog
├── Services/              ✅ BESTEHT — ExportService, BlockAwareExportService, ExportService3D
│   ├── ToolLibraryStore.cs
│   └── ToolpathPreviewService.cs
├── Core/                  ✅ BESTEHT — Emitters, Geometry, LayerParser, etc.
│
├── PlateDetection/        🆕 Solid→Platte Erkennung
│   ├── PlateDetector.cs
│   └── CoordinateTransformer.cs
│
└── BlockScanning/         🆕 Block-Inserts scannen & parsen
    ├── BlockScanner.cs
    └── AssignmentResolver.cs
```

---

## 3. Datenmodell (Core/Models/)

### 3.1 Plate — Eine Platte im Raum

```csharp
namespace RhinoCNCExporter.Core.Models;

/// <summary>
/// Represents a single plate (Platte) detected from the 3D model.
/// Contains all information needed to generate a CNC program for this plate.
/// </summary>
public sealed record Plate
{
    /// <summary>Unique identifier — typically the layer name or user-assigned name.</summary>
    public required string Name { get; init; }

    /// <summary>Plate length in mm (X-dimension in plate-local coordinates).</summary>
    public required double LengthX { get; init; }

    /// <summary>Plate width in mm (Y-dimension in plate-local coordinates).</summary>
    public required double WidthY { get; init; }

    /// <summary>Plate thickness in mm (Z-dimension).</summary>
    public required double Thickness { get; init; }

    /// <summary>Material identifier (optional, for future use: stückliste, nesting).</summary>
    public string? Material { get; init; }

    /// <summary>Layer path in Rhino (e.g., "Korpus_1::Seite_links").</summary>
    public string? LayerPath { get; init; }

    /// <summary>
    /// Origin of the plate-local coordinate system in world coordinates.
    /// For 2D (Phase 1): always (0,0,0). 
    /// For 3D (Phase 3): bottom-left corner of the plate's main face.
    /// </summary>
    public PlateOrigin Origin { get; init; } = PlateOrigin.Identity;

    /// <summary>All machining operations assigned to this plate.</summary>
    public IReadOnlyList<Machining> Machinings { get; init; } = Array.Empty<Machining>();

    /// <summary>Source: how this plate was detected.</summary>
    public PlateSource Source { get; init; } = PlateSource.LegacyLayer;
}

/// <summary>
/// Plate-local coordinate system origin + orientation in world space.
/// Identity = plate lies flat at origin (Phase 1 / 2D mode).
/// </summary>
public sealed record PlateOrigin
{
    public double OriginX { get; init; }
    public double OriginY { get; init; }
    public double OriginZ { get; init; }

    /// <summary>X-axis direction of the plate in world coordinates (unit vector).</summary>
    public (double X, double Y, double Z) XAxis { get; init; } = (1, 0, 0);
    /// <summary>Y-axis direction of the plate in world coordinates (unit vector).</summary>
    public (double X, double Y, double Z) YAxis { get; init; } = (0, 1, 0);
    /// <summary>Normal (Z-axis) of the plate in world coordinates (unit vector).</summary>
    public (double X, double Y, double Z) Normal { get; init; } = (0, 0, 1);

    public static PlateOrigin Identity => new();
}

public enum PlateSource
{
    /// <summary>Detected from WK_PIECE layer (Phase 1 legacy).</summary>
    LegacyLayer,
    /// <summary>Detected as a Solid/Extrusion on a named layer (Phase 3).</summary>
    SolidDetection,
    /// <summary>Manually assigned by user.</summary>
    Manual
}
```

### 3.2 Machining — Eine Bearbeitung auf einer Platte

```csharp
namespace RhinoCNCExporter.Core.Models;

/// <summary>
/// A single machining operation in plate-local coordinates.
/// All positions are relative to the plate's (0,0) = bottom-left corner.
/// Z = depth from top surface (positive = into material).
/// </summary>
public abstract record Machining
{
    /// <summary>Display name for the operation (used in CNC program).</summary>
    public required string Name { get; init; }

    /// <summary>Machining side (Top, Bottom, Left, Right, Front, Back).</summary>
    public MachiningSide Side { get; init; } = MachiningSide.Top;

    /// <summary>Technology code (e.g., "E010", "E013").</summary>
    public string? TechCode { get; init; }

    /// <summary>Source of this machining (legacy layer, block, or manual).</summary>
    public MachiningSource Source { get; init; } = MachiningSource.LegacyLayer;
}

public enum MachiningSide { Top, Bottom, Left, Right, Front, Back }
public enum MachiningSource { LegacyLayer, BlockDetection, Manual }

// --- Concrete machining types ---

public sealed record DrillMachining : Machining
{
    public required double X { get; init; }
    public required double Y { get; init; }
    public required double Depth { get; init; }
    public required double Diameter { get; init; }
}

public sealed record DrillPatternMachining : Machining
{
    public required double X { get; init; }
    public required double Y { get; init; }
    public required double Depth { get; init; }
    public required double Diameter { get; init; }
    public required int CountX { get; init; }
    public required int CountY { get; init; }
    public required double SpacingX { get; init; }
    public required double SpacingY { get; init; }
}

public sealed record RoutingMachining : Machining
{
    /// <summary>Polyline points in plate-local coordinates (closed = contour, open = groove).</summary>
    public required IReadOnlyList<(double X, double Y)> Points { get; init; }
    public required double Depth { get; init; }
    public required double ToolDiameter { get; init; }
    public double? StepDown { get; init; }
    public bool IsClosed { get; init; }
}

public sealed record RoutingWithArcsMachining : Machining
{
    public required double StartX { get; init; }
    public required double StartY { get; init; }
    public required IReadOnlyList<PolySegment> Segments { get; init; }
    public required double Depth { get; init; }
    public required double ToolDiameter { get; init; }
    public double? StepDown { get; init; }
    public bool IsClosed { get; init; }
}

public sealed record PocketMachining : Machining
{
    /// <summary>Offset loops from outside to inside.</summary>
    public required IReadOnlyList<IReadOnlyList<(double X, double Y)>> Loops { get; init; }
    public required double Depth { get; init; }
    public required double ToolDiameter { get; init; }
    public double? StepDown { get; init; }
}

public sealed record GrooveRntMachining : Machining
{
    public required Axis Axis { get; init; }
    public required double XStart { get; init; }
    public required double YStart { get; init; }
    public required double Length { get; init; }
    public required double Width { get; init; }
    public required double Depth { get; init; }
    public required string RntCode { get; init; }
}

public sealed record MacroMachining : Machining
{
    /// <summary>Macro name (e.g., "SawCut_Lamello", "RNT", "Rectangle").</summary>
    public required string MacroName { get; init; }
    /// <summary>Ordered list of macro parameters (strings, nulls, numbers).</summary>
    public required IReadOnlyList<string?> Parameters { get; init; }
}

public sealed record HorizontalDrillMachining : Machining
{
    public required double X { get; init; }
    public required double Y { get; init; }
    public required double Depth { get; init; }
    public required double Diameter { get; init; }
    /// <summary>Which plate edge: L=Links(-X), R=Rechts(+X), V=Vorne(-Y), H=Hinten(+Y).</summary>
    public required char DrillSide { get; init; }
}
```

### 3.3 FittingBlock — Ein Beschlag-Block im Modell

```csharp
namespace RhinoCNCExporter.Core.Models;

/// <summary>
/// Represents a parsed Block-Insert with CNC_* UserText attributes.
/// Pure data — no RhinoCommon dependency.
/// </summary>
public sealed record FittingBlock
{
    /// <summary>Block definition name (e.g., "Topfband_35", "CLAMEX_P14").</summary>
    public required string BlockName { get; init; }

    /// <summary>CNC operation type from CNC_Type UserText.</summary>
    public required string CncType { get; init; }

    /// <summary>Insertion point in world coordinates.</summary>
    public required (double X, double Y, double Z) InsertionPoint { get; init; }

    /// <summary>Rotation angle in degrees (0, 90, 180, 270).</summary>
    public double Rotation { get; init; }

    /// <summary>All CNC_* UserText key-value pairs from the block instance.</summary>
    public required IReadOnlyDictionary<string, string> CncAttributes { get; init; }

    /// <summary>The Rhino layer this block insert lives on.</summary>
    public string? LayerName { get; init; }

    // --- Convenience accessors for common CNC_* keys ---

    public double? Diameter => TryGetDouble("CNC_Diameter");
    public double? Depth => TryGetDouble("CNC_Depth");
    public string? MacroName => CncAttributes.GetValueOrDefault("CNC_MacroName");
    public string? MacroParams => CncAttributes.GetValueOrDefault("CNC_MacroParams");
    public string? Orientation => CncAttributes.GetValueOrDefault("CNC_Orientation");
    public MachiningSide? CncSide => ParseSide(CncAttributes.GetValueOrDefault("CNC_Side"));

    private double? TryGetDouble(string key)
        => CncAttributes.TryGetValue(key, out var v) && double.TryParse(v,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : null;

    private static MachiningSide? ParseSide(string? s) => s?.ToUpperInvariant() switch
    {
        "TOP" => MachiningSide.Top,
        "BOTTOM" => MachiningSide.Bottom,
        "LEFT" => MachiningSide.Left,
        "RIGHT" => MachiningSide.Right,
        "FRONT" => MachiningSide.Front,
        "BACK" => MachiningSide.Back,
        _ => null
    };
}
```

### 3.4 ExportJob — Ein Export-Auftrag

```csharp
namespace RhinoCNCExporter.Core.Models;

/// <summary>
/// Represents a complete export job: one or more plates to be exported.
/// </summary>
public sealed record ExportJob
{
    /// <summary>Plates to export (each plate → one CNC file).</summary>
    public required IReadOnlyList<Plate> Plates { get; init; }

    /// <summary>Target machine format.</summary>
    public required MachineFormat Format { get; init; }

    /// <summary>Output directory for generated CNC files.</summary>
    public required string OutputDirectory { get; init; }

    /// <summary>Machine profile to use.</summary>
    public required string ProfileName { get; init; }

    /// <summary>Whether to use legacy layer scanning (Phase 1 compat).</summary>
    public bool UseLegacyLayers { get; init; } = true;

    /// <summary>Whether to use block detection (Phase 2+).</summary>
    public bool UseBlockDetection { get; init; } = false;
}

public enum MachineFormat
{
    Xilog,   // SCM Maestro (.xcs)
    Biesse,  // bSolid (.cix)
    Homag    // woodWOP (.mpr)
}
```

---

## 4. Core Interfaces & Klassen

### 4.1 Block-Logik (Core/Blocks/)

```csharp
namespace RhinoCNCExporter.Core.Blocks;

/// <summary>
/// Schema for CNC_* UserText keys on block instances.
/// Validates and parses UserText attributes.
/// </summary>
public static class BlockUserTextSchema
{
    // --- Required Keys ---
    public const string CNC_TYPE = "CNC_Type";

    // --- Common Optional Keys ---
    public const string CNC_DIAMETER = "CNC_Diameter";
    public const string CNC_DEPTH = "CNC_Depth";
    public const string CNC_SIDE = "CNC_Side";
    public const string CNC_TECHCODE = "CNC_TechCode";
    public const string CNC_ORIENTATION = "CNC_Orientation";
    public const string CNC_MACRO_NAME = "CNC_MacroName";
    public const string CNC_MACRO_PARAMS = "CNC_MacroParams";
    public const string CNC_PATTERN_X = "CNC_PatternX";
    public const string CNC_PATTERN_Y = "CNC_PatternY";
    public const string CNC_SPACING_X = "CNC_SpacingX";
    public const string CNC_SPACING_Y = "CNC_SpacingY";
    public const string CNC_STEPDOWN = "CNC_StepDown";
    public const string CNC_TOOL_DIA = "CNC_ToolDia";

    // --- Valid CNC_Type values ---
    public static readonly IReadOnlySet<string> ValidTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "DRILL", "DRILLPATTERN", "MACRO", "CUT", "POCKET", "GROOVE", "HDRILL"
    };

    /// <summary>Validate that a dictionary of CNC_* attributes has at least the required keys.</summary>
    public static (bool IsValid, string? Error) Validate(IReadOnlyDictionary<string, string> attrs)
    {
        if (!attrs.ContainsKey(CNC_TYPE))
            return (false, $"Missing required key: {CNC_TYPE}");
        if (!ValidTypes.Contains(attrs[CNC_TYPE]))
            return (false, $"Unknown CNC_Type: {attrs[CNC_TYPE]}. Valid: {string.Join(", ", ValidTypes)}");
        return (true, null);
    }
}
```

```csharp
namespace RhinoCNCExporter.Core.Blocks;

using RhinoCNCExporter.Core.Models;

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
    public static IReadOnlyList<Machining> CreateFromBlock(FittingBlock block,
        double plateLocalX, double plateLocalY, double plateLocalZ,
        double plateThickness)
    {
        // Dispatch based on CNC_Type
        return block.CncType.ToUpperInvariant() switch
        {
            "DRILL" => CreateDrill(block, plateLocalX, plateLocalY, plateThickness),
            "DRILLPATTERN" => CreateDrillPattern(block, plateLocalX, plateLocalY, plateThickness),
            "MACRO" => CreateMacro(block, plateLocalX, plateLocalY, plateThickness),
            "HDRILL" => CreateHorizontalDrill(block, plateLocalX, plateLocalY, plateThickness),
            "CUT" => CreateCut(block, plateLocalX, plateLocalY, plateThickness),
            "POCKET" => CreatePocket(block, plateLocalX, plateLocalY, plateThickness),
            "GROOVE" => CreateGroove(block, plateLocalX, plateLocalY, plateThickness),
            _ => Array.Empty<Machining>()
        };
    }

    // Each Create* method returns 1+ Machining operations.
    // Some blocks generate multiple operations (e.g., Exzenter = drill + pocket).
    // Implementation in IMPLEMENTATION phase — here we define the dispatch pattern.

    private static IReadOnlyList<Machining> CreateDrill(FittingBlock b, double x, double y, double dz)
        => throw new NotImplementedException("Phase 2 implementation");

    private static IReadOnlyList<Machining> CreateDrillPattern(FittingBlock b, double x, double y, double dz)
        => throw new NotImplementedException("Phase 2 implementation");

    private static IReadOnlyList<Machining> CreateMacro(FittingBlock b, double x, double y, double dz)
        => throw new NotImplementedException("Phase 2 implementation");

    private static IReadOnlyList<Machining> CreateHorizontalDrill(FittingBlock b, double x, double y, double dz)
        => throw new NotImplementedException("Phase 2 implementation");

    private static IReadOnlyList<Machining> CreateCut(FittingBlock b, double x, double y, double dz)
        => throw new NotImplementedException("Phase 3 implementation");

    private static IReadOnlyList<Machining> CreatePocket(FittingBlock b, double x, double y, double dz)
        => throw new NotImplementedException("Phase 3 implementation");

    private static IReadOnlyList<Machining> CreateGroove(FittingBlock b, double x, double y, double dz)
        => throw new NotImplementedException("Phase 3 implementation");
}
```

### 4.2 Pipeline-Orchestrierung (Core/Pipeline/)

```csharp
namespace RhinoCNCExporter.Core.Pipeline;

using RhinoCNCExporter.Core.Emitters;
using RhinoCNCExporter.Core.Models;
using RhinoCNCExporter.Core.Naming;
using RhinoCNCExporter.Core.Profiles;

/// <summary>
/// Builds the final CNC output string for a single Plate.
/// Takes a Plate with its Machinings and routes each to the correct Emitter method.
/// Pure logic — no RhinoCommon.
/// </summary>
public class EmitterRouter
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
        parts.Add(_emitter.EmitHeader(plate.Name, plate.LengthX, plate.WidthY, plate.Thickness,
            _profile.SetupOffsetX, _profile.SetupOffsetY,
            _profile.SetupOffsetZ, _profile.SetupOffsetRot));

        // Default: sort by type. When plate.PreserveMachiningOrder is true, use list order (production parity).
        var sequence = plate.PreserveMachiningOrder ? plate.Machinings : OrderMachinings(plate.Machinings);
        foreach (var machining in sequence)
        {
            parts.Add(EmitMachining(machining));
        }

        // Footer
        parts.Add(_emitter.EmitFooter());

        return string.Join("\n", parts);
    }

    private string EmitMachining(Machining m) => m switch
    {
        DrillMachining d => _emitter.EmitDrill(
            _nameService.CreateUnique(d.Name), d.X, d.Y, d.Depth, d.Diameter,
            SideToPlane(d.Side)),

        DrillPatternMachining dp => _emitter.EmitDrillPattern(
            _nameService.CreateUnique(dp.Name), dp.X, dp.Y, dp.Depth, dp.Diameter,
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

        _ => $"// UNSUPPORTED: {m.GetType().Name}"
    };

    private string EmitHorizontalDrill(HorizontalDrillMachining h)
    {
        // Create workplane, select it, emit drill — same pattern as EmitHorizontalDrill.cs
        var wpName = _nameService.CreateUnique($"WP_{h.DrillSide}_{h.Name}");
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(_emitter.EmitWorkplane(wpName, h.X, h.Y, 0, 0, 0)); // simplified
        sb.AppendLine(_emitter.EmitSelectWorkplane(wpName));
        sb.AppendLine(_emitter.EmitDrill(_nameService.CreateUnique(h.Name), 0, 0, h.Depth, h.Diameter));
        return sb.ToString();
    }

    private string EmitMacroRaw(MacroMachining macro)
    {
        // For macros like SawCut_Lamello — emit raw CreateMacro with params
        // This will be expanded per macro type in implementation
        return $"// MACRO: {macro.MacroName} ({macro.Parameters.Count} params)";
    }

    private static string SideToPlane(MachiningSide side) => side switch
    {
        MachiningSide.Top => "Top",
        MachiningSide.Bottom => "Bottom",
        _ => "Top"
    };

    /// <summary>Standard machining order: Contours → Drills → Patterns → Grooves → Macros.</summary>
    private static IEnumerable<Machining> OrderMachinings(IReadOnlyList<Machining> machinings)
    {
        return machinings
            .OrderBy(m => m switch
            {
                RoutingMachining { IsClosed: true } => 0,   // Outer contour first
                RoutingWithArcsMachining { IsClosed: true } => 0,
                DrillMachining => 1,
                DrillPatternMachining => 2,
                HorizontalDrillMachining => 3,
                PocketMachining => 4,
                RoutingMachining { IsClosed: false } => 5,  // Open paths (grooves)
                RoutingWithArcsMachining { IsClosed: false } => 5,
                GrooveRntMachining => 6,
                MacroMachining => 7,
                _ => 99
            });
    }
}
```

```csharp
namespace RhinoCNCExporter.Core.Pipeline;

using RhinoCNCExporter.Core.Models;
using RhinoCNCExporter.Core.Blocks;

/// <summary>
/// Combines machinings from multiple sources (legacy layers + blocks)
/// into a unified list per plate.
/// Pure logic — receives already-parsed data.
/// </summary>
public static class MachiningBuilder
{
    /// <summary>
    /// Merge legacy layer-based machinings with block-based machinings.
    /// Deduplicates by position (within tolerance) to avoid double-drilling.
    /// </summary>
    public static IReadOnlyList<Machining> MergeAndDeduplicate(
        IReadOnlyList<Machining> legacyMachinings,
        IReadOnlyList<Machining> blockMachinings,
        double positionTolerance = 0.5)
    {
        // Block-sourced machinings take priority over legacy
        // If a block-machining is within tolerance of a legacy one → drop the legacy
        var result = new List<Machining>(blockMachinings);

        foreach (var legacy in legacyMachinings)
        {
            bool isDuplicate = blockMachinings.Any(block =>
                AreSamePosition(legacy, block, positionTolerance));

            if (!isDuplicate)
                result.Add(legacy);
        }

        return result;
    }

    private static bool AreSamePosition(Machining a, Machining b, double tol)
    {
        var (ax, ay) = GetPosition(a);
        var (bx, by) = GetPosition(b);
        if (ax is null || bx is null) return false;
        return Math.Abs(ax.Value - bx.Value) < tol && Math.Abs(ay.Value - by.Value) < tol;
    }

    private static (double? X, double? Y) GetPosition(Machining m) => m switch
    {
        DrillMachining d => (d.X, d.Y),
        DrillPatternMachining dp => (dp.X, dp.Y),
        HorizontalDrillMachining h => (h.X, h.Y),
        _ => (null, null) // Non-positional machinings don't deduplicate
    };
}
```

---

## 5. Plugin-Module (mit RhinoCommon)

### 5.1 BlockScanner — Block-Inserts mit CNC_* finden

```csharp
namespace RhinoCNCExporter.BlockScanning;

using Rhino;
using Rhino.DocObjects;
using RhinoCNCExporter.Core.Models;
using RhinoCNCExporter.Core.Blocks;

/// <summary>
/// Scans a Rhino document for Block-Inserts that have CNC_* UserText attributes.
/// Converts them to FittingBlock DTOs (pure data, no Rhino references).
/// DEPENDS ON: RhinoCommon (InstanceReferenceGeometry, UserText)
/// </summary>
public class BlockScanner
{
    /// <summary>
    /// Scan the entire document for block instances with CNC_Type UserText.
    /// Returns FittingBlock DTOs grouped by their layer.
    /// </summary>
    public IReadOnlyList<FittingBlock> ScanDocument(RhinoDoc doc)
    {
        // Implementation:
        // 1. Iterate doc.Objects where Geometry is InstanceReferenceGeometry
        // 2. For each instance: read UserText keys starting with "CNC_"
        // 3. If CNC_Type exists → create FittingBlock DTO
        // 4. Extract InsertionPoint, Rotation from instance transform
        // Returns list of FittingBlock (pure data)
        throw new NotImplementedException();
    }

    /// <summary>
    /// Scan only selected objects for CNC blocks.
    /// </summary>
    public IReadOnlyList<FittingBlock> ScanSelection(RhinoDoc doc)
    {
        throw new NotImplementedException();
    }
}
```

### 5.2 AssignmentResolver — Welcher Block gehört zu welcher Platte?

```csharp
namespace RhinoCNCExporter.BlockScanning;

using RhinoCNCExporter.Core.Models;

/// <summary>
/// Resolves which FittingBlocks belong to which Plates.
/// 
/// Strategy (in priority order):
/// 1. LAYER MATCH: Block on same layer as plate → belongs to that plate
/// 2. PROXIMITY: Block insertion point inside plate bounding box → belongs to that plate  
/// 3. EXPLICIT: UserText "CNC_Plate" = plate name → forced assignment
///
/// DEPENDS ON: Only Core Models (no RhinoCommon)
/// </summary>
public class AssignmentResolver
{
    /// <summary>
    /// Assign fitting blocks to plates.
    /// Returns plates with updated Machinings lists.
    /// </summary>
    public IReadOnlyList<(Plate Plate, IReadOnlyList<FittingBlock> Blocks)> Resolve(
        IReadOnlyList<Plate> plates,
        IReadOnlyList<FittingBlock> allBlocks)
    {
        // Phase 2 (flat/2D): Pure layer-based assignment
        //   Block on layer "Seite_links" → Plate "Seite_links"
        //
        // Phase 3 (3D): Layer + proximity-based assignment
        //   Block insertion point within plate bounding box → belongs to plate
        //   Edge case: block between two plates → assign to closest face
        throw new NotImplementedException();
    }
}
```

### 5.3 PlateDetector — Solids zu Platten

```csharp
namespace RhinoCNCExporter.PlateDetection;

using Rhino;
using RhinoCNCExporter.Core.Models;

/// <summary>
/// Detects plates from 3D geometry in the Rhino document.
/// A plate is the largest closed Surface/Solid/Extrusion on a layer.
///
/// Phase 1: Not used (WK_PIECE layer).
/// Phase 2: Optional — can detect plates from named layers with solids.
/// Phase 3: Primary plate detection method.
///
/// DEPENDS ON: RhinoCommon (Brep, Surface, Extrusion analysis)
/// </summary>
public class PlateDetector
{
    /// <summary>
    /// Scan document for plates.
    /// Returns detected plates without machinings (those are added later).
    /// </summary>
    public IReadOnlyList<Plate> DetectPlates(RhinoDoc doc)
    {
        // Implementation approach:
        // 1. For each layer with geometry:
        //    a. Find all Breps/Extrusions/Surfaces
        //    b. Find the largest one (by area) → that's the plate
        //    c. Compute: thickness (thinnest bounding box dimension)
        //    d. Compute: LengthX, WidthY (other two dimensions)
        //    e. Compute: PlateOrigin (position + orientation in world space)
        // 2. Skip layers that match legacy patterns (CUT_, DRILL_, etc.)
        // 3. Return list of Plate DTOs
        throw new NotImplementedException();
    }
}
```

### 5.4 CoordinateTransformer — 3D→Platten-Lokal

```csharp
namespace RhinoCNCExporter.PlateDetection;

using RhinoCNCExporter.Core.Models;

/// <summary>
/// Transforms world coordinates to plate-local coordinates.
/// 
/// Plate-local system:
///   Origin = bottom-left corner of plate's main face
///   X = along plate length
///   Y = along plate width
///   Z = into the plate (depth, from top face)
///
/// Phase 1/2: Identity transform (plates are flat at Z=0).
/// Phase 3: Full 3D transform (plates can be anywhere in space).
///
/// DEPENDS ON: Only Core Models (math only, no RhinoCommon)
/// </summary>
public static class CoordinateTransformer
{
    /// <summary>
    /// Transform a world-space point into plate-local coordinates.
    /// </summary>
    public static (double X, double Y, double Z) WorldToPlateLocal(
        PlateOrigin origin,
        double worldX, double worldY, double worldZ)
    {
        // Vector from plate origin to point
        double dx = worldX - origin.OriginX;
        double dy = worldY - origin.OriginY;
        double dz = worldZ - origin.OriginZ;

        // Project onto plate axes (dot products)
        double localX = dx * origin.XAxis.X + dy * origin.XAxis.Y + dz * origin.XAxis.Z;
        double localY = dx * origin.YAxis.X + dy * origin.YAxis.Y + dz * origin.YAxis.Z;
        double localZ = dx * origin.Normal.X + dy * origin.Normal.Y + dz * origin.Normal.Z;

        return (localX, localY, localZ);
    }

    /// <summary>
    /// For Phase 1/2: Identity transform (no coordinate change needed).
    /// </summary>
    public static (double X, double Y, double Z) Identity(double x, double y, double z)
        => (x, y, z);
}
```

### 5.5 StarterBlocks heute, Block-Library später

**Stand 24.03.2026:** Der produktive Pfad nutzt aktuell **code-definierte Starter-Blöcke** in `RhinoCNCExporter.Core/Blocks/StarterBlocks/StarterBlockDefinitions.cs`. Diese Definitionen dienen Tests, Dokumentation und als Vorlage für künftige Commands oder Importpfade.

Eine echte `.3dm`-basierte Block-Library mit `BlockLibraryService`, Embedded Resources oder Import-Workflow ist weiterhin **Future Work** und aktuell nicht Teil des produktiven Codes.

---

## 6. Datenfluss-Diagramm

### Phase 1 (Legacy — aktuell)

```
Rhino Document
    │
    ▼
ExportService.ExportWithEmitter()
    │
    ├─ LayerRegex.TryParse*()          → Specs (CutSpec, DrillSpec, ...)
    ├─ GeometryUtils.ToPolyPoints()    → Point lists
    ├─ Emit*.Emit()                    → CNC code strings
    │
    ▼
IEmitter.EmitHeader/EmitDrill/...
    │
    ▼
.xcs / .cix file
```

### Phase 2 (Block-Detection, additiv)

```
Rhino Document
    │
    ├──────────────────────────────────┐
    ▼                                  ▼
ExportService (legacy)         BlockScanner.ScanDocument()
    │                                  │
    ▼                                  ▼
Specs + Points              List<FittingBlock>
    │                                  │
    │                                  ▼
    │                          AssignmentResolver.Resolve()
    │                                  │
    │                                  ▼
    │                          MachiningFactory.CreateFromBlock()
    │                                  │
    ▼                                  ▼
Legacy Machinings           Block Machinings
    │                                  │
    └──────────┬───────────────────────┘
               ▼
    MachiningBuilder.MergeAndDeduplicate()
               │
               ▼
    EmitterRouter.GenerateProgram()
               │
               ▼
    .xcs / .cix / .mpr file
```

### Phase 3 (Full 3D Pipeline)

```
Rhino Document
    │
    ├─────────────────┬──────────────────┐
    ▼                 ▼                  ▼
PlateDetector   BlockScanner     Legacy ExportService
    │                 │                  │
    ▼                 ▼                  │
List<Plate>    List<FittingBlock>        │ (fallback)
    │                 │                  │
    ▼                 ▼                  │
AssignmentResolver.Resolve()             │
    │                                    │
    ▼                                    │
CoordinateTransformer                    │
    │                                    │
    ▼                                    │
MachiningFactory + MachiningBuilder      │
    │                                    │
    ▼                                    │
List<Plate> with Machinings              │
    │                                    │
    ▼                                    │
EmitterRouter.GenerateProgram()          │
    │                                    │
    ▼                                    │
Per Plate: .xcs / .cix / .mpr           │
```

---

## 7. Core vs. Plugin Grenze

| Verantwortung | Core (ohne RhinoCommon) | Plugin (mit RhinoCommon) |
|---|---|---|
| **Datenmodelle** | ✅ Plate, Machining, FittingBlock, ExportJob | |
| **UserText Schema** | ✅ BlockUserTextSchema, Validation | |
| **Block→Machining** | ✅ MachiningFactory | |
| **Merge & Deduplicate** | ✅ MachiningBuilder | |
| **Emit CNC Code** | ✅ IEmitter, EmitterRouter | |
| **Coordinate Math** | ✅ CoordinateTransformer | |
| **Layer Parsing** | ✅ LayerRegex, Specs | |
| **Naming** | ✅ NameService | |
| **Profiles** | ✅ IMachineProfile | |
| **Block Scanning** | | ✅ BlockScanner (UserText lesen) |
| **Plate Detection** | | ✅ PlateDetector (Brep/Solid analyse) |
| **Assignment** | | ✅ AssignmentResolver (Layer-Zuordnung) |
| **Starter-Blöcke / Block-Library** | ✅ `StarterBlockDefinitions` (code-definiert, schema-konform) | ⏳ spätere `.3dm`-Library/Import-Workflows |
| **UI** | | ✅ ExportPanel, ExportDialog |
| **Rhino Commands** | | ✅ RhinoCNCExporterCommand, ExportXilogCommand |

**Regel:** Wenn es ohne `using Rhino;` geht → Core. Wenn es RhinoCommon braucht → Plugin.

---

## 8. Erweiterungspunkte

### Neue Maschinenformate
1. Neues `*Emitter : IEmitter` in Core/Emitters/
2. Neues `*Profile : IMachineProfile` in Core/Profiles/
3. EmitterRouter funktioniert automatisch

### Neue Block-Typen
1. Block-Definition erstellen (.3dm mit CNC_* UserText)
2. Optional: Spezial-Logik in MachiningFactory (für komplexe Blöcke wie CLAMEX)
3. BlockUserTextSchema.ValidTypes erweitern wenn neuer CNC_Type

### Neue Bearbeitungstypen
1. Neues `*Machining : Machining` Record in Core/Models/
2. Handler in EmitterRouter.EmitMachining() switch-expression
3. Neue IEmitter-Methode (falls nicht durch bestehende abgedeckt)

---

*Dieses Dokument ist die technische Grundlage für alle Implementierungsentscheidungen. Änderungen hier = Änderungen am Code.*
