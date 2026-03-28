# CLAUDE.md — Quick-Start for AI Agents

**Project:** RhinoCNCExporter — Rhino 8 Plugin for CNC Export  
**Language:** C# / .NET 7 / xUnit  
**Last updated:** 2026-03-28

---

## What This Is

A Rhino 8 plugin that converts 3D models (plates with block-based fittings) into CNC machine programs:
- **SCM/Maestro XCS** (`.xcs`) — production-quality, primary format
- **Biesse CIX** (`.cix`) — basic implementation
- **Homag MPR** (`.mpr`) — planned

Block-based system: Rhino 3D model blocks → detect plates → detect fittings/operations → generate CNC code.

## Build & Test

```bash
dotnet build                    # Build all projects
dotnet test                     # Run xUnit tests (Tests project only — no Rhino needed)
```

**Note:** Plugin project requires RhinoCommon SDK (Windows only). Tests run without Rhino.

## Architecture (3 Projects)

```
RhinoCNCExporter.Core/      # Pure logic — NO RhinoCommon dependency
├── Models/                 # DTOs: Plate, Machining (8 subtypes), FittingBlock, etc.
├── Blocks/                 # Block attribute parsing + MachiningFactory
├── Pipeline/               # EmitterRouter, MachiningBuilder, BatchExportPlanner
├── PlateDetection/         # CoordinateTransformer (pure math)
└── Profiles/               # Machine profiles (ConfigurableMachineProfile)

RhinoCNCExporter/           # Plugin WITH RhinoCommon
├── Commands/               # Rhino CLI commands
├── UI/                     # Eto.Forms panels/dialogs
├── Services/               # ExportService, FeatureReader, FaceTagger, ToolLibrary
├── BlockScanning/          # BlockScanner, AssignmentResolver
├── PlateDetection/         # PlateDetector (Rhino geometry → Plate)
└── Core/Emitters/          # XilogEmitter, BiesseEmitter, IEmitter

RhinoCNCExporter.Tests/     # xUnit tests (references Core only)
```

**Key rule:** Core has NO RhinoCommon dependency. All Rhino-specific code lives in the Plugin project.

## Key Data Flow

```
Rhino Model → PlateDetector (Rhino) → Plate[] (Core DTO)
            → BlockScanner (Rhino) → FittingBlock[] (Core DTO)
            → MachiningFactory (Core) → Machining[] (Core DTO)
            → MachiningBuilder.Merge (Core) → merged Machining[]
            → EmitterRouter (Core) → IEmitter.Emit*() → CNC string
            → File.Write
```

## Machining Types (Core/Models/Machining.cs)

| Type | XCS Output | Status |
|------|-----------|--------|
| `DrillMachining` | `CreateDrill` | ✅ Production |
| `DrillPatternMachining` | `CreatePattern` + `CreateDrill` | ✅ Production |
| `RoutingMachining` | `CreatePolyline` + `CreateRoughFinish` | ✅ Production |
| `RoutingWithArcsMachining` | `CreatePolyline` + arcs | ✅ Production |
| `GrooveRntMachining` | `CreateMacro("RNT")` | ✅ Production |
| `HorizontalDrillMachining` | `CreateWorkplane` + `CreateDrill` | ✅ Production |
| `BladeCutMachining` | `CreateSectioningMillingStrategy` + `CreateBladeCut` | ✅ Implemented |
| `MacroMachining` | `CreateMacro(...)` | ✅ SawCut_Lamello |
| `PocketMachining` | Multiple `CreatePolyline` loops | ✅ Basic |

## Sprint Status (as of 2026-03-28)

- **Sprint 1-4:** ✅ Complete (Foundation, Block-Scan, Plate-Detect, Multi-Export)
- **Sprint 5:** 🟡 Partial (4 production validation fixtures passing, gaps documented)
- **Sprint 6:** 🟡 In progress (Tool Library basics)
- **Sprint 7-8:** 🟡 In progress (Rough/Finish strategies, Toolpath Preview basics)

## Reference Files

- `tests/references/*.xcs` — 68 production XCS files from real CAD+T exports
- `tests/references/cadt/*.dwg` — Source DWG files
- `tests/test_0{1,2,3}.xcs` — Older test references
- `docs/SPRINT5-VALIDATION-GAPS.md` — Detailed gap analysis

## Important Conventions

1. **Tests are MANDATORY** for every commit
2. **Core must stay Rhino-free** — no RhinoCommon in Core/
3. **Emitter output must match production format** — compare against reference XCS files
4. **NameService:** 31-char max names, auto-dedup
5. **PreserveMachiningOrder:** Set on Plate to keep exact operation order (for production comparison)
6. **InvariantCulture** for all number formatting (German decimals would break CNC)

## Common Gotchas

- `catch` blocks should always capture `Exception ex` — no bare catches
- `nullable` is enabled — watch for null reference warnings
- XCS uses Unix line endings (`\n`) — never `\r\n`
- Number format: Use `FmtCompact()` for header values (no trailing zeros), `:F3` for coordinates
- DZ expressions: `{DZ}-2` in block attributes resolves at export time

## Deeper Documentation

- `AGENTS.md` — Full project guide with layer conventions, build instructions
- `docs/TECHNICAL-ARCHITECTURE.md` — Detailed architecture
- `docs/IMPLEMENTATION-PLAN.md` — Sprint tasks and status
- `docs/RESEARCH-CAM-FORMATS.md` — Format specifications (XCS, CIX, MPR)
- `docs/XCS-REFERENCE-ANALYSIS.md` — Production XCS command reference
- `ROADMAP.md` — Long-term vision
